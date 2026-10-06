# Startup and Data

## Automatic composition

Each versioned runtime package provides an Umbraco composer. When Umbraco discovers the composer, Ekom registers its dependencies and adds content finders, URL providers, notification handlers, cache initialization components, and startup filters. A standard package installation needs no application-level `AddEkom(...)` call; U17 and U18 hosts do need the standard Umbraco `AddComposers()` call.

The startup filters add Ekom request handling, including session, authentication/authorization integration, and Ekom middleware. This behavior is registered by the package; sites using the standard package installation do not add an explicit `AddEkom(...)` call.

## Initialization

When Umbraco reaches `RuntimeLevel.Run`, Ekom executes its migration plan and creates any missing tables. The current plans cover the base tables and later additions such as activity-log metadata, warehouse stock, and native stock reservations, with version-specific differences. Startup also runs order-table and decimal-stock compatibility migrations.

The `orders-indexes-v1` migration ensures a full unique index on `EkomOrders.UniqueId` on SQL Server and SQLite, including existing tables. Equivalent indexes under other names are retained, and the existing `ReferenceId` primary-key and clustering layout are not changed. New tables use the same checks. This phase does not add store/date/status indexes.

Order-index validation fails if the `ReferenceId` primary key is missing or incompatible, the expected index name has an incompatible definition, or duplicate order IDs prevent creation of the unique index. Orders are never automatically deleted or merged. Resolve the reported schema or data issue and restart to retry; the failed migration is not recorded as complete. Index creation can block writes and require additional disk space, so schedule upgrades of large databases in an appropriate maintenance window.

The next migration, `order-performance-indexes-v1`, adds these narrow indexes to existing and new SQL Server/SQLite installations:

| Table | Index | Keys |
| --- | --- | --- |
| `EkomOrders` | `IX_EkomOrders_CustomerId` | `CustomerId` |
| `EkomOrders` | `IX_EkomOrders_CustomerUsername` | `CustomerUsername` |
| `EkomOrdersActivityLog` | `IX_EkomOrdersActivityLog_Key_Date` | `Key`, `Date` |

Eligible equivalent indexes under other names are preserved. Filtered/partial, disabled, expression, or differently keyed indexes do not replace the expected definitions. SQLite verification expects the standard BINARY key collations. Conflicting named definitions stop the migration without replacing existing indexes. If a later index fails to install, indexes already created remain in place and are recognized on retry. Existing primary keys, column types, and orders are not rewritten.

Before creating a SQL Server performance index, declared key widths are checked against the 1,700-byte nonclustered-key limit. Unbounded or oversized custom/legacy column definitions stop installation for schema review instead of introducing a new key-size restriction on future writes. Standard mapped customer usernames are bounded to 200 characters and fit this limit.

Customer history applies the optional store filter in SQL before loading orders. An exact in-memory guard is retained because SQL Server equality may ignore case or trailing spaces; the returned store matching remains unchanged. Null/empty store filters still return history across stores.

Manager store/date indexes are deliberately deferred. Synthetic SQLite testing with 120,000 orders found large improvements for narrow-period totals but regressions for some broad-period lists: range retrieval plus sorting can lose to a reverse primary-key scan for `ORDER BY ReferenceId DESC`. The manager defaults to year-to-date. Evaluate actual list and totals plans together before installing date indexes; neither SQLite skip-scan behavior nor synthetic timings should be assumed for SQL Server. Customer/store composite indexes also added little benefit over the chosen single-column customer indexes in that dataset. `OrderStatusCol` is not an index key because existing SQL Server definitions can be unbounded or too wide.

In the same warm-cache synthetic dataset (120,000 orders, 360,000 activity entries), the chosen three indexes reduced customer-history query medians from about 15-17 ms to 0.03-0.17 ms and a single-order activity query from about 7.6 ms to 0.01 ms. The indexes added about 26 MiB and roughly doubled execution time for a batch inserting 1,000 orders plus 3,000 logs. These are illustrative SQLite results, not production or SQL Server guarantees; write measurements excluded transaction commit/rollback. Indexes trade storage and insert work for faster reads, so validate with representative staging data before release.

The content initializer uses Ekom's registered property editors to create data types, document types, and root content only when the Ekom root is absent. Existing Ekom installations are not recreated on every start. Initialization is skipped while Umbraco is installing or upgrading and proceeds after the application can run normally.

Do not assume that a freshly installed site is ready for checkout before the first successful start and cache initialization. Create and publish stores through the backoffice before relying on catalog or provider behavior.

## Data and caches

Ekom stores operational data such as orders, stock values, discount usage, activity logs, warehouse stock, reservations, and optionally customer data in database tables. Stores, catalog items, discounts, providers, zones, and related configuration are represented by Umbraco content and loaded into Ekom caches. Stock combines persisted values with cached product and variant views.

For U17 and U18, cache initialization is triggered by `UmbracoApplicationStartedNotification`; U13 initializes from its startup component. Content publishing, unpublishing, saving, deletion, moving, domain, and language notifications refresh affected caches afterward.

Startup catches and logs initialization failures, so a running web process alone does not prove that Ekom initialized successfully. Check logs for `Ekom startup failed`, verify the Ekom root and tables, and confirm stores are published before diagnosing API results.

In a multi-node deployment, all nodes must use consistent Umbraco and Ekom configuration and the same database. Follow Umbraco's main-domain and distributed-cache guidance, and review [stock reservations](../guides/reservations.md) before enabling reservations across nodes.

## Configuration boundaries

Core settings bind from `Ekom`; feature options bind from subsections including `Ekom:Tracking`, `Ekom:Reservations`, and `Ekom:OrderDiscountCalculation`. Use environment-specific configuration or secret storage for payment, analytics, and integration credentials.

See [Configuration](../getting-started/configuration.md) for supported settings.

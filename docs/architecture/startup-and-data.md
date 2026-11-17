[Ekom docs](../README.md) / [Architecture](README.md) / Startup and data

# Startup and data

This page describes what Ekom does when a site starts: how it registers itself with Umbraco, which database changes it makes, which content it creates, and how it loads its data into memory. It also covers what happens when one of these steps fails, and where each kind of Ekom data is stored. Read it when you plan a deployment or an upgrade, or when Ekom does not start as expected.

On this page:

- [Registration](#registration)
- [Order of the startup steps](#order-of-the-startup-steps)
- [Database tables and migrations](#database-tables-and-migrations)
- [Required order index](#required-order-index)
- [Performance and lookup indexes](#performance-and-lookup-indexes)
- [Ekom content and document types](#ekom-content-and-document-types)
- [Loading data into memory](#loading-data-into-memory)
- [Where Ekom keeps its data](#where-ekom-keeps-its-data)
- [Running on more than one server](#running-on-more-than-one-server)

## Registration

Each runtime package (`Ekom.U17`, `Ekom.U18`, `Ekom.U19`) contains an Umbraco composer, `EkomComposer`. Umbraco finds and runs it through the `AddComposers()` call in the site's `Program.cs`. A site does not call `AddEkom` itself.

The composer:

- inserts Ekom's catalog content finder before Umbraco's `ContentFinderByPageIdQuery`, and adds Ekom's catalog URL provider;
- adds the startup components `EnsureTablesExist`, `EnsureNodesExist`, `EkomStartup` and `TrackingAutomationEvents`;
- adds handlers for Umbraco notifications about content, domains and languages, and for `UmbracoApplicationStartedNotification`;
- calls `AddEkom`, which registers Ekom's services, session support and two startup filters. The startup filters add Ekom's middleware at the start of the request pipeline and map Ekom's controllers.

## Order of the startup steps

| Step | When | What it does | If it fails |
| --- | --- | --- | --- |
| `EnsureTablesExist` | Umbraco is at the `Run` level. | Runs Ekom's migration plan and makes sure every Ekom table and required index exists. | The site does not start. |
| `EnsureNodesExist` | Umbraco is at the `Run` level. | Creates Ekom's data types, document types and root content if they are missing, and adds properties that newer versions need. | Logs `Failed to Initialize EnsureNodesExist`. The site starts. |
| `EkomStartup` | Umbraco is at the `Run` level. | Sets the cultures that requests can use from the Umbraco languages, and runs two compatibility updates of the order and stock tables. Logs `Ekom started`. | Logs `Ekom startup failed`. The site starts. |
| Cache loading | `UmbracoApplicationStartedNotification` | Loads stores, catalog, providers, discounts, prices and stock into memory. Logs `Ekom cache rebuild published successfully`. | Logs `Ekom cache initialization failed`. The site starts. See [Loading data into memory](#loading-data-into-memory). |

While Umbraco is installing or upgrading, its level is below `Run` and Ekom skips the first three steps. They run on the next start at the `Run` level.

Because three of the four steps log their failure and let the site start, a running site does not prove that Ekom started correctly. After a deployment, check the log for the messages above, check that the **Ekom** root content and the Ekom tables exist, and check that the stores are published.

## Database tables and migrations

Ekom stores its migration state in Umbraco's key-value table, under `Umbraco.Core.Upgrader.State+Ekom`. The migration plan has these steps:

| State | Change |
| --- | --- |
| `1` | Creates the base tables. |
| `2` | Adds the type column to the order activity log. |
| `3` | Creates the warehouse stock table. |
| `native-reservations-v1` | Creates the stock reservation tables. |
| `orders-indexes-v1` | Adds the required unique index on `EkomOrders.UniqueId`. See [Required order index](#required-order-index). |
| `order-performance-indexes-v1` | Adds customer and activity log indexes. See [Performance and lookup indexes](#performance-and-lookup-indexes). |
| `lookup-indexes-v1` | Adds lookup indexes for coupons, payment attempts, reservations and order updates. |

On every start, after the migration plan, Ekom also makes sure these tables and columns exist: order reviews, database prices, the status columns on `EkomOrders`, payments, shipments and stock reservations. Once the plan is complete, it checks all tables and indexes again on each start.

If a migration step fails, the site does not start. The error says from which state and at which state the migration failed. The failed step is not recorded as done, so Ekom tries it again on the next start.

Ekom supports SQL Server and SQLite. On any other database provider, startup fails with `Unsupported database provider for EkomOrders indexes.`

## Required order index

Ekom requires a unique index on `EkomOrders.UniqueId`, named `IX_EkomOrders_UniqueId`. The `orders-indexes-v1` step adds it to existing tables, and new tables get it too.

- An existing full unique index on `UniqueId` with another name is accepted, and Ekom does not add a second one.
- The primary key on `ReferenceId` and the way the table is clustered are not changed.

The step stops the site from starting when:

- `EkomOrders` has no primary key on `ReferenceId` alone,
- an index named `IX_EkomOrders_UniqueId` exists but is not a full unique index on `UniqueId`,
- two orders have the same `UniqueId`.

Ekom never deletes or merges orders to make the index fit. Fix the reported problem in the database and restart; the step runs again.

Creating the index can block writes to `EkomOrders` while it runs and needs extra disk space. On a large database, upgrade in a maintenance window.

## Performance and lookup indexes

The `order-performance-indexes-v1` step adds these indexes on SQL Server and SQLite:

| Table | Index | Keys |
| --- | --- | --- |
| `EkomOrders` | `IX_EkomOrders_CustomerId` | `CustomerId` |
| `EkomOrders` | `IX_EkomOrders_CustomerUsername` | `CustomerUsername` |
| `EkomOrdersActivityLog` | `IX_EkomOrdersActivityLog_Key_Date` | `Key`, `Date` |

Rules for these indexes:

- An existing index with another name that has exactly the same keys, in the same order, is accepted. A filtered or partial index, a disabled index, or an index with other keys does not count. On SQLite the keys must use the default `BINARY` collation.
- If an index with the expected name exists with a different definition, the step stops and replaces nothing.
- On SQL Server, Ekom first checks that the declared column widths fit the 1,700-byte limit for a nonclustered index key. If they do not, or a column is declared without a maximum length, the step stops so that you can review the schema. Ekom does not change column types. `CustomerUsername` is declared with a maximum of 200 characters, so a standard table fits.
- If one index fails, the indexes created before it stay. They are found and accepted on the next try.

The `lookup-indexes-v1` step adds indexes on coupon code, coupon key, discount and coupon code together, payment attempts by order, stock reservations by order, and `EkomOrders.UpdateDate`. On SQL Server it also adds `IX_EkomOrders_StoreAlias_CustomerList` for the **Customers** view in the Ekom section. These indexes only make queries faster. If one cannot be created, Ekom logs a warning and starts anyway, and tries again on the next start.

Ekom does not add indexes on order store, date or status. A date index can make long order lists slower, because those lists are sorted by `ReferenceId`. If you add such indexes yourself, test the order list and the totals with real data first.

Customer order history filters by store in SQL, then compares the store alias again in memory. SQL Server can ignore upper and lower case and trailing spaces when it compares text, and the second check keeps the match exact. When no store is given, history includes all stores.

## Ekom content and document types

`EnsureNodesExist` checks for an **Ekom** root node (document type `ekom`) that is not in the recycle bin. When there is none, Ekom creates:

- its data types, using Ekom's property editors;
- its document types, in an **Ekom** document type folder;
- the **Ekom** root node with Catalog, Shipping Providers, Payment Providers, Discounts (with Product Discounts and Order Discounts), Stores, Zones and Metafields under it. The nodes are saved and published.

When the root node exists, Ekom does not create content again. On every start it does add document type properties and data types that newer Ekom versions need, so an existing site gets them without manual changes.

A new site has no stores yet. Create and publish a store before you rely on the catalog, providers or checkout; see [Quick start](../getting-started/quick-start.md).

## Loading data into memory

Ekom serves catalog and configuration reads from memory. When Umbraco raises `UmbracoApplicationStartedNotification`, Ekom reads the published content under the **Ekom** root and loads, in order: stores and their domains, categories, product discounts, products, variants, variant groups, zones, payment providers, shipping providers and discounts. It also loads prices, sellable stock, warehouse stock and coupons.

While the site runs, Ekom updates this copy when content is published, unpublished, deleted, moved or moved to the recycle bin, and when domains or languages are saved or deleted.

If the load fails, Ekom logs `Ekom cache initialization failed` with the error. The site keeps running.

- If the **Ekom** root node is missing or not published, the error is `Ekom root node not found.`
- If content changed while Ekom was loading it, Ekom starts the load again, up to 3 times in a row. If the load at startup still fails for that reason, Ekom keeps trying in the background, up to 30 more times, waiting from 2 to 30 seconds between tries. After the last try it logs `Ekom caches are still empty after 30 startup retries. Rebuild them from the backoffice.`
- For any other error, Ekom does not try again by itself. Fix the cause, then rebuild the Ekom caches from the backoffice or restart the site.

## Where Ekom keeps its data

| Data | Stored in |
| --- | --- |
| Stores, categories, products, variants, discounts, payment and shipping providers, zones, metafields | Umbraco content under the **Ekom** root, loaded into memory. |
| Orders, order activity logs and order reviews | Ekom tables, for example `EkomOrders` and `EkomOrdersActivityLog`. |
| Payments and shipments | Ekom tables, for example `EkomOrderPayments` and `EkomOrderShipments`. |
| Sellable stock and discount stock | Ekom tables, `EkomStock` and `EkomDiscountStock`, with a copy of sellable stock in memory. |
| Warehouse stock | The `EkomWarehouseStock` table, with a copy in memory. |
| Coupons | The `EkomCoupon` table, with a copy in memory. |
| Stock reservations and checkout attempts | Ekom tables, for example `EkomStockReservation` and `EkomCheckoutPaymentAttempt`. |
| Database prices | The `EkomPrice` table. See [Database price storage](../guides/database-price-storage.md). |

Settings are read from the `Ekom` configuration section, and some features read their own sub-section, such as `Ekom:Tracking`, `Ekom:Reservations` and `Ekom:OrderDiscountCalculation`. Keep payment, analytics and integration credentials in environment-specific configuration or a secret store. See [Configuration](../getting-started/configuration.md).

Do not write to Ekom's tables yourself. Ekom's memory copy and its checks do not see such changes. Use the `Ekom.API` classes instead.

## Running on more than one server

- Every server must use the same database and the same Umbraco and Ekom configuration.
- Each server keeps its own memory copy of the catalog and stock. Follow Umbraco's guidance for load balancing so that content changes reach every server.
- Several servers can start at the same time. The index steps accept an index that another server created in the meantime.
- Read [Stock reservations](../guides/reservations.md) before you turn on reservations on more than one server.

## Related pages

- [Installation](../getting-started/installation.md): what to check after the first start.
- [Architecture overview](overview.md): the layers and how a request moves through them.
- [Stock](../guides/stock.md): how sellable stock is read and changed.
- [Upgrading](../upgrading/README.md): steps for moving to a newer Umbraco or Ekom version.

[Back to Architecture](README.md)

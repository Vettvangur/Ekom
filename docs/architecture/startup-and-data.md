# Startup and Data

## Automatic composition

Each versioned runtime package provides an Umbraco composer. When Umbraco discovers the composer, Ekom registers its dependencies and adds content finders, URL providers, notification handlers, cache initialization components, and startup filters. A standard package installation needs no application-level `AddEkom(...)` call; U17 and U18 hosts do need the standard Umbraco `AddComposers()` call.

The startup filters add Ekom request handling, including session, authentication/authorization integration, and Ekom middleware. This behavior is registered by the package; sites using the standard package installation do not add an explicit `AddEkom(...)` call.

## Initialization

When Umbraco reaches `RuntimeLevel.Run`, Ekom executes its migration plan and creates any missing tables. The current plans cover the base tables and later additions such as activity-log metadata, warehouse stock, and native stock reservations, with version-specific differences. Startup also runs order-table and decimal-stock compatibility migrations.

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

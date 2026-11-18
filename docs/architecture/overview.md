[Ekom docs](../README.md) / [Architecture](README.md) / Overview

# Architecture overview

Ekom is an e-commerce package for Umbraco. It is built in layers: a commerce core that does not depend on Umbraco, an Umbraco integration for each supported Umbraco version, and a backoffice user interface. This page describes those layers and how they work together while a site runs.

On this page:

- [Layers](#layers)
- [How Ekom starts](#how-ekom-starts)
- [How a storefront request is handled](#how-a-storefront-request-is-handled)
- [Where data comes from](#where-data-comes-from)
- [Optional packages](#optional-packages)

## Layers

| Layer | Project in the repository | What it contains |
| --- | --- | --- |
| Shared primitives | `Ekom/Ekom.Core/Ekom.Common` | Small types used by the other core projects, such as the price cache and property value helpers. |
| Commerce core | `Ekom/Ekom.Core/Ekom` | The public API classes in `Ekom.API` (`Catalog`, `Order`, `Stock`, `Providers` and others), models, pricing, orders, checkout, discounts, stock, the memory caches, database access, the storefront and manager HTTP controllers, tracking, and the background services. |
| ASP.NET Core hosting | `Ekom/Ekom.Core/Ekom.AspNetCore` | The dependency injection registrations for the core, the mail service, and the startup step that maps Ekom's controllers. |
| Umbraco integration | `Ekom/Ekom.Umbraco/Ekom.U17`, built as `Ekom.U17`, `Ekom.U18` and `Ekom.U19` | The Umbraco composer, database setup, creation of Ekom's document types and content, memory cache loading, catalog routing and URLs, Ekom's request middleware, and handlers for Umbraco content changes. |
| Backoffice user interface | `Ekom/Ekom.Web/Ekom.Web.U17/Client`, built as `Ekom.Web.U17`, `Ekom.Web.U18` and `Ekom.Web.U19` | The **Ekom** section (orders, customers, analytics, printing) and Ekom's property editors. |

The three core projects target both .NET 8 and .NET 10. The Umbraco integration and the backoffice user interface are built once for each Umbraco version:

- `Ekom.U17` compiles the `Ekom.U17` source against Umbraco 17, for .NET 10.
- `Ekom.U18` compiles the same source against Umbraco 18, for .NET 10, with the `UMBRACO_18` compile constant.
- `Ekom.U19` compiles the same source against Umbraco 19, for .NET 11, with both `UMBRACO_18` and `UMBRACO_19` defined.
- The three web-assets projects build the same TypeScript source in `Ekom.Web.U17/Client/src`, each against its own version of Umbraco's backoffice packages.

So a feature in the shared source is in all three versions. Where the versions differ, the code uses `#if UMBRACO_18` or `#if UMBRACO_19`. The largest difference is catalog search, which on Umbraco 19 uses Umbraco's search provider instead of Examine.

See [Packages](packages.md) for the NuGet package each project produces.

## How Ekom starts

1. Umbraco runs Ekom's composer, `EkomComposer`, during composition. The composer registers Ekom's content finder and URL provider, its startup components and its handlers for Umbraco notifications, and then calls `AddEkom`, which registers the core services.
2. When the application builds its request pipeline, Ekom's startup filters add its middleware and map its controllers.
3. When Umbraco reaches the `Run` level, Ekom's startup components update the database schema, create Ekom's document types and root content if they are missing, and finish startup.
4. When Umbraco raises `UmbracoApplicationStartedNotification`, Ekom loads stores, the catalog, providers, discounts, zones, prices and stock into memory.
5. While the site runs, Umbraco notifications for content, domains and languages keep that memory copy up to date.

See [Startup and data](startup-and-data.md) for each step in detail.

## How a storefront request is handled

Ekom adds this middleware to the start of the request pipeline, in this order:

1. A request size limit for Ekom's public storefront routes (`Ekom:Api:MaxRequestBodySize`).
2. Request body buffering, so Ekom can read a form body more than once.
3. A guard that answers a malformed form post to an Ekom route with `400 Bad Request`.
4. ASP.NET Core session, authentication and authorization.
5. Ekom's request middleware, which finds the store for the request and the signed-in member, and keeps them for the rest of the request.
6. Ekom's tracking middleware, used by [Tracking and consent](../guides/tracking-and-consent.md).

After that, a catalog URL is resolved by Ekom's content finder to a category or product, and Ekom's HTTP endpoints are served by its controllers. Your own code calls the `Ekom.API` classes. They read catalog data from memory, and orders from the session or the database.

## Where data comes from

Ekom keeps two kinds of data:

- **Content in Umbraco.** Stores, categories, products, variants, discounts, payment and shipping providers, zones and metafields are Umbraco content nodes under the **Ekom** root. Editors change them in the backoffice. Ekom reads the published content into memory and serves catalog reads from there.
- **Rows in Ekom's own database tables.** Orders, order activity logs and reviews, stock counts, coupons and discount stock, warehouse stock, stock reservations, checkout and payment attempts, payments, shipments and database prices live in tables that Ekom creates in the Umbraco database.

Your code normally uses the `Ekom.API` classes or the service interfaces Ekom registers, not the tables or caches directly. Changing the tables yourself bypasses Ekom's memory copy and its checks.

## Optional packages

These packages are not needed to run Ekom:

- **Ekom.Printer** prints picking slips for orders on a warehouse printer. See its [README](https://github.com/Vettvangur/Ekom/blob/Ekom/Ekom/Printing/Ekom.Printer/README.md).
- **Algolia, Klaviyo and Mailchimp** integrations, in `Plugins/`. Each has its own package, settings and release. See [Integrations](../integrations/README.md).

## Related pages

- [Packages](packages.md): which package contains what.
- [Startup and data](startup-and-data.md): database changes, memory loading and failure handling at startup.
- [Installation](../getting-started/installation.md): install the packages on a site.
- [Services and dependency injection](../reference/services-and-dependency-injection.md): the services Ekom registers and how to replace them.

[Back to Architecture](README.md)

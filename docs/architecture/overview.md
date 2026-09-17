# Architecture Overview

Ekom is an Umbraco commerce package with a shared commerce core, version-specific Umbraco integrations, optional backoffice web assets, and separately released integrations.

## Layers

| Layer | Responsibility |
| --- | --- |
| `Ekom/Ekom.Core/Ekom.Common` | Small shared primitives used by the other core projects. |
| `Ekom/Ekom.Core/Ekom` | Commerce APIs, models, pricing and order services, repositories, caches, and provider abstractions. |
| `Ekom/Ekom.Core/Ekom.AspNetCore` | ASP.NET Core DI registrations, controllers, middleware, tracking, rate limiting, and hosted workers. |
| `Ekom/Ekom.Umbraco/Ekom.U10` | Umbraco 13 integration, published as `Ekom.U10`. |
| `Ekom/Ekom.Umbraco/Ekom.U17` | Umbraco 17 integration, published as `Ekom.U17`. |
| `Ekom/Ekom.Umbraco/Ekom.U18` | Umbraco 18 integration, published as `Ekom.U18`. |
| `Ekom/Ekom.Web/*` | Version-matched Ekom backoffice assets. |

The three core projects multi-target .NET 8 and .NET 10. The versioned Umbraco and web projects select the framework and Umbraco API surface used by an application. `Ekom.U10` is the historical name of the Umbraco 13, .NET 8 integration. U17 and U18 target .NET 10, and U18 compiles shared U17 integration source against Umbraco 18 dependencies.

Optional Algolia, Klaviyo, and Mailchimp projects live under `Plugins/`. They are not required by the base runtime and have independent package and release configuration.

## Application startup

The versioned Umbraco package exposes an `EkomComposer` implementing Umbraco's `IComposer`. Umbraco discovers it automatically during normal composition, so a package consumer does not call `AddEkom(...)` manually. U17 and U18 hosts must retain Umbraco's standard `AddComposers()` pipeline call.

During composition, Ekom adds its service registrations, content finders, URL providers, components, notification handlers, and startup filters. The startup path initializes database/content prerequisites and Ekom caches once Umbraco is at the run level. Ekom also registers middleware through startup filters.

## Runtime flow

1. The composer registers the Ekom core and Umbraco-specific services.
2. Startup filters add session, authentication/authorization integration, malformed-form protection, and Ekom request/tracking middleware.
3. Umbraco components migrate Ekom tables and create missing Ekom content structures after the runtime reaches `Run`.
4. Cache initialization reads published stores, catalog items, providers, discounts, zones, and stock into Ekom's caches.
5. Umbraco content, domain, and language notifications refresh affected caches while the application runs.

Orders and operational records are database-backed; catalog and configuration entities primarily originate in Umbraco content and are exposed through Ekom's caches and APIs. Application code normally consumes the registered `Ekom.API` classes or lower-level service interfaces rather than constructing these objects directly.

See [Startup and data](startup-and-data.md) for operational details and [Packages](packages.md) for installation boundaries.

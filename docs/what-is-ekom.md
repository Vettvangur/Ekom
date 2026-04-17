# What is Ekom

Ekom is an open source eCommerce package for Umbraco.

It is built for developers who want full control over catalog, checkout, orders, providers, integrations, and custom business logic without being locked into a closed platform.

## Key characteristics

- Open source and free to use
- Built for Umbraco and ASP.NET Core
- Supports Umbraco 10+
- Supports B2B and B2C scenarios
- Supports multilingual and multicurrency stores
- Includes catalog, cart, checkout, and order management
- Supports payment and shipping providers
- Can be used in headless and server-rendered solutions
- Can be extended through code, events, providers, and integrations

## Main packages

In most setups you will work with these packages:

- `Ekom.U10`
- `Ekom.Web`

### `Ekom.U10`

This is the main Ekom package for Umbraco 10+.

It contains the core commerce logic, APIs, services, controllers, events, and infrastructure.

### `Ekom.Web`

This contains the web assets required for the Ekom backoffice/manager UI.

Install this into the main site project that contains the website `wwwroot`.

## What Ekom gives you

Ekom provides a developer-focused commerce foundation:

- product and category querying
- cart and order handling
- shipping and payment provider selection
- order completion flows
- discounts and coupon handling
- manager/backoffice order UI
- integration points for plugins and external systems

## Typical usage model

You can use Ekom in two main ways:

### Server-side usage

Inject and use the Ekom API classes directly in your Umbraco application.

Examples:

- `Ekom.API.Order`
- `Ekom.API.Catalog`
- `Ekom.API.Store`

### Headless usage

Use Ekom’s HTTP endpoints from a frontend or external client.

This is useful for:

- custom frontend frameworks
- external checkout UIs
- headless commerce builds

## Related pages

- [Installation](installation.md)
- [Appsettings Reference](appsettings-reference.md)
- [Quick Start](quick-start.md)

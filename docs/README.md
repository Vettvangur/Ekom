# Ekom Documentation

This documentation covers the current Ekom packages for Umbraco 13, 17, and 18. Start with the compatibility and installation guides, then use the topic guides and references as needed.

For the product overview and ecosystem landing page, visit [ekomcommerce.com](https://www.ekomcommerce.com/).

## Getting started

- [Compatibility](getting-started/compatibility.md) — choose the correct Umbraco, .NET, runtime, and web-assets packages.
- [Installation](getting-started/installation.md) — install Ekom and verify first startup.
- [Configuration](getting-started/configuration.md) — configure the primary application settings.
- [Quick start](getting-started/quick-start.md) — create a small catalog and basket flow.

## Architecture

- [Architecture overview](architecture/overview.md)
- [Packages and projects](architecture/packages.md)
- [Startup, persistence, and caching](architecture/startup-and-data.md)

## Guides

- [Catalog and products](guides/catalog-and-products.md)
- [Stores](guides/stores.md)
- [Orders and baskets](guides/orders-and-baskets.md)
- [Checkout](guides/checkout.md)
- [Providers](guides/providers.md)
- [Discounts](guides/discounts.md)
- [Stock](guides/stock.md)
- [Native SQL reservations](guides/reservations.md)
- [Tracking and consent](guides/tracking-and-consent.md)
- [Linked order lines](guides/linked-order-lines.md)
- [Variant App](guides/variant-app.md)
- [Catalog imports](guides/imports.md)
- [Headless usage](guides/headless.md)

## Reference

- [.NET API](reference/dotnet-api.md)
- [Services and dependency injection](reference/services-and-dependency-injection.md)
- [Events](reference/events.md)
- [HTTP API](reference/http-api.md)
- [Configuration reference](reference/configuration-reference.md)
- [Models and behavior](reference/models-and-behavior.md)

## Manager

- [Manager overview](manager/overview.md)
- [Access and permissions](manager/access-and-permissions.md)
- [Orders](manager/orders.md)
- [Activity logs](manager/activity-logs.md)

## Integrations

- [Integration packages](integrations/README.md) — compatibility and links to the canonical plugin documentation.

## Samples

- [Sample applications](samples/overview.md)

## Upgrading

- [Upgrade overview](upgrading/overview.md)
- [Umbraco 13 to 17](upgrading/umbraco-13-to-17.md)
- [Umbraco 17 to 18](upgrading/umbraco-17-to-18.md)
- [Native reservation migration](upgrading/reservations.md)

## Contributing

- [Contributor setup](contributing/setup.md)
- [Build, test, and release](contributing/build-test-release.md)

## Documentation conventions

- Documentation describes the current `Ekom` branch unless a page identifies a version-specific difference.
- Paths and links are repository-relative so they work directly on GitHub.
- Plugin-specific configuration belongs in each plugin README; this documentation links to it rather than duplicating it.
- The canonical Ekom release history is [`Ekom/CHANGELOG.md`](../Ekom/CHANGELOG.md).

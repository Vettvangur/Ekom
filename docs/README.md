# Ekom documentation

Ekom is an e-commerce package for Umbraco. It adds stores, a product catalog, baskets, checkout, payments, shipping, discounts and an order manager to an Umbraco site.

These docs cover Ekom on Umbraco 17, 18 and 19. Umbraco 19 is a release candidate. For a product overview, visit [ekomcommerce.com](https://www.ekomcommerce.com/).

## Find what you need

| You want to | Go to |
| --- | --- |
| Install Ekom on a site for the first time | [Getting started](getting-started/README.md) |
| Build one part of a store, such as checkout or discounts | [Guides](guides/README.md) |
| Copy working code for a common task | [Examples](examples/README.md) |
| Look up a setting, method, event or endpoint | [Reference](reference/README.md) |
| Work with orders and customers in the backoffice | [Manager](manager/README.md) |
| Move a site to a newer Umbraco or Ekom version | [Upgrading](upgrading/README.md) |
| Understand how Ekom is put together | [Architecture](architecture/README.md) |
| Connect Algolia, Klaviyo or Mailchimp | [Integrations](integrations/README.md) |
| See a working example site | [Samples](samples/overview.md) |
| Work on Ekom itself | [Contributing](contributing/README.md) |

## New to Ekom

Follow these in order. The first four take you from an empty Umbraco site to a working basket.

1. [Compatibility](getting-started/compatibility.md): pick the packages that match your Umbraco version.
2. [Installation](getting-started/installation.md): install Ekom and check that it starts.
3. [Configuration](getting-started/configuration.md): set the main options.
4. [Quick start](getting-started/quick-start.md): create a small catalog and add a product to a basket.
5. [Guides](guides/README.md): build the rest of the store, one part at a time.
6. [Examples](examples/README.md): copy working code for common tasks.

## Sections

### [Getting started](getting-started/README.md)

Install and configure Ekom, and get a first basket working.

### [Guides](guides/README.md)

One guide for each part of a store. Each explains what the feature does, how to set it up and how to use it from your code.

- **Stores and catalog:** [Stores](guides/stores.md), [Catalog and products](guides/catalog-and-products.md), [Variant App](guides/variant-app.md), [Stock](guides/stock.md), [Database price storage](guides/database-price-storage.md), [Catalog imports](guides/imports.md)
- **Baskets and orders:** [Orders and baskets](guides/orders-and-baskets.md), [Linked order lines](guides/linked-order-lines.md), [Order custom data](guides/order-custom-data.md), [Order statuses](guides/order-statuses.md), [Order reviews](guides/order-reviews.md)
- **Checkout, payment and shipping:** [Checkout](guides/checkout.md), [Providers](guides/providers.md), [Payments](guides/payments.md), [Shipping](guides/shipping.md), [Stock reservations](guides/reservations.md), [Gift-card recovery](guides/giftcard-recovery.md)
- **Discounts:** [Discounts and coupons](guides/discounts.md)
- **Storefront integration:** [Headless storefronts](guides/headless.md), [Tracking and consent](guides/tracking-and-consent.md)

### [Examples](examples/README.md)

Working code you can copy: event handlers, your own product classes, imports with images and variants, storefront pages, Manager actions and background jobs.

### [Reference](reference/README.md)

Exact names, types and defaults, for looking something up.

- [.NET API](reference/dotnet-api.md)
- [Services and dependency injection](reference/services-and-dependency-injection.md)
- [Events](reference/events.md)
- [HTTP API](reference/http-api.md)
- [Configuration reference](reference/configuration-reference.md)
- [Models and behavior](reference/models-and-behavior.md)

### [Manager](manager/README.md)

The Ekom area of the Umbraco backoffice: orders, customers, analytics and who can access them.

### [Upgrading](upgrading/README.md)

Step-by-step guides for moving between Umbraco versions and for changes inside Ekom that need action from you.

### [Architecture](architecture/README.md)

How the packages fit together, what happens at startup, and where Ekom keeps its data.

### [Integrations](integrations/README.md)

The optional Algolia, Klaviyo and Mailchimp packages.

### [Samples](samples/overview.md)

The example sites in this repository and what they show.

### [Contributing](contributing/README.md)

Set up, build, test and release Ekom itself.

## About these docs

- They describe the current `Ekom` branch. A page says so when something differs between Umbraco 17, 18 and 19.
- Every page starts with a path back to its section and to this page, and ends with related pages.
- Each plugin keeps its own setup instructions in its README. These docs link to them and do not repeat them.
- The release history is in [`Ekom/CHANGELOG.md`](https://github.com/Vettvangur/Ekom/blob/Ekom/Ekom/CHANGELOG.md).

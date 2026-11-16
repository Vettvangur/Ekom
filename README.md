# Ekom

[![Ekom.U10](https://img.shields.io/nuget/vpre/Ekom.U10?label=Ekom.U10)](https://www.nuget.org/packages/Ekom.U10/)
[![Ekom.U17](https://img.shields.io/nuget/vpre/Ekom.U17?label=Ekom.U17)](https://www.nuget.org/packages/Ekom.U17/)
[![Ekom.U18](https://img.shields.io/nuget/vpre/Ekom.U18?label=Ekom.U18)](https://www.nuget.org/packages/Ekom.U18/)
[![Ekom.U19](https://img.shields.io/nuget/vpre/Ekom.U19?label=Ekom.U19)](https://www.nuget.org/packages/Ekom.U19/)
[![License](https://img.shields.io/badge/license-MIT-green)](https://github.com/Vettvangur/Ekom/blob/Ekom/LICENSE)

Ekom is a free, open-source ecommerce platform for Umbraco. The catalog is Umbraco content, so editors manage products where they already work, and orders are handled in a manager inside the backoffice.

**Product website:** [ekomcommerce.com](https://www.ekomcommerce.com/)

**Documentation:** [Browse the Ekom documentation](https://github.com/Vettvangur/Ekom/tree/Ekom/docs)

![The Ekom manager's order list](https://raw.githubusercontent.com/Vettvangur/Ekom/Ekom/docs/images/marketplace/orders-list-light.png)

## Installation

Install the runtime package and the matching web-assets package in the site's main project, the one that contains `wwwroot`.

| Umbraco | .NET | Runtime package | Backoffice assets |
| --- | --- | --- | --- |
| 13 | 8 | `Ekom.U10` | `Ekom.Web` |
| 17 | 10 | `Ekom.U17` | `Ekom.Web.U17` |
| 18 | 10 | `Ekom.U18` | `Ekom.Web.U18` |
| 19 (release candidate) | 11 | `Ekom.U19` | `Ekom.Web.U19` |

For example, on Umbraco 17:

```bash
dotnet add package Ekom.U17
dotnet add package Ekom.Web.U17
```

`Ekom.U10` is the retained package name for the Umbraco 13 integration. It does not support Umbraco 10.

Ekom registers itself through its Umbraco composer; do not call `AddEkom(...)` yourself. On first startup it creates its database tables, data types, document types and root content. Then create and publish a store.

Continue with the [installation guide](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/getting-started/installation.md), the [quick start](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/getting-started/quick-start.md) and the [compatibility guide](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/getting-started/compatibility.md).

### Payment providers

Offline payments, such as bank transfer, work with Ekom alone. Each online gateway is a separate package that the site references itself:

```bash
dotnet add package Ekom.Payments.Borgun
```

Available providers: `Ekom.Payments.AltaPay`, `Ekom.Payments.Borgun`, `Ekom.Payments.Netgiro`, `Ekom.Payments.PayTrail`, `Ekom.Payments.SiminnPay`, `Ekom.Payments.Straumur`, `Ekom.Payments.TeyaConsumerloans` and `Ekom.Payments.Valitor`. They require Ekom 1.2 or later.

See [Payments](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/guides/payments.md) and [Providers](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/guides/providers.md). A site that used Ekom.Payments 1.0 or earlier should read [Upgrading payments](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/upgrading/payments.md) first.

## Features

### Catalog

- [Products, variants and categories](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/guides/catalog-and-products.md) as Umbraco content, with a catalog view that shows price, stock and publish state on every card
- [Several stores](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/guides/stores.md) in one site, each with its own languages, currencies and catalog
- [Imports](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/guides/imports.md) that keep products, prices and stock in step with an ERP
- [Database price storage](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/guides/database-price-storage.md), so frequent price updates go live without publishing a product

![The catalog view of a category](https://raw.githubusercontent.com/Vettvangur/Ekom/Ekom/docs/images/marketplace/catalog-products-light.png)

### Selling

- [Baskets and orders](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/guides/orders-and-baskets.md) for consumers and for business customers
- [Checkout](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/guides/checkout.md) with shipping and payment [providers](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/guides/providers.md) you configure as content
- [Discounts](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/guides/discounts.md) on products, orders and quantities, and coupons with usage limits
- [Stock](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/guides/stock.md) with [reservations](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/guides/reservations.md), so two customers cannot buy the last item

### Payments and orders

- [Every payment attempt recorded on its order](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/guides/payments.md): provider, transaction ID, authorization code and card, with the payment status calculated from what the gateway reported
- [Separate order, payment and fulfillment statuses](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/guides/order-statuses.md), so an order can be paid but not yet shipped
- [Order reviews](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/guides/order-reviews.md) that flag orders a person must look at, such as a payment that does not match its order

### Manager

- [Orders](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/manager/orders.md): search, filter, change statuses, edit order lines and export to CSV
- [Customers](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/manager/customers.md) and [analytics](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/analytics.md) for the same orders
- An [activity log](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/manager/activity-logs.md) on every order, and [access per store](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/manager/access-and-permissions.md)

### Integrating and extending

- [Headless APIs](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/guides/headless.md) for catalog, basket, checkout and orders
- [GA4 and Meta tracking](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/guides/tracking-and-consent.md) with consent handling
- [Events](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/reference/events.md) and [services](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/reference/services-and-dependency-injection.md) for your own pricing, stock rules, search and manager actions

## Optional packages

| Package | Adds |
| --- | --- |
| [Ekom.Algolia](https://github.com/Vettvangur/Ekom/tree/Ekom/Plugins/Ekom.Algolia) | Product, category and content indexing, and product search through Algolia |
| [Ekom.Klaviyo](https://github.com/Vettvangur/Ekom/tree/Ekom/Plugins/Ekom.Klaviyo) | Order events and catalog synchronization with Klaviyo |
| [Ekom.Mailchimp](https://github.com/Vettvangur/Ekom/tree/Ekom/Plugins/Ekom.Mailchimp) | Mailchimp audience tags, subscriptions, purchase tracking and transactional messages |
| [Ekom.Printer](https://github.com/Vettvangur/Ekom/tree/Ekom/Ekom/Printing/Ekom.Printer) | Automatic picking-slip printing through a [Windows agent](https://github.com/Vettvangur/Ekom/tree/Ekom/Ekom/Printing/Ekom.Printer.Agent) |

Each package states the Ekom and Umbraco versions it supports. Check them before upgrading Ekom or Umbraco. See [Integration packages](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/integrations/README.md).

## Documentation

- [Getting started](https://github.com/Vettvangur/Ekom/tree/Ekom/docs/getting-started)
- [Feature guides](https://github.com/Vettvangur/Ekom/tree/Ekom/docs/guides)
- [Manager](https://github.com/Vettvangur/Ekom/tree/Ekom/docs/manager)
- [API and configuration reference](https://github.com/Vettvangur/Ekom/tree/Ekom/docs/reference)
- [Architecture](https://github.com/Vettvangur/Ekom/tree/Ekom/docs/architecture)
- [Samples](https://github.com/Vettvangur/Ekom/tree/Ekom/docs/samples)
- [Upgrading](https://github.com/Vettvangur/Ekom/tree/Ekom/docs/upgrading)

## Help and contributing

Report bugs and ask questions in [GitHub issues](https://github.com/Vettvangur/Ekom/issues).

To work on Ekom itself, see [contributor setup](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/contributing/setup.md) and [build, test, and release](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/contributing/build-test-release.md). The release history is in [`Ekom/CHANGELOG.md`](https://github.com/Vettvangur/Ekom/blob/Ekom/Ekom/CHANGELOG.md).

## License

Ekom is available under the [MIT License](https://github.com/Vettvangur/Ekom/blob/Ekom/LICENSE).

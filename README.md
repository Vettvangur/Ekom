# Ekom

[![Ekom.U10](https://img.shields.io/nuget/vpre/Ekom.U10?label=Ekom.U10)](https://www.nuget.org/packages/Ekom.U10/)
[![Ekom.U17](https://img.shields.io/nuget/vpre/Ekom.U17?label=Ekom.U17)](https://www.nuget.org/packages/Ekom.U17/)
[![Ekom.U18](https://img.shields.io/nuget/vpre/Ekom.U18?label=Ekom.U18)](https://www.nuget.org/packages/Ekom.U18/)
[![Ekom.Algolia](https://img.shields.io/nuget/vpre/Ekom.Algolia?label=Algolia)](https://www.nuget.org/packages/Ekom.Algolia/)
[![Ekom.Klaviyo](https://img.shields.io/nuget/vpre/Ekom.Klaviyo?label=Klaviyo)](https://www.nuget.org/packages/Ekom.Klaviyo/)
[![Ekom.Mailchimp](https://img.shields.io/nuget/vpre/Ekom.Mailchimp?label=Mailchimp)](https://www.nuget.org/packages/Ekom.Mailchimp/)
[![License](https://img.shields.io/badge/license-MIT-green)](https://github.com/Vettvangur/Ekom/blob/Ekom/LICENSE)

Ekom is a free, open-source ecommerce platform for Umbraco. It provides a customizable catalog, baskets and orders, checkout and payment-provider integration, discounts, stock management, multi-store support, tracking, and headless APIs.

**Product website:** [ekomcommerce.com](https://www.ekomcommerce.com/)

**Documentation:** [Browse the Ekom documentation](https://github.com/Vettvangur/Ekom/tree/Ekom/docs)

## Supported platforms

| Umbraco | .NET | Runtime package | Backoffice assets |
| --- | --- | --- | --- |
| 13 | 8 | `Ekom.U10` | `Ekom.Web` |
| 17 | 10 | `Ekom.U17` | `Ekom.Web.U17` |
| 18 | 10 | `Ekom.U18` | `Ekom.Web.U18` |

`Ekom.U10` is the retained package name for the Umbraco 13 integration. It does not mean that the current package supports Umbraco 10.

See the [compatibility guide](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/getting-started/compatibility.md) for package and framework details.

## Installation

Install both the runtime package and matching web-assets package in the site's main project—the project that contains `wwwroot`.

### Umbraco 18

```bash
dotnet add package Ekom.U18
dotnet add package Ekom.Web.U18
```

### Umbraco 17

```bash
dotnet add package Ekom.U17
dotnet add package Ekom.Web.U17
```

### Umbraco 13

```bash
dotnet add package Ekom.U10
dotnet add package Ekom.Web
```

Ekom registers itself through its Umbraco composer. A standard installation does not call `AddEkom(...)` manually.

Continue with the [installation guide](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/getting-started/installation.md) and [quick start](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/getting-started/quick-start.md).

## Features

- Multi-store, multilingual, and multicurrency catalogs
- Products, variants, metafields, categories, search, and imports
- B2C and B2B baskets, orders, checkout, and order management
- Extensible payment and shipping providers
- Product, order, quantity, and coupon discounts
- Primary stock, native SQL reservations, and display-only warehouse stock
- Headless catalog, order, provider, and checkout APIs
- GA4 and Meta tracking with consent handling
- Algolia, Klaviyo, and Mailchimp integrations
- Extension points for events, search, pricing, stock policies, and manager actions

## Documentation

- [Getting started](https://github.com/Vettvangur/Ekom/tree/Ekom/docs/getting-started)
- [Architecture](https://github.com/Vettvangur/Ekom/tree/Ekom/docs/architecture)
- [Feature guides](https://github.com/Vettvangur/Ekom/tree/Ekom/docs/guides)
- [API and configuration reference](https://github.com/Vettvangur/Ekom/tree/Ekom/docs/reference)
- [Manager](https://github.com/Vettvangur/Ekom/tree/Ekom/docs/manager)
- [Samples](https://github.com/Vettvangur/Ekom/tree/Ekom/docs/samples)
- [Upgrading](https://github.com/Vettvangur/Ekom/tree/Ekom/docs/upgrading)
- [Contributing](https://github.com/Vettvangur/Ekom/tree/Ekom/docs/contributing)

## Integrations

- [Ekom.Algolia](https://github.com/Vettvangur/Ekom/tree/Ekom/Plugins/Ekom.Algolia)
- [Ekom.Klaviyo](https://github.com/Vettvangur/Ekom/tree/Ekom/Plugins/Ekom.Klaviyo)
- [Ekom.Mailchimp](https://github.com/Vettvangur/Ekom/tree/Ekom/Plugins/Ekom.Mailchimp)

Provider packages may have their own compatibility requirements. Confirm those before upgrading Ekom or Umbraco.

## Development and releases

See [contributor setup](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/contributing/setup.md) and [build, test, and release](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/contributing/build-test-release.md).

The canonical Ekom release history is in [`Ekom/CHANGELOG.md`](https://github.com/Vettvangur/Ekom/blob/Ekom/Ekom/CHANGELOG.md). Pull requests use squash merges and Conventional Commit-style titles so release-please can prepare releases.

## License

Ekom is available under the [MIT License](https://github.com/Vettvangur/Ekom/blob/Ekom/LICENSE).

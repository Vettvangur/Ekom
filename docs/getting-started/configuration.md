# Configuration

[Ekom docs](../README.md) / [Getting started](README.md) / Configuration

Ekom reads its settings from the `Ekom` section of the site's configuration. This page lists the settings most sites look at after installing Ekom: what each one does, its default, and what happens with a wrong value. Ekom runs with all defaults, so you only add the settings you want to change.

The settings come from ASP.NET Core configuration, so a value can come from `appsettings.json`, an environment-specific file such as `appsettings.Production.json`, environment variables, user secrets or any other configuration provider. Keep API keys, passwords and production endpoints out of files you commit.

On this page:

- [Example](#example)
- [Core settings](#core-settings)
- [VAT rounding](#vat-rounding)
- [Stock reservations](#stock-reservations)
- [Ekom section access](#ekom-section-access)
- [Settings for other features](#settings-for-other-features)

For every setting, see the [Configuration reference](../reference/configuration-reference.md).

## Example

```json
{
  "Ekom": {
    "BasketCookieLifetime": 7,
    "CategoryRootLevel": 3,
    "VatCalcRounding": "AwayFromZero",
    "VatRoundingScope": "PerUnit",
    "VatIncludedPerUnitPolicy": "PreserveStickerGross",
    "Reservations": {
      "Enabled": true,
      "Timeout": 30
    },
    "Manager": {
      "SectionAccessGroup": "ekom",
      "StoreGroupPermissions": {
        "Store": ["ekom", "shop-staff"]
      }
    }
  }
}
```

In environment variables, use a double underscore between levels, for example `Ekom__Reservations__Enabled=true`.

## Core settings

| Setting | Default | What it does |
| --- | --- | --- |
| `PerStoreStock` | `false` | `false`: one stock count per product or variant, shared by all stores. `true`: a separate count for each store. See [Stock](../guides/stock.md). |
| `DisableStock` | `false` | `true`: Ekom no longer rejects an order line because there is too little stock. |
| `BasketCookieLifetime` | `1` | How many days the basket cookie lasts. Decimals are allowed. A value that is not a number is ignored and `1` is used. |
| `ShareBasket` | `false` | `true`: one basket is shared by all stores. The stores must use the same currencies. |
| `UserBasket` | `false` | `true`: a signed-in member has one basket, saved on the member, instead of a basket linked through a cookie. |
| `ApplyVatOnShipping` | `false` | `true`: VAT is added to the shipping cost. |
| `CategoryRootLevel` | `3` | The Umbraco content level of your top-level categories. `Catalog.GetRootCategories` returns the categories at this level. The value must be a whole number; any other value makes that method throw. |
| `DefaultProductOrderBy` | `DateDesc` | Default sort order of product lists. Accepts `UmbracoSortOrderAsc`, `UmbracoSortOrderDesc`, `TitleAsc`, `TitleDesc`, `PriceAsc`, `PriceDesc`, `DateAsc`, `DateDesc`, `SkuAsc`, `SkuDesc`, `Score` and `NoOrder`. An unknown value is ignored and `DateDesc` is used. |
| `GlobalCatalog` | `false` | `true`: when a product or category is not found in the current store, Ekom looks for it in the other stores. |
| `CustomImage` | `images` | The alias of the property that holds product, variant and variant group images. |
| `AbsoluteUrls` | `false` | `true`: the URLs Umbraco gives products and categories include the store's domain, for example `https://shop.example.com/shirts/blue-shirt`. Use it when stores run on different domains. |
| `ExamineSearchIndex` | `ExternalIndex` | The index that catalog search uses. On Umbraco 19 the value is a search index alias, and `ExternalIndex` means Umbraco's published content index. See [Compatibility](compatibility.md#what-differs-between-umbraco-17-18-and-19). |
| `EmailNotifications` | None | The address that receives Ekom's notification emails. Separate several addresses with commas. Ekom sends through Umbraco's SMTP settings (`Umbraco:CMS:Global:Smtp`), from the address in `Umbraco:CMS:Global:Smtp:From`. When this setting is empty, Ekom sends no notification email unless your code passes a recipient. |
| `UseDatabasePrices` | `false` | `true`: prices can be changed in the database without publishing products. See [Database price storage](../guides/database-price-storage.md). |
| `AutoClosePaidAndFulfilledOrders` | `true` | Close an order automatically when it is paid and fully fulfilled. Set `Ekom:Stores:{storeAlias}:AutoClosePaidAndFulfilledOrders` to change it for one store. See [Order, payment, and fulfillment statuses](../guides/order-statuses.md). |

The store node in Umbraco has its own **User Basket**, **Share Basket Between Stores** and **Apply Vat On Shipping** properties. For a store that has the property, Ekom uses the store's value and ignores `UserBasket`, `ShareBasket` or `ApplyVatOnShipping`. The settings are the fallback for stores without the property.

Use `true` or `false` for settings that turn something on or off.

## VAT rounding

| Setting | Default | Values |
| --- | --- | --- |
| `VatCalcRounding` | `AwayFromZero` | `None`, `RoundDown`, `RoundUp`, `RoundToEven`, `AwayFromZero` |
| `VatRoundingScope` | `PerUnit` | `PerUnit`, `PerTotal` |
| `VatIncludedPerUnitPolicy` | `PreserveStickerGross` | `PreserveStickerGross`, `LineLevelVat` |

An unknown value is ignored and the default is used.

`VatIncludedPerUnitPolicy` matters when prices include VAT. `PreserveStickerGross` keeps the line total equal to the shelf price times the quantity. `LineLevelVat` works out the VAT again from the line's net amount; the VAT of each unit and the VAT of the order total can then differ slightly.

## Stock reservations

A stock reservation holds stock for a customer while they pay. Reservations are off by default. Read [Stock reservations](../guides/reservations.md) before you turn them on, in particular when the site runs on more than one server.

| Setting | Default | What it does |
| --- | --- | --- |
| `Reservations:Enabled` | `false` | `true`: Ekom reserves stock during checkout. |
| `Reservations:Timeout` | `30` | How many minutes a reservation lasts. If it is not set, Ekom uses the older `Ekom:ReservationTimeout` setting, and then `30`. |
| `Reservations:Stores:{storeAlias}:Enabled` and `:Timeout` | Not set | The same two settings for one store. A store value replaces the general value for that store. |
| `Reservations:WorkerEnabled` | `true` | Runs the background job that ends expired reservations and deletes old completed ones. |
| `Reservations:PollInterval` | `00:00:30` | How often the background job runs, as a .NET `TimeSpan`. Must be more than zero and at most one day. |
| `Reservations:BatchSize` | `100` | How many reservations the background job handles in one run. Must be from 1 to 10000. |
| `Reservations:CompletedRetention` | `7.00:00:00` | How long completed reservations are kept before the background job deletes them, as a .NET `TimeSpan`. Must not be negative. |

Ekom checks `PollInterval`, `BatchSize` and `CompletedRetention` when the site starts, also when `WorkerEnabled` is `false`. An out-of-range value stops the site from starting with an `ArgumentOutOfRangeException` that names the setting.

If the background job fails, it waits longer before each new try, up to 5 minutes.

## Ekom section access

These settings decide which backoffice users can work in the **Ekom** section and which stores they see there. Umbraco administrators always have access to every store.

| Setting | What it does |
| --- | --- |
| `Manager:SectionAccessGroup` | Backoffice user group aliases that may use the **Ekom** section, separated by commas. |
| `Manager:StoreGroupPermissions` | For each store alias, the user group aliases that may work with that store. A group listed here may also use the section. |

When neither setting lists a group, every signed-in backoffice user who can open the **Ekom** section can work with every store. Set at least one of them on a site where not every backoffice user may see orders.

Use group aliases, not display names. The user group must also have the **Ekom** section in Umbraco, or the section is not shown. See [Access and permissions](../manager/access-and-permissions.md) for examples.

## Settings for other features

These features have their own section under `Ekom`. Each guide explains the settings.

| Section | Feature | Guide |
| --- | --- | --- |
| `Ekom:Payments` | Settings for payment providers. | [Payments](../guides/payments.md) |
| `Ekom:Headless` | Calls to your front end when content changes. | [Headless storefronts](../guides/headless.md) |
| `Ekom:Tracking` | Consent and purchase events for GA4 and Meta. | [Tracking and consent](../guides/tracking-and-consent.md) |
| `Ekom:OrderDiscountCalculation` | The API key for the order discount calculation endpoint. | [Discount APIs and behavior](../guides/discounts/developers.md) |
| `Ekom:VariantApp` | Extra variant fields that editors can change in the Variant App. | [Variant App](../guides/variant-app.md) |
| `Ekom:Manager:Customers` | How the **Customers** view groups orders into customers. | [Customers](../manager/customers.md) |
| `Ekom:Search` and `Ekom:Api` | Which fields anonymous callers may search, and the largest request body the storefront endpoints accept. | [HTTP API](../reference/http-api.md) |

The Algolia, Klaviyo and Mailchimp packages also read their settings from under `Ekom`: `Ekom:Algolia`, `Ekom:Klaviyo` and `Ekom:Mailchimp`. See [Integrations](../integrations/README.md).

## Related pages

- [Configuration reference](../reference/configuration-reference.md): every setting in one list.
- [Stock](../guides/stock.md): what `PerStoreStock` and `DisableStock` change.
- [Stock reservations](../guides/reservations.md): how reservations work before you turn them on.
- [Access and permissions](../manager/access-and-permissions.md): give backoffice users access to orders by store.
- [Orders and baskets](../guides/orders-and-baskets.md): how the basket cookie and member baskets work.

Next: [Quick start](quick-start.md)

[Back to Getting started](README.md)

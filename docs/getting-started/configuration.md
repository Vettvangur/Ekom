# Configuration

Core settings are read from the `Ekom` section of the site's ASP.NET Core configuration. Values can therefore come from `appsettings.json`, environment-specific files, environment variables, user secrets, or another configured provider. Keep credentials and production endpoints out of committed configuration.

```json
{
  "Ekom": {
    "ExamineSearchIndex": "ExternalIndex",
    "BasketCookieLifetime": 1,
    "CategoryRootLevel": 3,
    "VatCalcRounding": "AwayFromZero",
    "VatRoundingScope": "PerUnit",
    "VatIncludedPerUnitPolicy": "PreserveStickerGross",
    "Reservations": {
      "Enabled": false,
      "Timeout": 30,
      "WorkerEnabled": true,
      "PollInterval": "00:00:30",
      "BatchSize": 100,
      "CompletedRetention": "7.00:00:00"
    },
    "Manager": {
      "SectionAccessGroup": "ekom",
      "StoreGroupPermissions": {
        "store": ["commerce-editors"]
      }
    }
  }
}
```

## Core settings

| Setting | Default | Purpose |
| --- | --- | --- |
| `PerStoreStock` | `false` | Use stock cached per store instead of per product or variant. |
| `ExamineSearchIndex` | `ExternalIndex` | Examine index used for catalog search. |
| `ExamineSearchNormalizedFields` | `nodeName`, `title`, `pageTitle`, `sku`, `searchTags`, `summary`, `description` | Fields copied into normalized fields for forgiving search. |
| `ShareBasket` | `false` | Share baskets between stores. Participating stores need compatible currencies. |
| `BasketCookieLifetime` | `1` day | Lifetime of the order cookie. |
| `CustomImage` | `images` | Media property alias used for product images. |
| `CategoryRootLevel` | `3` | Minimum Umbraco level considered a category. |
| `ApplyVatOnShipping` | `false` | Apply VAT to shipping costs. |
| `UserBasket` | `false` | Store one basket per signed-in member using the member's `orderId`. |
| `DisableStock` | `false` | Disable stock validation. |
| `AbsoluteUrls` | `false` | Generate absolute backoffice URLs for multi-site scenarios. |
| `DefaultProductOrderBy` | `DateDesc` | Default catalog sort order. |
| `GlobalCatalog` | `false` | Search other stores when an item is absent from the current store. |
| `EmailNotifications` | Umbraco mail setting | Override the recipient used by Ekom mail notifications. |
| `CustomerData` | `false` | Persist checkout customer data in the Ekom customer-data table. |

VAT settings accept `None`, `RoundDown`, `RoundUp`, `RoundToEven`, or `AwayFromZero` for `VatCalcRounding`; `PerUnit` or `PerTotal` for `VatRoundingScope`; and `PreserveStickerGross` or `LineLevelVat` for `VatIncludedPerUnitPolicy`.

`BasketCookieLifetime` and reservation `Timeout` are numeric values interpreted as days and minutes respectively. `PollInterval` and `CompletedRetention` use .NET `TimeSpan` strings. Invalid `CategoryRootLevel` or reservation values can fail during startup or option validation, so validate deployment configuration before release.

## Reservations

Automatic reservations are opt-in with `Reservations:Enabled`; the default is `false`. `Reservations:Timeout` is in minutes and defaults to 30. The older `Ekom:ReservationTimeout` setting remains a fallback for the timeout, but new configuration should use `Ekom:Reservations:Timeout`.

The expiry worker is enabled by default. `WorkerEnabled`, `PollInterval`, `BatchSize`, and `CompletedRetention` control its behavior. See the [stock reservations guide](../guides/reservations.md) before changing reservation behavior in a multi-node checkout deployment.

## Manager access

`Manager:SectionAccessGroup` is a comma-separated list of backoffice group aliases allowed to enter the Ekom manager. `Manager:StoreGroupPermissions` maps a store alias to groups allowed to work with that store. Umbraco administrators bypass these restrictions. The legacy `SectionAccessRules` setting only affects Umbraco 13 dashboard visibility when current manager groups are empty; it does not grant manager API or store access.

## Feature sections

Configure these features under their own `Ekom` subsections:

- `Ekom:Payments` for provider-specific payment settings.
- `Ekom:Headless:ReValidateApis` for store-specific external revalidation endpoints.
- `Ekom:Tracking` for consent capture and GA4 or Meta dispatching.
- `Ekom:OrderDiscountCalculation:ApiKey` to enable the order-discount calculation endpoint.
- `Ekom:VariantApp` for extra editable variant fields.

Plugin settings also live below `Ekom`, for example `Ekom:Algolia`, `Ekom:Klaviyo`, and `Ekom:Mailchimp`. Refer to the relative repository documentation for [Algolia](../../Plugins/Ekom.Algolia/README.md), [Klaviyo](../../Plugins/Ekom.Klaviyo/README.md), and [Mailchimp](../../Plugins/Ekom.Mailchimp/README.md).

See [Quick start](quick-start.md) for the next implementation step and the [configuration reference](../reference/configuration-reference.md) for the full current settings list.

# Configuration Reference

Core settings live below `Ekom` in `appsettings.json`. Options binding is case-insensitive. The values below are defaults verified from the current option/configuration classes.

## Core settings

| Key | Type/default | Behavior |
| --- | --- | --- |
| `Ekom:PerStoreStock` | `bool`, `false` | Uses `{storeAlias}_{itemKey}` stock identities instead of global item stock. |
| `Ekom:ExamineSearchIndex` | `string`, `ExternalIndex` | Default Examine searcher/index name. |
| `Ekom:ExamineSearchNormalizedFields` | array | Defaults to `nodeName`, `title`, `pageTitle`, `sku`, `searchTags`, `summary`, `description`. |
| `Ekom:ShareBasket` | `bool`, `false` | Allows store basket sharing; stores must have compatible currencies. |
| `Ekom:ApplyVatOnShipping` | `bool`, `false` | Default shipping VAT behavior. |
| `Ekom:AbsoluteUrls` | `bool`, `false` | Produces absolute backoffice URLs. |
| `Ekom:DefaultProductOrderBy` | `OrderBy`, `DateDesc` | Default `ProductQuery` ordering; invalid values fall back to `DateDesc`. |
| `Ekom:BasketCookieLifetime` | number, `1` | Order-cookie lifetime in days. |
| `Ekom:GlobalCatalog` | `bool`, `false` | Allows product/category ID/key fallback to another store. |
| `Ekom:CustomImage` | `string`, `images` | Product image property alias. |
| `Ekom:CategoryRootLevel` | `int`, `3` | Umbraco level considered a root catalog category. |
| `Ekom:CustomerData` | `bool`, `false` | Enables Ekom customer/order data persistence table behavior. |
| `Ekom:VatCalcRounding` | `Rounding`, `AwayFromZero` | VAT rounding mode. |
| `Ekom:VatIncludedPerUnitPolicy` | enum, `PreserveStickerGross` | VAT-included unit pricing policy. |
| `Ekom:VatRoundingScope` | enum, `PerUnit` | `PerUnit` or `PerTotal`. |
| `Ekom:UserBasket` | `bool`, `false` | Uses a member-linked single basket. |
| `Ekom:EmailNotifications` | nullable string | Notification recipient override. |
| `Ekom:DisableStock` | `bool`, `false` | Disables normal stock enforcement. |

`Ekom:ReservationTimeout` is a legacy fallback for reservation timeout. Prefer `Ekom:Reservations:Timeout`.

## Reservations

`StockReservationOptions` binds from `Ekom:Reservations`:

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | `false` | Enables checkout reservation behavior. |
| `Timeout` | `30` | Minutes; also exposed as `Configuration.ReservationTimeout`. |
| `PollInterval` | `00:00:30` | Hosted worker interval; must be greater than zero and no more than one day. |
| `BatchSize` | `100` | Worker batch size, valid range 1-10,000. |
| `WorkerEnabled` | `true` | Enables expiry/cleanup hosted processing. |
| `CompletedRetention` | `7.00:00:00` | Retention for terminal records; cannot be negative. |

```json
{
  "Ekom": {
    "Reservations": {
      "Enabled": true,
      "Timeout": 30,
      "PollInterval": "00:00:30",
      "BatchSize": 100,
      "WorkerEnabled": true,
      "CompletedRetention": "7.00:00:00"
    }
  }
}
```

See [Stock reservations](../guides/reservations.md) for lifecycle and migration behavior.

## Manager permissions

`Ekom:Manager` binds to `ManagerOptions`:

```json
{
  "Ekom": {
    "Manager": {
      "SectionAccessGroup": "ekom,commerce-admins",
      "StoreGroupPermissions": {
        "store": [ "store-editors", "commerce-admins" ]
      }
    }
  }
}
```

`SectionAccessGroup` is a comma-separated string. `StoreGroupPermissions` maps exact store aliases to arrays of Umbraco group aliases. Ekom combines both when deriving recognized manager groups and still checks store access per operation. The legacy `Ekom:SectionAccessRules` setting only affects Umbraco 13 dashboard visibility when current manager groups are empty; it does not grant manager API or store access.

## Variant app

`Ekom:VariantApp:VariantGroups` and `Ekom:VariantApp:Variants` are string arrays containing property aliases on the fixed `ekmProductVariantGroup` and `ekmProductVariant` document types.

## Order-discount integration

`Ekom:OrderDiscountCalculation:ApiKey` is nullable and has no default. Configure it to enable `/ekom/order-discounts/*`; requests send it in `X-Ekom-Api-Key`. If absent/blank, those routes reject every request. Keep this value in secrets/environment configuration.

## Headless revalidation

`Ekom:Headless:ReValidateApis` is an array of:

| Property | Meaning |
| --- | --- |
| `Store` | Store alias whose changes trigger the target. |
| `Url` | Revalidation endpoint. |
| `Secret` | Secret sent by revalidation logic; treat as sensitive. |

`Configuration.HeadlessConfig()` returns null when the section is absent or the array is empty.

## Tracking

`TrackingOptions` binds from `Ekom:Tracking`.

| Key | Default | Behavior |
| --- | --- | --- |
| `Enabled` | `false` | Master tracking switch. |
| `CaptureEnabled` | `true` | Enables automatic attribution capture. |
| `LogEventData` | `false` | Logs all event data; may contain customer data. |
| `LogPurchaseEventData` | `false` | Logs purchase event data only. |
| `CookieName` | `EkomTracking` | Attribution cookie name. |
| `CookieLifetimeDays` | `30` | Cookie lifetime. |
| `SiteBaseUrl` | null | Global absolute site base URL. |
| `Stores` | empty | Per-store `Alias`/`SiteBaseUrl` overrides. |

Consent options under `Tracking:Consent` are `FallbackAnalyticsConsent`, `FallbackMarketingConsent`, optional analytics/marketing cookie and header names, and a `Stores` array with nullable fallback overrides and per-store names. These headers are consent inputs only; they do not authenticate Ekom HTTP endpoints.

`Tracking:Ga4` supports `Enabled`, `Testing`, nullable `UseDebugEndpoint`, nullable `DebugMode`, dispatching options, per-store credentials, and event switches (`AddedToCart`, `RemovedFromCart`, `StartedCheckout`, `AddedShippingInfo`, `AddedPaymentInfo`).

`Tracking:Meta` supports `Enabled`, `Testing`, dispatching options, per-store credentials, and the same event switches. Dispatch defaults are `Capacity: 1000` and `MaxConcurrency: 2`.

Per-store credential entries share this shape: `Alias`, `MeasurementId`, `ApiSecret`, `PixelId`, `AccessToken`, `TestEventCode`. Only fields relevant to the selected provider are used. Keep secrets out of source control and logs.

## Payments

Provider-specific values live beneath `Ekom:Payments`. Their shape is defined by the installed payment-provider package, not by core Ekom. Consult that package's documentation and do not infer a universal credential schema.

## Example

```json
{
  "Ekom": {
    "PerStoreStock": true,
    "GlobalCatalog": false,
    "CategoryRootLevel": 3,
    "BasketCookieLifetime": 7,
    "DefaultProductOrderBy": "DateDesc",
    "VatCalcRounding": "AwayFromZero",
    "VatRoundingScope": "PerUnit",
    "VatIncludedPerUnitPolicy": "PreserveStickerGross",
    "Reservations": {
      "Enabled": true,
      "Timeout": 30
    },
    "OrderDiscountCalculation": {
      "ApiKey": "use-a-secret-provider"
    },
    "Tracking": {
      "Enabled": false,
      "CaptureEnabled": true
    }
  }
}
```

## Related reference

- [Services and DI](services-and-dependency-injection.md)
- [HTTP API](http-api.md)
- [Models and behavior](models-and-behavior.md)

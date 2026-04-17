# Appsettings Reference

All Ekom settings live under the `Ekom` section in `appsettings.json`.

## Example root config

```json
{
  "Ekom": {
    "PerStoreStock": false,
    "ExamineSearchIndex": "ExternalIndex",
    "ShareBasket": false,
    "BasketCookieLifetime": 1,
    "CustomImage": "images",
    "ReservationTimeout": 30,
    "CategoryRootLevel": 3,
    "VatCalcRounding": "AwayFromZero",
    "VatRoundingScope": "PerUnit",
    "VatIncludedPerUnitPolicy": "PreserveStickerGross",
    "ApplyVatOnShipping": true,
    "UserBasket": false,
    "DisableStock": false,
    "AbsoluteUrls": true,
    "DefaultProductOrderBy": "DateDesc",
    "GlobalCatalog": false,
    "EmailNotifications": "orders@example.com",
    "CustomerData": false
  }
}
```

## Core settings

### `PerStoreStock`

Use per-store stock cache instead of product/variant stock.

### `ExamineSearchIndex`

Name of the Examine index used for search.

### `ShareBasket`

Share baskets between stores.

### `BasketCookieLifetime`

Lifetime of the basket cookie in days.

### `CustomImage`

Media folder alias used for product images.

### `ReservationTimeout`

Checkout reservation timeout in minutes.

### `CategoryRootLevel`

Minimum Umbraco level for categories.

### `VatCalcRounding`

VAT rounding strategy.

Supported values include:

- `None`
- `RoundDown`
- `RoundUp`
- `RoundToEven`
- `AwayFromZero`

### `VatRoundingScope`

VAT rounding scope.

Supported values:

- `PerUnit`
- `PerTotal`

### `VatIncludedPerUnitPolicy`

VAT included pricing policy.

Supported values:

- `PreserveStickerGross`
- `LineLevelVat`

### `ApplyVatOnShipping`

Apply VAT to shipping costs.

### `UserBasket`

Use a member-linked basket instead of only cookie-based basket handling.

### `DisableStock`

Disable stock checks.

### `AbsoluteUrls`

Force backoffice URLs to be absolute.

### `DefaultProductOrderBy`

Default product sort order.

### `GlobalCatalog`

If a product is not found in the current store, search other stores.

### `EmailNotifications`

Override email used for mail notifications.

### `CustomerData`

Store customer checkout data in the customer data table.

## Manager settings

Use the `Manager` section to control access to the Ekom manager.

```json
{
  "Ekom": {
    "Manager": {
      "SectionAccessGroup": "ekom",
      "StoreGroupPermissions": {
        "Store": [ "StoreGroup" ],
        "Store2": [ "Store2Group" ]
      }
    }
  }
}
```

### `Manager.SectionAccessGroup`

Groups allowed to access the Ekom manager section.

### `Manager.StoreGroupPermissions`

Per-store permission mapping. Users only get access to stores mapped to their groups.

## Manager access rules

- A user can open the Ekom manager when they belong to `Manager.SectionAccessGroup` or to a group configured in `Manager.StoreGroupPermissions`
- Store access is still checked per store
- Users only see stores where their groups match
- Umbraco administrators bypass these restrictions

## Headless revalidation

Use `Headless.ReValidateApis` to configure revalidation endpoints.

```json
{
  "Ekom": {
    "Headless": {
      "ReValidateApis": [
        {
          "Store": "store1",
          "Url": "https://example.com/api/revalidate",
          "Secret": "secret"
        }
      ]
    }
  }
}
```

Each item contains:

- `Store`
- `Url`
- `Secret`

## Payments settings

Provider-specific payment configuration lives under:

```json
{
  "Ekom": {
    "Payments": {
      "valitor": {
        "merchantId": "1",
        "verificationCode": "xxxxx",
        "merchantName": "",
        "paymentPageUrl": "https://paymentweb.uat.valitor.is/"
      }
    }
  }
}
```

The exact configuration shape depends on the provider you use.

## Tracking settings

Ekom supports order-level consent and tracking data for built-in tracking integrations.

```json
{
  "Ekom": {
    "Tracking": {
      "Enabled": true,
      "CaptureEnabled": true,
      "CookieName": "EkomTracking",
      "CookieLifetimeDays": 30,
      "SiteBaseUrl": "https://www.example.com"
    }
  }
}
```

### Common tracking settings

- `Enabled`
- `CaptureEnabled`
- `CookieName`
- `CookieLifetimeDays`
- `SiteBaseUrl`

### Consent settings

```json
{
  "Ekom": {
    "Tracking": {
      "Consent": {
        "FallbackAnalyticsConsent": false,
        "FallbackMarketingConsent": false,
        "AnalyticsCookieName": "ekom_consent_analytics",
        "AnalyticsHeaderName": "X-Ekom-Consent-Analytics",
        "MarketingCookieName": "ekom_consent_marketing",
        "MarketingHeaderName": "X-Ekom-Consent-Marketing"
      }
    }
  }
}
```

### Store-specific consent override

```json
{
  "Ekom": {
    "Tracking": {
      "Consent": {
        "Stores": [
          {
            "Alias": "Store",
            "AnalyticsCookieName": "cookiehub",
            "MarketingCookieName": "cookiehub"
          }
        ]
      }
    }
  }
}
```

## Algolia configuration

If you use `Ekom.Algolia`, configuration lives under `Ekom.Algolia`.

```json
{
  "Ekom": {
    "Algolia": {
      "Enabled": true,
      "ApplicationId": "APP_ID",
      "AdminApiKey": "ADMIN_API_KEY",
      "SearchApiKey": "SEARCH_API_KEY",
      "InsightsApiKey": "INSIGHTS_API_KEY",
      "AnalyticsRegion": "eu",
      "Environment": "prod"
    }
  }
}
```

Typical groups:

- `Indexing`
- `Search`
- `Events`
- `Stores`

## Klaviyo configuration

If you use `Ekom.Klaviyo`, configuration lives under `Ekom.Klaviyo`.

```json
{
  "Ekom": {
    "Klaviyo": {
      "Enabled": true,
      "PrivateApiKey": "secret",
      "ApiBaseUrl": "https://a.klaviyo.com",
      "Revision": "2026-01-15",
      "ProfileExternalIdProperty": "email",
      "SiteBaseUrl": "https://example.com",
      "Testing": false
    }
  }
}
```

Typical groups:

- `Stores`
- `Orders`
- `Subscriptions`
- `Catalog`
- `Tracking`

## Common pitfalls

## Putting plugin config in the wrong place

`Ekom.Algolia` and `Ekom.Klaviyo` settings belong under the Ekom section used by those integrations in your solution setup.

## Mixing legacy and current manager settings

Prefer `Manager.SectionAccessGroup` and `Manager.StoreGroupPermissions`.

## Forgetting store-specific overrides

Tracking and plugin configuration can behave differently per store.

## Related pages

- [Installation](installation.md)
- [Quick Start](quick-start.md)
- [Tracking Overview](tracking-overview.md)
- [Ekom.Klaviyo](ekom-klaviyo.md)
- [Ekom.Algolia](ekom-algolia.md)

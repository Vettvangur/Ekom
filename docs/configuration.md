# Configuration

All core Ekom settings live under the `Ekom` section in `appsettings.json`.

Use this page as the main guide for configuring Ekom. It covers the core appsettings, what they do, and when you would typically change them.

## Example root configuration

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
    "CustomerData": false,
    "Manager": {
      "SectionAccessGroup": "ekom",
      "StoreGroupPermissions": {
        "Store": [ "StoreGroup" ]
      }
    }
  }
}
```

## Core settings

### `PerStoreStock`

**Default:** `false`

Controls which stock cache Ekom uses. When enabled, stock is handled per store instead of only per product or variant.

Use this when stock should behave differently between stores.

### `ExamineSearchIndex`

**Default:** `ExternalIndex`

Sets the Examine index Ekom uses for search.

Only change this if your solution uses a different index name.

### `ShareBasket`

**Default:** `false`

Allows baskets to be shared between stores.

Use this carefully. Shared baskets work best when the participating stores use compatible currencies and checkout expectations.

### `BasketCookieLifetime`

**Default:** `1`

Controls how many days the order cookie should live.

Increase this if you want carts to persist longer between visits.

### `CustomImage`

**Default:** `images`

Sets the media folder alias Ekom uses for product images.

Change this only if your product image property uses a different alias.

### `ReservationTimeout`

**Default:** `30`

The checkout reservation timeout in minutes.

Use this to control how long a checkout-related reservation should remain active before timing out.

### `CategoryRootLevel`

**Default:** `3`

Sets the minimum Umbraco level for category nodes in the content tree.

Change this if your category structure starts at a different level.

### `VatCalcRounding`

**Default:** `AwayFromZero`

Controls the VAT rounding strategy.

Supported values:

- `None`
- `RoundDown`
- `RoundUp`
- `RoundToEven`
- `AwayFromZero`

### `VatRoundingScope`

**Default:** `PerUnit`

Controls whether VAT rounding happens per unit or on the total.

Supported values:

- `PerUnit`
- `PerTotal`

### `VatIncludedPerUnitPolicy`

**Default:** `PreserveStickerGross`

Controls how VAT-included unit pricing should be handled.

Supported values:

- `PreserveStickerGross`
- `LineLevelVat`

### `ApplyVatOnShipping`

**Default:** `false`

Controls whether VAT is applied to shipping costs by default.

Store-level configuration can override this behavior where relevant.

### `UserBasket`

**Default:** `false`

Uses a member-linked basket instead of only cookie-based basket handling.

Use this when a signed-in member should keep a single basket tied to their account.

### `DisableStock`

**Default:** `false`

Disables stock checks.

This should only be enabled if your solution intentionally does not enforce stock validation.

### `AbsoluteUrls`

**Default:** `false`

Forces backoffice URLs to be absolute.

This can be useful in multi-site or special hosting setups.

### `DefaultProductOrderBy`

**Default:** `DateDesc`

Sets the default product sort order used by Ekom.

Change this when your catalog should use a different default ordering.

### `GlobalCatalog`

**Default:** `false`

If a product is not found in the current store, Ekom will search other stores.

Use this only when cross-store catalog lookup is part of your store setup.

### `EmailNotifications`

**Default:** uses the Umbraco/default mail configuration

Overrides the email address used for mail notifications.

Use this when Ekom notifications should go to a specific inbox.

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

Comma-separated backoffice groups that can access the Ekom manager section.

### `Manager.StoreGroupPermissions`

Maps a store alias to the groups that are allowed to work with that store.

### Manager access behavior

- Users can open the Ekom manager when they belong to `Manager.SectionAccessGroup` or to a group configured under `Manager.StoreGroupPermissions`
- Store access is still checked per store
- Users only see stores where their groups match
- Umbraco administrators bypass these restrictions

## Headless settings

Use `Headless.ReValidateApis` when Ekom should call external revalidation endpoints.

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

Use this when your frontend or external application needs to be revalidated after Ekom changes.

## Tracking settings

Tracking settings live under `Ekom.Tracking`.

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

### `Tracking.Enabled`

**Default:** `false`

Turns Ekom tracking on or off.

### `Tracking.CaptureEnabled`

**Default:** `true`

Controls whether tracking data should be captured automatically.

### `Tracking.CookieName`

**Default:** `EkomTracking`

Sets the cookie name used for Ekom tracking.

### `Tracking.CookieLifetimeDays`

**Default:** `30`

Controls how long the tracking cookie should live.

### `Tracking.SiteBaseUrl`

Base URL used for tracking-related behavior where needed.

## Tracking consent settings

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

### `Tracking.Consent.FallbackAnalyticsConsent`

Fallback analytics consent when no resolver or incoming value supplies one.

### `Tracking.Consent.FallbackMarketingConsent`

Fallback marketing consent when no resolver or incoming value supplies one.

### `Tracking.Consent.AnalyticsCookieName`

Cookie name used to resolve analytics consent.

### `Tracking.Consent.AnalyticsHeaderName`

Header name used to resolve analytics consent.

### `Tracking.Consent.MarketingCookieName`

Cookie name used to resolve marketing consent.

### `Tracking.Consent.MarketingHeaderName`

Header name used to resolve marketing consent.

### Store-specific consent overrides

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

Use `Tracking.Consent.Stores` when consent behavior should vary by store.

Each store item supports:

- `Alias`
- `FallbackAnalyticsConsent`
- `FallbackMarketingConsent`
- `AnalyticsCookieName`
- `AnalyticsHeaderName`
- `MarketingCookieName`
- `MarketingHeaderName`

## Tracking provider settings

Ekom supports provider-specific tracking configuration for GA4 and Meta.

```json
{
  "Ekom": {
    "Tracking": {
      "Ga4": {
        "Enabled": true,
        "Testing": false,
        "Dispatching": {
          "Capacity": 1000,
          "MaxConcurrency": 2
        },
        "Stores": [
          {
            "Alias": "store1",
            "MeasurementId": "G-XXXXXXX",
            "ApiSecret": "secret",
            "TestEventCode": "test-code"
          }
        ]
      },
      "Meta": {
        "Enabled": true,
        "Testing": false,
        "Dispatching": {
          "Capacity": 1000,
          "MaxConcurrency": 2
        },
        "Stores": [
          {
            "Alias": "store1",
            "PixelId": "123456789",
            "AccessToken": "token",
            "TestEventCode": "test-code"
          }
        ]
      }
    }
  }
}
```

Both `Tracking.Ga4` and `Tracking.Meta` support:

- `Enabled`
- `Testing`
- `Dispatching.Capacity`
- `Dispatching.MaxConcurrency`
- `Stores`

`Stores` is configured per store alias.

GA4 store items support:

- `Alias`
- `MeasurementId`
- `ApiSecret`
- `TestEventCode`

Meta store items support:

- `Alias`
- `PixelId`
- `AccessToken`
- `TestEventCode`

## Payments settings

Provider-specific payment configuration lives under `Ekom.Payments`.

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

## Plugin configuration

Plugin-specific configuration lives under its own section inside `Ekom`, for example:

- `Ekom.Algolia`
- `Ekom.Klaviyo`

Keep the full configuration details for those integrations on their own plugin pages.

## Common mistakes

## Putting settings in the wrong section

All core Ekom settings should live under `Ekom`. Nested features should stay under their matching subsection such as `Ekom.Manager`, `Ekom.Headless`, `Ekom.Tracking`, or `Ekom.Payments`.

## Forgetting store-specific overrides

Tracking and permissions can behave differently per store. Use store-specific configuration when your stores do not share the same rules.


## Related pages

- [Installation](installation.md)
- [Tracking Overview](tracking-overview.md)
- [CookieHub Integration](cookiehub-integration.md)
- [Ekom.Klaviyo](ekom-klaviyo.md)
- [Ekom.Algolia](ekom-algolia.md)

# Tracking Overview

Ekom supports order-level tracking and consent data so orders can carry the information needed for analytics and marketing dispatch.

This page focuses on the developer view of tracking:

- how tracking data gets onto an order
- how consent is resolved
- where tracking is configured
- how tracking is used later in integrations such as GA4 and Meta

## What tracking means in Ekom

Ekom stores tracking data on the order itself.

That means an order can carry information such as:

- landing URL
- referrer URL
- click identifiers
- GA4-related values
- Meta-related values
- consent state

This allows Ekom to make tracking decisions based on the actual order rather than only on the current request.

## Tracking goals

The built-in tracking flow is designed to support:

- order-level consent-aware tracking
- purchase event dispatching for integrations
- consistent tracking data across checkout and completion
- store-specific consent handling

## Tracking configuration

Tracking is configured under:

```json
"Ekom": {
  "Tracking": {
    "Enabled": true,
    "CaptureEnabled": true,
    "CookieName": "EkomTracking",
    "CookieLifetimeDays": 30,
    "SiteBaseUrl": "https://www.example.com"
  }
}
```

### Important settings

- `Enabled`: enables tracking features
- `CaptureEnabled`: enables request capture into Ekom tracking data
- `CookieName`: name of the Ekom tracking cookie
- `CookieLifetimeDays`: cookie lifetime
- `SiteBaseUrl`: fallback base URL when no landing URL can be resolved from the request

## Consent configuration

Consent is configured under:

```json
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
```

### Store-specific overrides

Consent handling can also be overridden per store:

```json
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
```

This is especially useful when different stores use different consent platforms.

## How consent is resolved

Consent is resolved through a chain of `ITrackingConsentResolver` services.

### Resolution rules

- resolvers run in order
- the first resolver that returns a value wins
- if none resolve, Ekom falls back to configured fallback values

This allows you to customize consent resolution without changing core checkout code.

## CookieHub support

Ekom has built-in support for CookieHub.

If a store points the analytics and/or marketing cookie name to `cookiehub`, Ekom will:

- read the CookieHub cookie
- URL-decode the payload
- parse the JSON data
- map CookieHub categories to `OrderConsent`

### Mapping

- `categories.analytics` → `OrderConsent.Analytics`
- `categories.marketing` → `OrderConsent.Marketing`

### Example CookieHub payload

```json
{
  "categories": {
    "necessary": true,
    "preferences": false,
    "analytics": true,
    "marketing": false
  },
  "level": "full",
  "revision": 1,
  "timestamp": "2026-04-03T12:00:00Z"
}
```

## How tracking gets onto an order

Tracking can reach an order in multiple ways.

## 1. Automatic request capture

When tracking capture is enabled, Ekom can capture tracking data from the incoming request and store it in its tracking cookie and order flow.

The built-in tracking cookie service is responsible for reading, writing, and capturing tracking values.

## 2. Manual tracking update through `API.Order`

You can add or replace tracking manually through `API.Order.UpdateTrackingAsync(...)`.

```csharp
IOrderInfo order = await _order.UpdateTrackingAsync(
    "Store",
    new OrderTracking
    {
        LandingUrl = "https://example.com/products/shoe",
        ReferrerUrl = "https://google.com"
    },
    ct: ct);
```

This is useful when:

- you are building a custom frontend
- you want to post explicit tracking data into Ekom
- you need to preserve tracking across non-standard flows

## 3. Tracking in add-to-order and customer update flows

Tracking can also be supplied as part of order update flows.

Examples include:

- add-to-order requests
- update-customer requests
- dedicated tracking update requests

This allows a headless frontend to keep tracking and consent aligned with the active order.

## Updating tracking through HTTP

Ekom exposes a dedicated public endpoint:

```text
POST /ekom/order/updatetracking
```

### Example request

```http
POST /ekom/order/updatetracking
Content-Type: application/json

{
  "storeAlias": "Store",
  "tracking": {
    "landingUrl": "https://example.com/products/shoe",
    "referrerUrl": "https://google.com"
  },
  "consent": {
    "analytics": true,
    "marketing": false
  }
}
```

### Response

- `200 OK` with the updated order
- `400 Bad Request` if `storeAlias` or `tracking` is missing

## Tracking model

Tracking data is represented by `OrderTracking`.

That model also contains nested provider-specific tracking sections such as:

- `Ga4`
- `Meta`

This lets Ekom carry both general tracking information and provider-specific details on the same order.

## How tracking is used later

Tracking is not only captured for its own sake.

It is later used when Ekom or integrations need to make decisions such as:

- whether analytics dispatch is allowed
- whether marketing dispatch is allowed
- what landing/referrer/click data should be attached to a purchase event

## GA4 and Meta dispatching

Ekom includes built-in support for background dispatching to:

- GA4
- Meta

These integrations are configured under:

- `Ekom:Tracking:Ga4`
- `Ekom:Tracking:Meta`

### Example structure

```json
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
        "Alias": "Store",
        "MeasurementId": "G-XXXXXXXXXX",
        "ApiSecret": "your-ga4-api-secret"
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
        "Alias": "Store",
        "PixelId": "123456789012345",
        "AccessToken": "your-meta-access-token",
        "TestEventCode": "TEST12345"
      }
    ]
  }
}
```

### Notes

- GA4 uses `MeasurementId` and `ApiSecret`
- Meta uses `PixelId` and `AccessToken`
- both use background dispatchers
- testing mode is supported for both

## Tracking and checkout

Tracking becomes especially important during checkout completion.

At that point, stored order consent and tracking data can influence whether purchase-related provider events should be dispatched.

This is why it is useful to attach tracking to the order before completion rather than trying to reconstruct it later.

## Minimal server-side example

```csharp
IOrderInfo? currentOrder = await _order.GetOrderAsync("Store", ct);

if (currentOrder == null)
{
    throw new InvalidOperationException("No active order found.");
}

IOrderInfo updatedOrder = await _order.UpdateTrackingAsync(
    "Store",
    new OrderTracking
    {
        LandingUrl = "https://example.com/products/shoe",
        ReferrerUrl = "https://google.com"
    },
    ct: ct);
```

## Common pitfalls

### Assuming tracking is only request-based

In Ekom, tracking is order-level as well, not just request-level.

### Forgetting consent

Tracking data without correct consent handling can lead to dispatch behavior that does not match your intended rules.

### Ignoring store-specific overrides

Multi-store setups can have different consent rules and cookie names.

### Trying to reconstruct tracking only after completion

It is usually better to attach tracking data during the order lifecycle, not after checkout is done.

## Related pages

- [Configuration](configuration.md)
- [Order Lifecycle](order-lifecycle.md)
- [Checkout Flow](checkout-flow.md)
- [Order Endpoints](order-endpoints.md)

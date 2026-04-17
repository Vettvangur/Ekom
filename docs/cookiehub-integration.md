# CookieHub Integration

This page explains how to use CookieHub with Ekom’s built-in tracking consent resolution.

It is aimed at developers who want CookieHub consent to control whether analytics and marketing tracking is allowed on orders.

## What the integration does

Ekom includes a built-in CookieHub consent resolver.

When a store is configured to use CookieHub, Ekom will:

- read the `cookiehub` cookie
- URL-decode the cookie value
- parse the JSON payload
- map CookieHub categories to `OrderConsent`

That consent can then be stored on the order and used later by Ekom tracking and purchase dispatch flows.

## What you do not need to build

You do **not** need to:

- register your own CookieHub resolver in startup
- change the core Ekom config shape
- write custom parsing logic just to support the standard CookieHub cookie payload

The built-in resolver is already part of Ekom.

## Built-in resolver

The built-in resolver is:

```csharp
public sealed class CookieHubTrackingConsentResolver : ITrackingConsentResolver
```

It lives in:

```text
Ekom/Ekom/Tracking/Resolvers/CookieHubTrackingConsentResolver.cs
```

### Registration behavior

The resolver is registered automatically by Ekom.

No custom per-site registration is required for the normal CookieHub scenario.

## How CookieHub is activated

CookieHub is not turned on through a separate feature flag.

Instead, it is activated by consent configuration.

If a store points the analytics and/or marketing cookie name to `cookiehub`, the built-in resolver treats that store as CookieHub-enabled.

### Example appsettings

```json
"Tracking": {
  "Consent": {
    "FallbackAnalyticsConsent": false,
    "FallbackMarketingConsent": false,
    "Stores": [
      {
        "Alias": "Store",
        "AnalyticsCookieName": "cookiehub",
        "MarketingCookieName": "cookiehub"
      }
    ]
  }
}
```

This keeps the normal Ekom tracking config model intact while enabling CookieHub for the selected store.

## Consent mapping

The built-in resolver maps CookieHub categories like this:

- `categories.analytics` → `OrderConsent.Analytics`
- `categories.marketing` → `OrderConsent.Marketing`

It also tries to read the CookieHub timestamp and stores:

- `ResolvedAtUtc`
- `Source = "cookiehub"`

## Expected CookieHub payload

After URL decoding, Ekom expects a cookie payload shaped like this:

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

The most important fields for Ekom are under `categories`.

## How Ekom uses CookieHub consent

Once consent is resolved, Ekom can use it in several places.

### Request and middleware capture

During request capture, Ekom can resolve consent before reading or applying tracking values.

### Order updates

On later order requests, Ekom can resolve consent again and keep the stored order consent aligned with the current request context.

### Checkout completion and provider dispatch

Completed checkout flows can use stored order consent to decide whether analytics or marketing provider dispatch should happen.

This is why CookieHub integration is not just a UI concern. It affects how order tracking behaves later in the lifecycle.

## Fallback behavior

If the CookieHub resolver cannot resolve consent, Ekom continues through the normal resolver chain.

That means it can still fall back to:

- the default cookie/header resolver
- `FallbackAnalyticsConsent`
- `FallbackMarketingConsent`

This makes the integration resilient even when the CookieHub cookie is missing or malformed.

## What happens when the cookie is missing

If the `cookiehub` cookie is not present, the resolver returns `null` and Ekom continues with the remaining consent resolution flow.

## What happens when the cookie is invalid

If the CookieHub cookie exists but contains invalid JSON, the resolver also returns `null`.

That means:

- no exception is thrown from the resolver
- Ekom continues through the resolver chain
- fallback consent rules can still apply

## Minimal configuration example

This is the smallest useful store-specific CookieHub setup.

```json
{
  "Ekom": {
    "Tracking": {
      "Enabled": true,
      "CaptureEnabled": true,
      "Consent": {
        "FallbackAnalyticsConsent": false,
        "FallbackMarketingConsent": false,
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

## Multi-store behavior

CookieHub can be enabled for one store without forcing the same behavior on every store.

This is useful when:

- one store uses CookieHub
- another store uses a different consent solution
- some stores rely on header/cookie fallback rules only

## How to verify it works

A practical developer verification flow is:

1. configure a store to use `cookiehub`
2. load the site with a valid CookieHub cookie present
3. update the order or tracking state
4. inspect the stored order consent/tracking behavior
5. verify that downstream analytics/marketing behavior follows the resolved consent

## Common pitfalls

### Forgetting the store override

CookieHub is activated by the consent cookie-name settings, not by a separate CookieHub switch.

### Expecting CookieHub to work without tracking enabled

Consent resolution matters most when tracking features are enabled and used in the order flow.

### Assuming malformed cookie data will throw visibly

The resolver returns `null` for invalid CookieHub JSON and lets the rest of the resolver chain continue.

### Applying CookieHub to every store unintentionally

Use store-specific overrides carefully in multi-store setups.

## Related pages

- [Tracking Overview](tracking-overview.md)
- [Appsettings Reference](appsettings-reference.md)
- [Order Lifecycle](order-lifecycle.md)
- [Checkout Flow Overview](checkout-flow-overview.md)

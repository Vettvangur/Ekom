# CookieHub Consent Resolver

This sample shows how to plug CookieHub into Ekom's consent resolver chain without changing Ekom core config shape.

## What It Does

- Registers a custom `ITrackingConsentResolver`
- Reads the `cookiehub` cookie
- URL-decodes and parses the JSON payload
- Maps CookieHub categories to Ekom order consent:
  - `categories.analytics` -> `OrderConsent.Analytics`
  - `categories.marketing` -> `OrderConsent.Marketing`

## Sample Registration

Register the resolver in `Startup.cs`:

```csharp
services.AddSingleton<ITrackingConsentResolver, CookieHubTrackingConsentResolver>();
```

## Sample Resolver

The sample implementation lives in `Samples/U10/Ekom.Site/CookieHubTrackingConsentResolver.cs`.

Core contract:

```csharp
public sealed class CookieHubTrackingConsentResolver : ITrackingConsentResolver
{
    public int Order => 100;

    public OrderConsent? Resolve(HttpContext httpContext, string? storeAlias, TrackingConsentOptions options)
    {
        // parse CookieHub cookie and return OrderConsent
    }
}
```

Lower `Order` values run first in the resolver chain.

## Sample Appsettings

Use normal Ekom consent config, but point the relevant store to the CookieHub cookie:

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

This keeps the default config model intact while letting the custom resolver interpret `cookiehub` specially for that store.

## CookieHub Payload Shape

Expected cookie content after URL decoding:

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

## How Ekom Uses It

- Middleware capture resolves store-specific consent before reading GA4/Meta cookies
- Order updates resolve consent again on the next Ekom request for that order
- Completed checkout only dispatches provider events if stored order consent allows it

## Fallback Behavior

If the CookieHub resolver returns `null`, Ekom continues through the resolver chain and then falls back to:

- simple cookie/header boolean resolver
- `FallbackAnalyticsConsent`
- `FallbackMarketingConsent`

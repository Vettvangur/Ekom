# Tracking and consent

Ekom can capture attribution into an `EkomTracking` cookie, persist it with the basket/order, and dispatch consent-aware GA4 and Meta events. The optional Mailchimp plugin consumes the order's Mailchimp attribution for completed-purchase tracking.

Tracking is disabled by default. Consent fallbacks also default to `false`.

## Configuration

```json
{
  "Ekom": {
    "Tracking": {
      "Enabled": true,
      "CaptureEnabled": true,
      "LogEventData": false,
      "LogPurchaseEventData": false,
      "CookieName": "EkomTracking",
      "CookieLifetimeDays": 30,
      "SiteBaseUrl": "https://www.example.com",
      "Stores": [
        {
          "Alias": "Store",
          "SiteBaseUrl": "https://store.example.com"
        }
      ],
      "Consent": {
        "FallbackAnalyticsConsent": false,
        "FallbackMarketingConsent": false,
        "AnalyticsCookieName": "ekom_consent_analytics",
        "AnalyticsHeaderName": "X-Ekom-Consent-Analytics",
        "MarketingCookieName": "ekom_consent_marketing",
        "MarketingHeaderName": "X-Ekom-Consent-Marketing"
      },
      "Ga4": {
        "Enabled": true,
        "UseDebugEndpoint": false,
        "DebugMode": false,
        "Events": {
          "AddedToCart": false,
          "RemovedFromCart": false,
          "StartedCheckout": false,
          "AddedShippingInfo": false,
          "AddedPaymentInfo": false
        },
        "Dispatching": {
          "Capacity": 1000,
          "MaxConcurrency": 2
        },
        "Stores": [
          {
            "Alias": "Store",
            "MeasurementId": "G-XXXXXXXXXX",
            "ApiSecret": "secret"
          }
        ]
      },
      "Meta": {
        "Enabled": true,
        "Testing": false,
        "Events": {
          "AddedToCart": false,
          "RemovedFromCart": false,
          "StartedCheckout": false,
          "AddedShippingInfo": false,
          "AddedPaymentInfo": false
        },
        "Dispatching": {
          "Capacity": 1000,
          "MaxConcurrency": 2
        },
        "Stores": [
          {
            "Alias": "Store",
            "PixelId": "123456789012345",
            "AccessToken": "token",
            "TestEventCode": "TEST12345"
          }
        ]
      }
    }
  }
}
```

`LogEventData` logs every outbound GA4/Meta payload. `LogPurchaseEventData` logs only event name `purchase`; Meta's event is named `Purchase`, which is matched case-insensitively. These payloads can contain customer or attribution data, so enable logging only with an appropriate data policy.

The dispatchers use bounded in-memory channels with `Capacity`, dropping the oldest queued request when full. Current dispatchers have one channel reader each; `MaxConcurrency` exists in options but is not currently used to create parallel consumers. Queued events are not durable across process failure.

## Consent resolution

`ITrackingConsentResolver` implementations run in ascending `Order`; the first non-null result wins. Missing analytics or marketing values on that result are filled from configured fallbacks. If no resolver resolves anything, both fallback values are returned with source `fallback`.

The default resolver reads the configured header first, then cookie, and accepts:

- `true`, `1`, `yes`, `granted`
- `false`, `0`, `no`, `denied`

Store entries under `Tracking:Consent:Stores` override fallback values and cookie/header names by case-insensitive store alias.

Custom resolvers are ordinary DI services:

```csharp
public sealed class SiteConsentResolver : ITrackingConsentResolver
{
    public int Order => 50;

    public OrderConsent? Resolve(
        HttpContext context,
        string? storeAlias,
        TrackingConsentOptions options)
    {
        var analyticsClaim = context.User.FindFirst("analytics_consent")?.Value;
        if (!bool.TryParse(analyticsClaim, out var analyticsConsent))
            return null;

        return new OrderConsent
        {
            Analytics = analyticsConsent,
            Source = "site",
        };
    }
}

services.AddSingleton<ITrackingConsentResolver, SiteConsentResolver>();
```

Choose an order below `100` to run before CookieHub and below `1000` to run before the default resolver. The example assumes that `analytics_consent` records an explicit user preference. Authentication by itself is not consent; only map a claim, cookie, or preference that your site has collected and is authorized to use.

## CookieHub

CookieHub support is registered by Ekom. Activate it for a store by setting either configured consent cookie name to `cookiehub`:

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

The resolver reads the actual `cookiehub` cookie. It accepts URL-decoded JSON, standard Base64, or URL-safe Base64. Supported payloads include named categories:

```json
{
  "categories": {
    "analytics": true,
    "marketing": false
  },
  "timestamp": "2026-09-17T12:00:00Z"
}
```

It also accepts a numeric category array, where `3` is analytics and `4` is marketing, and treats `allAllowed: true` as consent for both. Invalid or missing data returns null and allows later resolvers/fallbacks to run.

## Automatic browser capture

When `Enabled` and `CaptureEnabled` are true, `EkomTrackingMiddleware` considers `GET` and `HEAD` requests that:

- are not under `/umbraco`, `/ekom`, `/api`, or `/webapi`
- do not have a file extension
- produce a `text/html` response before the cookie is written

It captures UTM values, `gclid`/`fbclid`, landing URL, referrer, GA `_ga` client/session IDs, Meta `_fbp`/`_fbc`, and Mailchimp `mc_cid`/supported `mc_tc`. The tracking cookie is path `/`, `SameSite=Lax`, non-HttpOnly, secure on HTTPS, and expires after `CookieLifetimeDays`.

Analytics consent controls GA identifiers. Marketing consent controls Meta identifiers and Mailchimp attribution. Before either consent is granted, general landing/UTM and Mailchimp first-touch attribution can be retained in the pre-consent session; eligible values are promoted to the tracking cookie when consent later permits capture.

## Mailchimp first-touch attribution

Ekom recognizes `mc_cid` and only the exact tracking code `mc_tc=prec`.

- Before marketing consent, first-touch Mailchimp values are kept in the pre-consent session rather than `EkomTracking`.
- When marketing consent is granted, the first-touch values are promoted into the tracking cookie and can be persisted with the basket/order.
- When marketing consent is withdrawn, browser middleware moves Mailchimp values back to the pre-consent session and removes them from the tracking cookie. The next unfinished-order update carrying that consent removes them from the order too.
- `OrderTrackingService` strips Mailchimp attribution whenever order marketing consent is not true.
- The Mailchimp order mapper sends attribution only with marketing consent. It prefers `OrderTracking.Mailchimp`, then legacy customer properties `mc_cid`/`mc_tc`; unsupported tracking codes are discarded.

The Mailchimp plugin must be separately installed, configured under `Ekom:Mailchimp`, and registered with `builder.Services.AddMailchimp()`. Automatic completed-checkout tracking additionally requires `Enabled`, `Purchases:Enabled`, and `Purchases:TrackCompletedCheckouts`.

## Persist tracking on an order

Order updates automatically resolve cookie tracking/consent when no explicit values are supplied. A headless client can submit explicit values with add-to-order, customer update, or the dedicated endpoint:

```http
POST /ekom/order/updatetracking
Content-Type: application/json

{
  "storeAlias": "Store",
  "consent": {
    "analytics": true,
    "marketing": false
  },
  "tracking": {
    "source": "newsletter",
    "medium": "email",
    "campaign": "autumn",
    "landingUrl": "https://example.com/shop",
    "ga4": {
      "clientId": "123.456",
      "sessionId": "789"
    }
  }
}
```

The C# equivalent is `Order.UpdateTrackingAsync(storeAlias, tracking, new OrderSettings { Consent = consent }, ct)`. Manual tracking replaces existing tracking and is rejected for a final order. Do not trust consent or customer identifiers posted by an untrusted client without applying the site's consent policy.

## GA4 behavior

GA4 sends Measurement Protocol events only with analytics consent and matching store credentials. Purchase events are automatic whenever GA4 tracking is enabled; optional lifecycle events default to false:

| Ekom trigger | GA4 event |
| --- | --- |
| Order line added | `add_to_cart` |
| Order line removed | `remove_from_cart` |
| First customer email added | `begin_checkout` |
| Shipping provider selected | `add_shipping_info` |
| Payment provider selected | `add_payment_info` |
| Checkout completed | `purchase` |

Item `price` is VAT-exclusive before line discount; `discount` is the VAT-exclusive per-unit reduction. `value` is the discounted item total, while purchase `tax` and shipping are separate. Payment fees are not added to event value. Amounts use the store currency's decimal precision. Coupons are attached to applicable items and to the purchase when appropriate. Captured UTM values map to GA campaign parameters.

`UseDebugEndpoint` selects `debug/mp/collect` and validates its response. `DebugMode` adds `debug_mode: true`. If either nullable setting is omitted, legacy `Testing` supplies its value.

## Meta behavior

Meta sends Conversions API events only with marketing consent, matching store credentials, and at least one matchable value: email, phone, first/last name, `_fbp`, `_fbc`, or custom user data. Customer fields are normalized and SHA-256 hashed; browser IDs are sent in their native normalized form.

The optional lifecycle events map to `AddToCart`, `RemoveFromCart`, `InitiateCheckout`, `AddShippingInfo`, and `AddPaymentInfo`; completed checkout sends `Purchase`. `event_source_url` uses the captured landing URL, then store-specific `SiteBaseUrl`, then global `SiteBaseUrl`, with available campaign/click query values. `Testing` includes the store's `TestEventCode`; without a code the event is sent normally with a warning.

## Customize purchase dispatch

`TrackingEvents.Ga4PurchasePreparingAsync` and `MetaPurchasePreparingAsync` run after request creation and before queueing. Handlers can replace `Request`, add data to its dictionaries, or set `Cancel`.

```csharp
TrackingEvents.Ga4PurchasePreparingAsync += (sender, args, ct) =>
{
    args.Request.Parameters["customer_type"] = "member";
    return Task.CompletedTask;
};
```

These hooks currently apply to purchase events, not the optional cart/checkout lifecycle events. Unsubscribe static handlers at shutdown.

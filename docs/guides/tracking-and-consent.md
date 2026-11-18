# Tracking and consent

[Ekom docs](../README.md) / [Guides](README.md) / Tracking and consent

Ekom can record where a customer came from (campaign values, click IDs, analytics IDs), save it with the basket and the order, and send purchase events from the server to Google Analytics 4 (GA4) and Meta. It follows the customer's consent for analytics and for marketing. Use it when you want server-side purchase tracking that respects your cookie banner.

> **Tracking is off by default.** Nothing is captured and nothing is sent until you set `Ekom:Tracking:Enabled` to `true`. GA4 and Meta each also need their own `Enabled` setting and store credentials. When Ekom cannot find the customer's consent, it assumes **no** consent: both fallback values are `false` by default.

On this page:

- [Turn tracking on](#turn-tracking-on)
- [All tracking settings](#all-tracking-settings)
- [How Ekom finds the customer's consent](#how-ekom-finds-the-customers-consent)
- [Read consent from CookieHub](#read-consent-from-cookiehub)
- [What Ekom captures from the browser](#what-ekom-captures-from-the-browser)
- [Mailchimp first-touch attribution](#mailchimp-first-touch-attribution)
- [Save tracking on an order](#save-tracking-on-an-order)
- [Events sent to GA4](#events-sent-to-ga4)
- [Events sent to Meta](#events-sent-to-meta)
- [Change a purchase event before it is sent](#change-a-purchase-event-before-it-is-sent)
- [Delivery, logging and failures](#delivery-logging-and-failures)

## Turn tracking on

1. Decide where Ekom reads consent. Set the cookie or header names your cookie banner writes, or use [CookieHub](#read-consent-from-cookiehub), or add [your own resolver](#add-your-own-consent-resolver).
2. Turn on tracking and the providers you use, and add the credentials for each store.
3. Restart the site.
4. Complete a test order with consent given, and check the order's activity log for "GA4 purchase event successfully sent" or "Meta Purchase event sent".

A minimal setup for one store, with GA4 and Meta, and consent read from your own cookies:

```json
{
  "Ekom": {
    "Tracking": {
      "Enabled": true,
      "Consent": {
        "AnalyticsCookieName": "site_consent_analytics",
        "MarketingCookieName": "site_consent_marketing"
      },
      "Ga4": {
        "Enabled": true,
        "Stores": [
          { "Alias": "Store", "MeasurementId": "G-XXXXXXXXXX", "ApiSecret": "your-api-secret" }
        ]
      },
      "Meta": {
        "Enabled": true,
        "Stores": [
          { "Alias": "Store", "PixelId": "123456789012345", "AccessToken": "your-access-token" }
        ]
      }
    }
  }
}
```

Keep `ApiSecret` and `AccessToken` out of source control, for example in environment variables or a secret store.

## All tracking settings

All keys are under `Ekom:Tracking`.

### General

| Setting | Default | Effect |
| --- | --- | --- |
| `Enabled` | `false` | Turns tracking on. While `false`, Ekom captures nothing and sends no events. |
| `CaptureEnabled` | `true` | Lets Ekom capture tracking from browser requests into the tracking cookie, and copy the cookie onto baskets. |
| `CookieName` | `EkomTracking` | The name of the tracking cookie. |
| `CookieLifetimeDays` | `30` | How long the tracking cookie lasts, in days. |
| `SiteBaseUrl` | none | The site URL used for Meta's `event_source_url` when no landing URL was captured. |
| `Stores` | empty | Per store: `Alias` and `SiteBaseUrl`. A store's value wins over the general one. |
| `LogEventData` | `false` | Logs every GA4 and Meta payload Ekom sends, at Information level. |
| `LogPurchaseEventData` | `false` | Logs only purchase payloads. Matches `purchase` in any case, so it also covers Meta's `Purchase`. |

Payloads can hold customer and attribution data. Turn logging on only when your data policy allows it.

### Consent (`Ekom:Tracking:Consent`)

| Setting | Default | Effect |
| --- | --- | --- |
| `FallbackAnalyticsConsent` | `false` | The analytics consent Ekom uses when it finds none. |
| `FallbackMarketingConsent` | `false` | The marketing consent Ekom uses when it finds none. |
| `AnalyticsHeaderName` | none | A request header that holds analytics consent. |
| `AnalyticsCookieName` | none | A cookie that holds analytics consent. Set to `cookiehub` to use CookieHub. |
| `MarketingHeaderName` | none | A request header that holds marketing consent. |
| `MarketingCookieName` | none | A cookie that holds marketing consent. Set to `cookiehub` to use CookieHub. |
| `Stores` | empty | Per store: `Alias` plus any of the six settings above. A store value replaces the general value for that store. Aliases are matched without regard to case. |

There are no default cookie or header names. Until you set one, or use CookieHub or your own resolver, every customer gets the fallback values.

### GA4 (`Ekom:Tracking:Ga4`)

| Setting | Default | Effect |
| --- | --- | --- |
| `Enabled` | `false` | Sends GA4 events. The purchase event is always sent while this is on. |
| `Events:AddedToCart` | `false` | Sends `add_to_cart`. |
| `Events:RemovedFromCart` | `false` | Sends `remove_from_cart`. |
| `Events:StartedCheckout` | `false` | Sends `begin_checkout`. |
| `Events:AddedShippingInfo` | `false` | Sends `add_shipping_info`. |
| `Events:AddedPaymentInfo` | `false` | Sends `add_payment_info`. |
| `UseDebugEndpoint` | not set | `true` sends to GA4's `debug/mp/collect` and checks the validation messages it returns. Not set: uses `Testing`. |
| `DebugMode` | not set | `true` adds `debug_mode: true` to each event. Not set: uses `Testing`. |
| `Testing` | `false` | The value for `UseDebugEndpoint` and `DebugMode` when they are not set. |
| `Dispatching:Capacity` | `1000` | How many events can wait in the send queue. |
| `Dispatching:MaxConcurrency` | `2` | Not used. Events are sent one at a time. |
| `Stores` | empty | Per store: `Alias`, `MeasurementId`, `ApiSecret`. |

### Meta (`Ekom:Tracking:Meta`)

| Setting | Default | Effect |
| --- | --- | --- |
| `Enabled` | `false` | Sends Meta Conversions API events. The purchase event is always sent while this is on. |
| `Events:AddedToCart`, `Events:RemovedFromCart`, `Events:StartedCheckout`, `Events:AddedShippingInfo`, `Events:AddedPaymentInfo` | `false` | Turn on the matching extra events. |
| `Testing` | `false` | Sends the store's `TestEventCode` with each event, so it shows in Meta's test events. Without a code, the event is sent as a normal event and Ekom logs a warning. |
| `Dispatching:Capacity` | `1000` | How many events can wait in the send queue. |
| `Dispatching:MaxConcurrency` | `2` | Not used. Events are sent one at a time. |
| `Stores` | empty | Per store: `Alias`, `PixelId`, `AccessToken`, `TestEventCode`. |

## How Ekom finds the customer's consent

Consent has two parts: analytics and marketing. Each is `true`, `false` or unknown.

Ekom asks a list of consent resolvers, in order of their `Order` value, lowest first. The first resolver that returns a result wins. If that result leaves analytics or marketing unknown, Ekom fills it in from the fallback value. If no resolver returns a result, Ekom uses both fallback values, with source `fallback`.

Ekom registers two resolvers:

| Resolver | `Order` | Reads |
| --- | --- | --- |
| `CookieHubTrackingConsentResolver` | `100` | The `cookiehub` cookie, when a consent cookie name is set to `cookiehub`. |
| `DefaultTrackingConsentResolver` | `1000` | The configured header first, then the configured cookie. |

The default resolver accepts these values, in any case:

- consent given: `true`, `1`, `yes`, `granted`
- consent refused: `false`, `0`, `no`, `denied`

Any other value counts as unknown.

### Add your own consent resolver

Implement `ITrackingConsentResolver` and register it as a service. This example reads analytics consent from a claim that your site sets when the user saves their cookie choice.

```csharp
using Ekom.Models;
using Ekom.Tracking;
using Microsoft.AspNetCore.Http;

public sealed class ClaimConsentResolver : ITrackingConsentResolver
{
    public int Order => 50;

    public OrderConsent? Resolve(
        HttpContext context,
        string? storeAlias,
        TrackingConsentOptions options)
    {
        string? claim = context.User.FindFirst("analytics_consent")?.Value;
        if (!bool.TryParse(claim, out bool analytics))
        {
            return null;
        }

        return new OrderConsent
        {
            Analytics = analytics,
            Source = "site",
        };
    }
}
```

```csharp
builder.Services.AddSingleton<ITrackingConsentResolver, ClaimConsentResolver>();
```

- Use an `Order` below `100` to run before CookieHub, or below `1000` to run before the default resolver.
- Return `null` when you do not know. Ekom then asks the next resolver.
- In the example, marketing is left unknown, so Ekom fills it from `FallbackMarketingConsent`.
- Map only a value that records the customer's own choice. Being logged in is not consent.

## Read consent from CookieHub

Set either consent cookie name to `cookiehub`, for all stores or for one store:

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

The CookieHub resolver then reads the cookie named `cookiehub`. It accepts the value as URL-encoded JSON, as standard Base64, or as URL-safe Base64. It understands three forms:

- Named categories: `{ "categories": { "analytics": true, "marketing": false } }`.
- A list of category numbers: `3` in the list means analytics consent, `4` means marketing consent. A number that is not in the list means no consent.
- `"allAllowed": true` means consent for both.

A `timestamp` in the cookie is saved as the time of consent. If the cookie is missing or cannot be read, the resolver returns nothing and Ekom asks the next resolver.

## What Ekom captures from the browser

The tracking middleware is added for you. It runs only when `Enabled` and `CaptureEnabled` are both `true`, and only for requests that:

- use `GET` or `HEAD`,
- are not under `/umbraco`, `/ekom`, `/api` or `/webapi`,
- have no file extension in the path,
- get a `text/html` response.

From such a request, Ekom reads:

| Value | Source | Needs |
| --- | --- | --- |
| Campaign values | `utm_source`, `utm_medium`, `utm_campaign`, `utm_term`, `utm_content` | Analytics or marketing consent |
| Click ID | `gclid`, or `fbclid` | Analytics or marketing consent |
| Landing URL and referrer | The request URL and the `Referer` header | Analytics or marketing consent |
| GA4 client ID and session ID | The `_ga` cookie and the `_ga_...` session cookie | Analytics consent |
| Meta browser IDs | The `_fbp` and `_fbc` cookies. Without `_fbc`, Ekom builds one from `fbclid`. | Marketing consent |
| Mailchimp campaign | `mc_cid`, and `mc_tc` only when it is exactly `prec` | Marketing consent |

Ekom writes these values to the tracking cookie: path `/`, `SameSite=Lax`, readable by JavaScript (not `HttpOnly`), `Secure` on HTTPS, and expiring after `CookieLifetimeDays`. A new value replaces the old one for each field. Mailchimp values are the exception: they keep the first touch.

Before the customer gives any consent, Ekom keeps the campaign values, click ID, landing URL, referrer and Mailchimp values in the ASP.NET Core session instead of a cookie. When the customer later gives consent, Ekom moves the values that the consent allows into the tracking cookie.

## Mailchimp first-touch attribution

Ekom recognizes `mc_cid`, and `mc_tc` only when its value is exactly `prec`.

- Before marketing consent, the first Mailchimp values are kept in the session, not in the tracking cookie.
- When marketing consent is given, those first values move into the tracking cookie, and can then be saved with the basket and the order.
- When marketing consent is withdrawn, the middleware moves the Mailchimp values back to the session and removes them from the cookie. They are removed from an unfinished order the next time that order gets the new consent, for example from an `updatecustomer` request that sends it.
- Whenever an order's marketing consent is not `true`, Ekom removes Mailchimp values from the order's tracking.
- The Mailchimp plugin sends the values only with marketing consent. It uses the order's tracking first, then the older customer fields `mc_cid` and `mc_tc`, and drops any tracking code other than `prec`.

The Mailchimp plugin is installed separately. Configure it under `Ekom:Mailchimp` and register it with `builder.Services.AddMailchimp()`. To track completed checkouts, it also needs `Enabled`, `Purchases:Enabled` and `Purchases:TrackCompletedCheckouts` set to `true`. See [Integrations](../integrations/README.md).

## Save tracking on an order

When a basket is saved and it has no consent yet, Ekom reads the consent from the request. When it has no tracking yet, Ekom copies the tracking cookie onto it. After that, the basket keeps both: later requests do not change them from the cookie.

To change them, send consent and tracking explicitly. You can send them with `POST /ekom/order/add`, with `POST /ekom/order/updatecustomer`, or with the dedicated endpoint:

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
    "campaign": "autumn-sale",
    "landingUrl": "https://www.example.com/shop",
    "ga4": {
      "clientId": "123456789.1700000000",
      "sessionId": "1700000000"
    }
  }
}
```

- `storeAlias` and `tracking` are required. Without them the endpoint returns `400`.
- Tracking you send replaces the tracking on the basket. Consent you send replaces the consent.
- Changing tracking or consent on an order that is already final throws `InvalidOperationException` ("Tracking can only be replaced before order completion."). The HTTP endpoints return `500` in that case.

From .NET:

```csharp
using Ekom.API;
using Ekom.Models;

var tracking = new OrderTracking
{
    Source = "newsletter",
    Medium = "email",
    Campaign = "autumn-sale",
};

var consent = new OrderConsent
{
    Analytics = true,
    Marketing = false,
};

IOrderInfo updated = await order.UpdateTrackingAsync(
    "Store",
    tracking,
    new OrderSettings { Consent = consent },
    ct);
```

Consent and identifiers sent by a browser come from an untrusted client. Check them against your own consent records before you save them.

## Events sent to GA4

Ekom sends GA4 events through the Measurement Protocol. It needs a `MeasurementId` and `ApiSecret` for the order's store; without them it logs a warning and sends nothing.

| What happens in Ekom | GA4 event | Setting |
| --- | --- | --- |
| A line is added to the basket | `add_to_cart` | `Events:AddedToCart` |
| A line is removed | `remove_from_cart` | `Events:RemovedFromCart` |
| The basket gets a customer email for the first time | `begin_checkout` | `Events:StartedCheckout` |
| A shipping provider is chosen | `add_shipping_info` | `Events:AddedShippingInfo` |
| A payment provider is chosen | `add_payment_info` | `Events:AddedPaymentInfo` |
| Checkout completes | `purchase` | Always, while GA4 is on |

What the order's analytics consent changes:

- **With analytics consent:** Ekom sends the captured client ID and session ID. For a purchase with captured IDs and campaign values, it also sends a `campaign_details` event first.
- **Without analytics consent:** Ekom still sends the event, but with a new random client ID, no session ID, and none of the extra parameters from tracking data or from [your handlers](#change-a-purchase-event-before-it-is-sent).

How amounts are worked out:

- An item's `price` is the unit price without VAT, before discount. `discount` is the discount per unit, without VAT.
- The event `value` is the sum of the discounted item prices times quantity, without VAT.
- On `purchase`, `tax` is the order VAT and `shipping` is the shipping price without VAT. Payment fees are not part of `value`.
- Amounts are rounded to the number of decimals of the store's currency.
- A coupon is sent on the items it applied to, and on the purchase when at least one item used it.
- Captured campaign values are sent as GA4 campaign parameters, and a `gclid` click ID as `gclid`.

## Events sent to Meta

Ekom sends Meta events through the Conversions API. It sends an event only when all of these are true:

1. The order has marketing consent. Without it, nothing is sent.
2. The store has a `PixelId` and `AccessToken`. Without them, Ekom logs a warning.
3. There is at least one value Meta can match on: email, phone, first name, last name, `_fbp`, `_fbc`, or your own user data. Without one, Ekom skips the event and writes an alert to the order's activity log.

| What happens in Ekom | Meta event | Setting |
| --- | --- | --- |
| A line is added to the basket | `AddToCart` | `Events:AddedToCart` |
| A line is removed | `RemoveFromCart` | `Events:RemovedFromCart` |
| The basket gets a customer email for the first time | `InitiateCheckout` | `Events:StartedCheckout` |
| A shipping provider is chosen | `AddShippingInfo` | `Events:AddedShippingInfo` |
| A payment provider is chosen | `AddPaymentInfo` | `Events:AddedPaymentInfo` |
| Checkout completes | `Purchase` | Always, while Meta is on |

- Email, phone, first name and last name are normalized (trimmed, lower case, digits only for phone) and hashed with SHA-256. `_fbp` and `_fbc` are sent as they are, trimmed.
- The purchase `value` is the amount charged for the order. Item prices include VAT.
- The purchase `event_id` is the order number.
- `event_source_url` is the captured landing URL. Without one, Ekom uses the store's `SiteBaseUrl`, then the general `SiteBaseUrl`. Ekom adds the captured campaign values and click IDs to its query string.

## Change a purchase event before it is sent

`TrackingEvents.Ga4PurchasePreparingAsync` and `TrackingEvents.MetaPurchasePreparingAsync` run after Ekom builds the purchase event and before it queues it. A handler can change `Request`, add values to its dictionaries (`Parameters` for GA4, `UserData` and `CustomData` for Meta), or set `Cancel` to `true` to send nothing.

```csharp
using Ekom.Events;

TrackingEvents.Ga4PurchasePreparingAsync += (sender, args, ct) =>
{
    args.Request.Parameters["customer_type"] = "member";
    return Task.CompletedTask;
};
```

- These events run for purchase events only, not for the extra basket and checkout events.
- They are static events. Unsubscribe when the application stops.
- For GA4, parameters you add are removed when the order has no analytics consent.

## Delivery, logging and failures

- Events are queued in memory and sent in the background, one at a time per provider.
- When a queue is full (`Dispatching:Capacity`), the oldest waiting event is dropped.
- Queued events are lost if the process stops.
- A failed send is not retried. Ekom logs a warning and writes an alert to the order's activity log. A successful send also writes an entry, for example "GA4 purchase event successfully sent".
- With `UseDebugEndpoint` on, a GA4 validation message counts as a failure and is written to the activity log.

## Related pages

- [Headless storefronts](headless.md): send consent and tracking with basket requests.
- [Checkout](checkout.md): the completed checkout that triggers the purchase events.
- [Activity logs](../manager/activity-logs.md): where the sent and failed events show for each order.
- [Integrations](../integrations/README.md): the Mailchimp plugin and other integrations.
- [Configuration reference](../reference/configuration-reference.md): all `Ekom` settings.
- [Events](../reference/events.md): every event Ekom raises.

[Back to Guides](README.md)

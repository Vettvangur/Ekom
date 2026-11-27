# Ekom.Brevo

Brevo contacts, email subscriptions, completed orders, and transactional email for Ekom.

| Package | Umbraco | Framework |
| --- | --- | --- |
| Ekom.Brevo | 17 | .NET 10 |
| Ekom.Brevo.U18 | 18 | .NET 10 |
| Ekom.Brevo.U19 | 19 | .NET 11 |

Call `builder.Services.AddBrevo()` (`using Ekom.Brevo;`). Umbraco composers attach checkout handlers; services still require explicit registration.

```json
{
  "Ekom": {
    "Brevo": {
      "Enabled": true,
      "ApiKey": "use-environment-variables-or-secret-storage",
      "Subscriptions": {
        "Enabled": true,
        "DefaultListId": 12,
        "DoubleOptIn": true,
        "DoubleOptInTemplateId": 42,
        "DoubleOptInRedirectUrl": "https://shop.example.com/subscribed"
      },
      "Orders": { "Enabled": true, "TrackCompletedCheckouts": true },
      "Transactional": {
        "Enabled": true,
        "DefaultFromEmail": "orders@example.com",
        "DefaultFromName": "Example Shop",
        "DefaultReplyTo": "support@example.com"
      },
      "Stores": [
        { "Alias": "iceland", "ApiKey": "store-key", "ListId": 15 }
      ]
    }
  }
}
```

Root `Enabled`, automatic order tracking, and transactional sending default to false. Contacts and orders have independent feature switches. Missing API keys return `ConfigurationMissing`; they do not prevent host startup. Invalid queue settings, duplicate/blank aliases, or an invalid API base URL fail options validation. The configurable `ApiBaseUrl` defaults to `https://api.brevo.com/v3/` and requires HTTPS with a trailing slash. No Klaviyo revision header is used.

Store aliases match case-insensitively. Store API keys and list/DOI settings override global values. Transactional key precedence is store transactional key, store API key, global transactional key, global API key. A store's nested `Transactional` object can override `Enabled`, `DefaultFromEmail`, `DefaultFromName`, `DefaultReplyTo`, and `ApiKey`. Omitted values inherit defaults. Use `Ekom__Brevo__ApiKey` and `Ekom__Brevo__Stores__0__ApiKey` for environment secrets.

## Contacts and consent

Inject `IBrevoContactsService` from `Ekom.Brevo.Services`:

```csharp
var contact = new BrevoContact
{
    StoreAlias = "iceland",
    Email = "customer@example.com",
    FirstName = "Example",
    LastName = "Customer",
};
await contacts.UpsertAsync(contact, cancellationToken);
// Only after explicit email marketing consent:
var subscription = await contacts.SubscribeAsync(contact, cancellationToken: cancellationToken);
var lists = await contacts.GetListsAsync("iceland", cancellationToken);
await contacts.RemoveFromListAsync("iceland", contact.Email, 15, cancellationToken);
await contacts.UnsubscribeAsync("iceland", contact.Email, cancellationToken);
```

Models are in `Ekom.Brevo.Models`. Upsert does not change email/SMS blocklist state or list membership. Custom attribute names must be uppercase and already exist with matching types in Brevo. `EMAIL` and `EXT_ID` custom attributes are rejected to prevent accidental identity changes; use `ExternalId` for the latter. Phone values use Brevo's `SMS` attribute but do not subscribe to SMS.

Subscribe uses explicit list ID, then store list ID, then the global default. Double opt-in is the default and requires a DOI template and redirect URL; list membership is granted by Brevo after confirmation. Disable `Subscriptions:DoubleOptIn` only when calling with existing explicit consent. That path sets `emailBlacklisted=false` and adds the list. Unsubscribe blocks marketing email for the entire Brevo account, potentially affecting other stores sharing that account. RemoveFromList only changes membership. Neither changes transactional suppression.

On completed checkout, `customerBrevoConsentToSubscribeEmail` values `true`, `1`, or `on` request subscription independently of order tracking. Submit false for unchecked choices so earlier cart values are cleared. Missing/false values do not unsubscribe existing contacts. Ordinary purchases do not opt customers into marketing. Public signup endpoints must apply application authentication/anti-abuse controls.

## Orders

Enable the Brevo eCommerce app in the account before tracking orders. Inject `IBrevoOrderService` and call `TrackOrderAsync(IOrderInfo, cancellationToken)` for explicit tracking, or enable `Orders:TrackCompletedCheckouts` for automatic integration. Only completed orders are sent in phase one; cancellations/refunds are not automatically sent.

The mapper sends full order totals before gift-card payment, including tax/shipping, billing/shipping addresses, email identity, products, SKU, fractional quantities, and UTC timestamps. Currency, tax, shipping, discounts, and order number are included as `metaInfo`; configure the Brevo account's ecommerce currency appropriately, as the order endpoint has no top-level currency field. IDs are uppercased store alias plus Ekom GUID; keep store aliases stable. Public `BrevoOrderMapper.ToBrevoOrder` exposes the mapping for inspection.

Duplicate order submissions are suppressed per process for up to 24 hours using a bounded 10,000-entry cache. Accepted and unknown outcomes remain suppressed; definitive rejection permits an explicit retry. This is not durable or cross-instance deduplication. Restart/cache eviction can allow resubmission. Do not replay unknown results without checking Brevo: every order submission can trigger another automation. Historical reconciliation should use Brevo's historical batch import, not this live-order service.

## Transactional email

Authenticate your sending domain and configure an approved sender in Brevo. Inject `IBrevoTransactionalService`. Sending is explicit; the plugin does not automatically send order confirmations or replace Ekom's existing email sender.

```csharp
var submission = await transactional.SendAsync(new BrevoTransactionalMessage
{
    StoreAlias = "iceland",
    To = [new("customer@example.com", "Example Customer")],
    TemplateId = 23,
    Parameters = new Dictionary<string, object?> { ["ORDER_NUMBER"] = "1234" },
}, cancellationToken);

if (submission.Status == BrevoSubmissionStatus.Queued && submission.Completion is { } completion)
{
    var delivery = await completion.WaitAsync(cancellationToken);
    // Accepted means Brevo accepted the request, not inbox delivery.
    // delivery.MessageId is populated for accepted email when Brevo returns it.
}
```

For direct content, omit `TemplateId` and set `Subject` plus `HtmlContent`, `TextContent`, or both. Sender and subject may come from a Brevo template; explicit sender/subject override it. Messages support `Sender`, `ReplyTo`, `Cc`, `Bcc`, and binary attachments: `new BrevoAttachment("invoice.pdf", bytes)`. At least one To recipient and at most 99 total recipients are supported. Messages are serialized before queuing, so later caller mutations cannot alter queued sends. Base64 overhead counts toward message size.

## Queueing and failures

Contact/order work and transactional email use independent, single-consumer queues. Marketing operations retain admission order so a subscription cannot overtake an earlier unsubscribe retry. Each queue has these settings under `Dispatching` or `Transactional:Dispatching`:

| Setting | Default |
| --- | --- |
| MaxQueueSize | 1000 |
| MaxMessageBytes | 9437184 (9 MiB) |
| MaxQueueBytes | 67108864 (64 MiB, including in-flight work) |
| MaxAttempts | 4 |
| InitialRetryDelaySeconds | 2 |
| MaxRetryDelaySeconds | 120 |

Queue admission returns Queued, Disabled, ConfigurationMissing, QueueFull, or (orders only) Duplicate. Invalid payloads throw before admission; cancelled callers throw cancellation before admission. Queued submissions expose a completion task: Accepted, Failed, Unknown, or Cancelled. Background failures log correlation ID, store, status, and HTTP status without payloads or provider error text. Inspect `Completion` for application-specific failure reporting; GetListsAsync propagates read failures directly.

HTTP 429 retries honor Retry-After. If the server requests a delay above the configured maximum, work fails instead of retrying too early. Replay-safe contact operations also retry 5xx/network errors. Orders, DOI messages, and transactional emails do not retry ambiguous network/5xx failures, and return Unknown. Queue admission cancellation does not cancel work already accepted. Shutdown closes admission, cancels in-flight work, and completes unsent work as Cancelled.

Queues are in memory: process crashes lose pending work and completion results. Durable outbox/replay tooling, delivery webhooks, SMS, carts, and catalog synchronization are outside phase one.

## Validation

```shell
dotnet test Plugins/Ekom.Brevo.Tests/Ekom.Brevo.Tests.csproj
dotnet test Plugins/Ekom.Brevo.Tests/Ekom.Brevo.Tests.csproj -p:BrevoUmbracoVersion=18
dotnet test Plugins/Ekom.Brevo.Tests/Ekom.Brevo.Tests.csproj -p:BrevoUmbracoVersion=19
```

Before production, use a test account to verify contact/DOI consent, a completed purchase and automation, and both template/direct emails including attachments. No live Brevo credentials are needed for the automated tests.

API references: [contacts](https://developers.brevo.com/reference/create-contact), [double opt-in](https://developers.brevo.com/reference/create-doi-contact), [orders](https://developers.brevo.com/reference/create-order), and [transactional email](https://developers.brevo.com/reference/send-transac-email).

Release automation uses `Ekom.Brevo-v*`, `Ekom.Brevo.U18-v*`, and `Ekom.Brevo.U19-v*` tags. Configure NuGet trusted publishing for the three package IDs and workflows before the first release; creating this integration does not publish packages.

The first release is **1.0.0** for all three packages. Merge the integration PR into `Ekom` with its conventional `feat(brevo):` title. The existing release-please action then creates or updates the shared `chore: release Ekom` PR, including Brevo 1.0.0. Brevo uses the same linked-version configuration as Algolia, keeping the base, U18, and U19 packages together.

Merge that release PR to let release-please update the manifest and create the `Ekom.Brevo-v1.0.0`, `Ekom.Brevo.U18-v1.0.0`, and `Ekom.Brevo.U19-v1.0.0` release tags automatically. Those tags trigger the matching NuGet publishing workflows using the existing `RELEASE_PLEASE_TOKEN` and NuGet trusted publishing. No manual tag push is required. Subsequent conventional commits produce normal version bumps. Manual publishing workflow dispatch also requires selecting a matching release tag; selecting a branch intentionally skips publishing.

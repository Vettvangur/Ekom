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
    OrderId = "order-1234",
    IdempotencyKey = "order-1234:confirmation",
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

Contact/order work retains its in-memory single-consumer queue and admission ordering. Transactional email uses a durable outbox in the configured Umbraco database (SQL Server or SQLite), with one worker per host and database claims shared across instances. Each queue has these settings under `Dispatching` or `Transactional:Dispatching`:

| Setting | Default |
| --- | --- |
| MaxQueueSize | 1000 |
| MaxMessageBytes | 9437184 (9 MiB) |
| MaxQueueBytes | 67108864 (64 MiB, including in-flight work) |
| MaxAttempts | 4 |
| InitialRetryDelaySeconds | 2 |
| MaxRetryDelaySeconds | 120 |

Queue admission returns Queued, Disabled, ConfigurationMissing, QueueFull, or Duplicate (orders or emails with an `IdempotencyKey`). Invalid payloads throw before admission; cancelled callers throw cancellation before admission. Queued submissions expose a completion task: Accepted, Failed, Unknown, or Cancelled. Background failures log correlation ID, store, status, and HTTP status without payloads or provider error text. Inspect `Completion` for application-specific failure reporting; GetListsAsync propagates read failures directly.

HTTP 429 retries honor Retry-After. If the server requests a delay above the configured maximum, work fails instead of retrying too early. Replay-safe contact operations also retry 5xx/network errors. Orders, DOI messages, and transactional emails do not retry ambiguous network/5xx failures, and return Unknown. Queue admission cancellation does not cancel work already accepted. Shutdown closes admission and cancels in-flight requests. Local completion tasks for unsent emails complete as Cancelled, but their durable rows remain Pending and resume on restart. Use the durable delivery service after restart; completion tasks themselves are process-local. Contact/order shutdown behavior is unchanged.

### Durable email outbox

`AddBrevo()` uses Ekom's `DatabaseFactory` and the existing `umbracoDbDSN` connection. Three plugin-owned tables (`EkomBrevoEmail`, `EkomBrevoEvent`, `EkomBrevoLock`) and lookup indexes are created lazily on first use. The database account needs schema creation permission on first deployment. Schema setup and queue mutations use database transactions; no Brevo HTTP request runs inside a database transaction. Run all instances against the same database. SQLite supports multiple processes using the same local database file; use SQL Server for multiple application servers.

`Queued` is returned only after the serialized email is committed. Database failures propagate to the caller rather than reporting successful admission. Queue count and byte limits apply globally to Pending and Sending emails. Store/API keys are resolved at send time and never stored in the outbox. Payloads and attachments contain customer data: apply your normal database access, backup and encryption policies. Accepted payloads are cleared immediately; failed/unknown payloads remain until retention expires.

Optional `IdempotencyKey` values deduplicate email submissions per store across instances and restarts. Repeating the same key and content returns Duplicate with the original CorrelationId; different content/order metadata throws. Keys are hashed before storage, and remain effective only while the row is retained. The correlation header counts toward payload byte limits. `OrderId` is optional application metadata; callers supply it when sending an order email. Phase 2 does not automatically send order confirmations.

HTTP 429 retry time and attempt count are stored durably. Other ambiguous email failures (timeouts, network errors, HTTP 408/5xx) become Unknown. A crashed Sending lease also becomes Unknown instead of being sent again, even if the process crashed before making the HTTP call. This prioritizes avoiding duplicate customer emails over automatic recovery of uncertain sends; check Brevo before deliberately submitting a replacement. Exactly-once inbox delivery is not guaranteed. Disabling the integration pauses the worker; a store whose transactional sending is disabled, or whose key is missing, produces a definitive Failed result without sending.

Settings under `Transactional:Persistence`:

| Setting | Default | Purpose |
| --- | --- | --- |
| PollIntervalSeconds | 2 | Idle queue and cross-instance completion polling (1-60) |
| LeaseSeconds | 120 | Crash recovery deadline (60-3600; HTTP timeout is 30 seconds) |
| RetentionDays | 30 | Terminal history, event and dedup retention (1-365) |

The worker removes expired terminal rows and their events hourly. Pending and Sending rows are preserved. Retention is measured from the last queue-state update, not webhook arrival. All workers should use the same settings and synchronized UTC clocks. There is no upgrade recovery for emails that existed only in the old process's memory; drain that queue before deploying.

### Delivery tracking and failed emails

Inject `IBrevoDeliveryService` from `Ekom.Brevo.Services`:

```csharp
var status = await deliveries.GetAsync("iceland", submission.CorrelationId, cancellationToken);
var orderEmails = await deliveries.GetByOrderAsync("iceland", "order-1234", cancellationToken);
var failures = await deliveries.GetFailuresAsync("iceland", take: 100, cancellationToken: cancellationToken);
// Only a definitive Failed send can be requeued; Unknown and Accepted are refused.
var retried = await deliveries.RetryAsync("iceland", submission.CorrelationId, cancellationToken);
```

The status includes queue state, attempts, timestamps, HTTP status, Brevo message ID, order ID and recipient events in occurrence order. API Accepted is separate from inbox Delivered. Events are per recipient, so one email can have both delivered and bounced recipients. GetByOrderAsync returns the latest 100 sends; GetFailuresAsync returns up to 100 Failed/Unknown sends. These services are for trusted application code; no public read/retry endpoint exposes customer data. Manual retry resets the attempt budget, obeys queue capacity and uses current credentials.

### Transactional email webhooks

Webhooks default to disabled. Configure a random bearer secret (32-256 characters, no whitespace) through secret storage:

```json
"Transactional": {
  "Enabled": true,
  "Webhooks": { "Enabled": true, "Token": "replace-with-a-random-secret-from-secret-storage" }
}
```

Use `Ekom__Brevo__Transactional__Webhooks__Token` for the global secret. An optional store `Transactional:WebhookToken` overrides it. Map the route in the application's endpoint setup:

```csharp
using Ekom.Brevo.Webhooks;
// WebApplication implements IEndpointRouteBuilder; call before app.Run().
app.MapBrevoWebhooks();
```

Configure a Brevo **transactional** webhook for the HTTPS URL `https://your-shop.example/ekom/brevo/webhooks/iceland`. Set Brevo's webhook `auth` to `{ "type": "bearer", "token": "the-same-secret" }`. Subscribe to `sent`, `delivered`, `softBounce`, `hardBounce`, `blocked`, `spam`, `invalid`, `deferred`, `error`, and `unsubscribed` as available in Brevo. Subscription event names differ from delivered payload names (for example `hardBounce` sends `hard_bounce`). Keep the URL store alias aligned with the sending store; configure one route per store/account as needed.

The endpoint accepts single objects or batches (maximum 100 events and 64 KiB), authenticates before reading the body, and commits events before returning 200. Invalid requests return 400, missing/incorrect authentication 401, oversized bodies 413, and disabled integration/webhooks 404. Storage failures return 5xx for provider retry. Unknown account messages and unsupported engagement events are acknowledged and ignored. SMS/WhatsApp and marketing webhooks are not supported.

Tracking uses normalized `message-id` (angle brackets removed) and the generated `X-Mailin-custom` correlation header. Events arriving before the API response are retained. Duplicate events are identified by email correlation, provider message ID, recipient, event type and UTC `ts_event`; Brevo's `id` identifies the webhook configuration and is not treated as an event ID. Events are stored independently, so an older event arriving late does not overwrite a newer one. A webhook can supply a missing message ID for an Unknown send, but does not automatically authorize replay or change its API outcome.

See Brevo's [webhook payloads](https://developers.brevo.com/docs/transactional-webhooks) and [bearer authentication](https://developers.brevo.com/docs/secured-webhooks).

Contact/order queues remain in memory. SMS, WhatsApp, carts, catalog synchronization, and a backoffice delivery UI are outside phase 2.

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

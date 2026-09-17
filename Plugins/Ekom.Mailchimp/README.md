# Ekom.Mailchimp

Mailchimp Marketing API integration for Ekom. It supports audience tags, subscriptions, unsubscriptions, and completed-purchase conversion tracking.

## Install

Choose the package that matches the application:

| Package | Target | Integration |
| --- | --- | --- |
| `Ekom.Mailchimp` | .NET 8 | Reusable services with `Ekom.Core`; no Umbraco event component |
| `Ekom.Mailchimp` | .NET 10 and Umbraco 17 | Services plus the Ekom completed-checkout event component through `Ekom.U17` |
| `Ekom.Mailchimp.U18` | .NET 10 and Umbraco 18 | Services plus the Ekom completed-checkout event component through `Ekom.U18` |

```shell
dotnet add package Ekom.Mailchimp
# Umbraco 18:
dotnet add package Ekom.Mailchimp.U18
```

Both packages expose the same `AddMailchimp` registration and `Ekom:Mailchimp` options. Calling `AddMailchimp` is required in every host; the Umbraco packages automatically register only the completed-checkout event component.

## Configuration

```json
{
  "Ekom": {
    "Mailchimp": {
      "Enabled": true,
      "ApiKey": "your-api-key-us1",
      "AudienceId": "audience-id",
      "EcommerceStoreId": "stable-mailchimp-store-id",
      "SiteBaseUrl": "https://www.example.com",
      "Stores": [
        {
          "Alias": "iceland",
          "ApiKey": "iceland-api-key-us1",
          "AudienceId": "iceland-audience-id",
          "EcommerceStoreId": "iceland-mailchimp-store-id",
          "SiteBaseUrl": "https://www.example.is"
        },
        {
          "Alias": "united-kingdom",
          "ApiKey": "united-kingdom-api-key",
          "ServerPrefix": "us2",
          "AudienceId": "united-kingdom-audience-id",
          "EcommerceStoreId": "united-kingdom-mailchimp-store-id",
          "SiteBaseUrl": "https://www.example.co.uk"
        }
      ],
      "Purchases": {
        "Enabled": true,
        "TrackCompletedCheckouts": true
      },
      "Subscriptions": {
        "Enabled": true
      },
      "Dispatching": {
        "MaxQueueSize": 1000,
        "MaxConcurrency": 3,
        "MaxAttempts": 4,
        "InitialRetryDelaySeconds": 2
      }
    }
  }
}
```

`Stores` is an array of per-store overrides. Each `Alias` is matched against the Ekom store alias case-insensitively. A store can override `ApiKey`, `ServerPrefix`, `AudienceId`, `EcommerceStoreId`, and `SiteBaseUrl`; omitted values fall back to the corresponding global value. Global credentials can be omitted when every store supplies its complete configuration.

Global and store-level values can be mixed. For example, each store can provide its own API key while sharing global audience and e-commerce store IDs. A root `ApiKey` is not required when every used store provides one:

```json
{
  "Ekom": {
    "Mailchimp": {
      "Enabled": true,
      "AudienceId": "shared-audience-id",
      "EcommerceStoreId": "shared-mailchimp-store-id",
      "Stores": [
        {
          "Alias": "HVerslun",
          "ApiKey": "store-specific-key-us21"
        }
      ]
    }
  }
}
```

`ServerPrefix` is inferred from the suffix of a standard Mailchimp API key, such as `us1` in `your-api-key-us1`. Set it explicitly when the API key does not contain the server prefix.

`Subscriptions:Enabled` defaults to `true` and controls audience tag retrieval, subscriptions, and unsubscriptions. `Purchases:Enabled` also defaults to `true`; `Purchases:TrackCompletedCheckouts` defaults to `false`.

Dispatcher settings apply to the shared bounded in-memory work queue:

| Setting | Default | Description |
| --- | --- | --- |
| `Dispatching:MaxQueueSize` | `1000` | Maximum queued work items. New work is dropped with a warning when the queue is full. |
| `Dispatching:MaxConcurrency` | `3` | Maximum number of work items processed concurrently. |
| `Dispatching:MaxAttempts` | `4` | Maximum attempts for transient HTTP and network failures, including the initial request. |
| `Dispatching:InitialRetryDelaySeconds` | `2` | Initial retry delay; subsequent retries use exponential backoff. |

Use the standard ASP.NET Core configuration key format for environment variables and secrets. Array entries use zero-based indexes; for example, `Ekom__Mailchimp__Stores__0__ApiKey` sets the API key for the first configured store.

Missing operational values do not prevent the application from starting. Each missing setting is logged once for the affected store, and work for that store is ignored before it reaches the queue. Configured stores are checked during startup; missing global fallback values for an unlisted alias are logged when that alias is first used. Stores are evaluated independently, and a missing `EcommerceStoreId` disables purchase tracking without disabling subscriptions. Invalid dispatcher settings remain startup errors. Duplicate or blank store aliases are also startup errors when Mailchimp and at least one feature are enabled.

Register the integration during application startup. The options are read from `Ekom:Mailchimp`.

```csharp
using Ekom.Mailchimp;

builder.Services.AddMailchimp();
```

Options can also be configured or overridden in code:

```csharp
using Ekom.Mailchimp;

builder.Services.AddMailchimp(options =>
{
    options.Enabled = true;
    options.ApiKey = "your-api-key-us1";
    options.AudienceId = "audience-id";
    options.EcommerceStoreId = "stable-mailchimp-store-id";
    options.SiteBaseUrl = new Uri("https://www.example.com");
    options.Purchases.TrackCompletedCheckouts = true;
});
```

The Umbraco 17 and Umbraco 18 packages automatically register the completed-checkout event component. Calling `AddMailchimp` is still required.

## Usage

### Inject the service

`IMailchimpService` is the public entry point for audience tags, subscriptions, and purchases.

```csharp
using Ekom.Mailchimp.Services;

public sealed class CustomerMarketingService(IMailchimpService mailchimp)
{
    private readonly IMailchimpService _mailchimp = mailchimp;
}
```

### Fetch audience tags

Fetch tags from the globally configured `AudienceId` for use in forms or other customer preference interfaces:

```csharp
using Ekom.Mailchimp.Models;

IReadOnlyList<MailchimpTag> tags = await _mailchimp.GetTagsAsync(cancellationToken);
```

Successful responses are cached in memory for five minutes. Store-specific overrides are not used for this operation. If Mailchimp or subscriptions are disabled, the method returns an empty list. Mailchimp API, network, cancellation, configuration, and invalid-response errors are propagated to the caller.

### Subscribe a contact

Subscription calls require an explicit `Pending` or `Subscribed` status. Use `Pending` for double opt-in workflows, and only use `Subscribed` when the contact has already provided the required consent.

```csharp
using Ekom.Mailchimp.Models;

await _mailchimp.SubscribeAsync(new MailchimpSubscribeRequest
{
    StoreAlias = "default",
    Email = "person@example.com",
    Status = MailchimpSubscriptionStatus.Pending,
    FirstName = "Example",
    LastName = "Customer",
    Language = "en",
    MergeFields = new Dictionary<string, object?>
    {
        ["COMPANY"] = "Example Ltd.",
    },
    Tags = ["customer", "web-signup"],
}, cancellationToken);
```

### Unsubscribe a contact

```csharp
using Ekom.Mailchimp.Models;

await _mailchimp.UnsubscribeAsync(new MailchimpUnsubscribeRequest
{
    StoreAlias = "default",
    Email = "person@example.com",
}, cancellationToken);
```

### Track an Ekom order

Pass an `IOrderInfo` to use the standard Ekom-to-Mailchimp mapping. The mapper includes the customer, addresses, order totals, and product lines.

```csharp
using Ekom.Models;

public ValueTask TrackOrderAsync(IOrderInfo order, CancellationToken cancellationToken)
    => _mailchimp.TrackPurchaseAsync(order, cancellationToken);
```

The standard mapping reads subscription consent from `customerMailchimpConsentToSubscribe`. Ekom automatically captures `mc_cid` and the supported `mc_tc=prec` value from the visitor's landing URL as first-touch attribution. Before marketing consent, those values remain in the pre-consent session; after consent, they are promoted to the `EkomTracking` cookie and persisted with the basket/order. Withdrawing marketing consent removes Mailchimp attribution from the tracking cookie and unfinished order, and the mapper suppresses both persisted and legacy attribution without consent. With consent, the mapper prefers persisted tracking and retains matching customer properties as a backward-compatible fallback. Relative product and image URLs use the configured `SiteBaseUrl`.

### Track a purchase directly

Use `MailchimpPurchase` when the purchase does not originate from an Ekom order or when the complete payload is already available.

```csharp
using Ekom.Mailchimp.Models;

await _mailchimp.TrackPurchaseAsync(new MailchimpPurchase
{
    StoreAlias = "default",
    StoreName = "Example Store",
    OrderId = "order-123",
    OrderNumber = "123",
    CurrencyCode = "USD",
    OrderTotal = 49.90m,
    ProcessedAt = DateTimeOffset.UtcNow,
    Customer = new MailchimpPurchaseCustomer
    {
        Id = "customer-123",
        Email = "person@example.com",
        FirstName = "Example",
        LastName = "Customer",
        MarketingOptIn = true,
    },
    Lines =
    [
        new MailchimpPurchaseLine
        {
            Id = "line-123",
            ProductId = "product-123",
            ProductVariantId = "variant-123",
            ProductTitle = "Example product",
            VariantTitle = "Default",
            Sku = "SKU-123",
            Quantity = 1,
            Price = 49.90m,
            ProductUrl = new Uri("https://www.example.com/products/example"),
        },
    ],
}, cancellationToken);
```

Purchase, customer, line, product, and variant IDs must be non-empty and no longer than 50 characters. Currency codes must be three-letter ISO 4217 codes, such as `USD`; line quantities must be positive integers, and the only supported tracking code is `prec`.

### Map and customize an Ekom order

`ToMailchimpPurchase` exposes the standard order mapper when a mapped purchase needs to be inspected or changed before submission.

```csharp
using Ekom.Mailchimp;
using Ekom.Mailchimp.Mappers;
using Ekom.Mailchimp.Models;
using Ekom.Models;
using Microsoft.Extensions.Options;

public async ValueTask TrackCustomizedOrderAsync(
    IOrderInfo order,
    IOptions<MailchimpOptions> options,
    CancellationToken cancellationToken)
{
    MailchimpPurchase purchase = order.ToMailchimpPurchase(options.Value) with
    {
        FulfillmentStatus = "pending",
    };

    await _mailchimp.TrackPurchaseAsync(purchase, cancellationToken);
}
```

### Enrich every purchase

Implement `IMailchimpPurchaseEnricher` to apply reusable changes to purchases created through either `TrackPurchaseAsync` overload.

```csharp
using Ekom.Mailchimp.Enrichers;
using Ekom.Mailchimp.Models;

public sealed class FulfillmentStatusEnricher : IMailchimpPurchaseEnricher
{
    public ValueTask<MailchimpPurchase> EnrichAsync(
        MailchimpPurchase purchase,
        CancellationToken cancellationToken = default)
    {
        MailchimpPurchase enriched = purchase with
        {
            FulfillmentStatus = purchase.FulfillmentStatus ?? "pending",
        };

        return ValueTask.FromResult(enriched);
    }
}
```

Register enrichers with dependency injection. Multiple enrichers run sequentially in their registration order before validation and queueing.

```csharp
using Ekom.Mailchimp;
using Ekom.Mailchimp.Enrichers;

builder.Services.AddMailchimp();
builder.Services.AddScoped<IMailchimpPurchaseEnricher, FulfillmentStatusEnricher>();
```

### Automatic checkout tracking

Automatic tracking requires `Enabled`, `Purchases:Enabled`, and `Purchases:TrackCompletedCheckouts` to be `true`. Product and variant records referenced by the order are upserted before the order. Replaying an order with the same IDs is safe because purchase tracking uses idempotent Mailchimp commerce upserts.

### Queueing and failures

Service calls perform immediate guard and configuration checks before queueing; purchase payloads are also validated before queueing. They do not wait for Mailchimp to process the operation, so remote API failures occur asynchronously. The dispatcher uses a bounded in-memory queue and retries transient HTTP and network failures. When the queue is full, new work is dropped with a warning rather than blocking checkout. Remote failures are logged, including details from `MailchimpApiException`. Work still queued during process shutdown is not durable; call the relevant service method again to replay the operation safely.

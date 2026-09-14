# Ekom.Mailchimp

Mailchimp Marketing API integration for Ekom. The first release supports audience subscriptions, unsubscriptions, and completed-purchase conversion tracking.

`Ekom.Mailchimp` supports Umbraco 17 on .NET 10 and reusable integration services on .NET 8. Use `Ekom.Mailchimp.U18` for Umbraco 18.

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
      }
    }
  }
}
```

`Stores` is an array of per-store overrides. Each `Alias` is matched against the Ekom store alias case-insensitively. A store can override `ApiKey`, `ServerPrefix`, `AudienceId`, `EcommerceStoreId`, and `SiteBaseUrl`; omitted values fall back to the corresponding global value. Global credentials can be omitted when every store supplies its complete configuration.

`ServerPrefix` is inferred from the suffix of a standard Mailchimp API key, such as `us1` in `your-api-key-us1`. Set it explicitly when the API key does not contain the server prefix.

Use the standard ASP.NET Core configuration key format for environment variables and secrets. Array entries use zero-based indexes; for example, `Ekom__Mailchimp__Stores__0__ApiKey` sets the API key for the first configured store.

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

The Umbraco package automatically registers the completed-checkout event component. Calling `AddMailchimp` is still required.

## Usage

### Inject the service

`IMailchimpService` is the public entry point for subscriptions and purchases.

```csharp
using Ekom.Mailchimp.Services;

public sealed class CustomerMarketingService(IMailchimpService mailchimp)
{
    private readonly IMailchimpService _mailchimp = mailchimp;
}
```

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

The standard mapping reads marketing consent from `customerMailchimpConsentToSubscribe`, the Mailchimp campaign ID from `mc_cid`, and the tracking code from `mc_tc`. Relative product and image URLs use the configured `SiteBaseUrl`.

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

Service calls perform immediate guard checks and enqueue work; purchase payloads are also validated before queueing. They do not wait for Mailchimp to process the operation, so configuration, subscription payload, and API failures can occur asynchronously. The dispatcher uses a bounded in-memory queue and retries transient HTTP and network failures. When the queue is full, new work is dropped with a warning rather than blocking checkout. Remote failures are logged, including details from `MailchimpApiException`. Work still queued during process shutdown is not durable; call the relevant service method again to replay the operation safely.

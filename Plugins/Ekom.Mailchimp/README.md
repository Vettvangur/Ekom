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

Register the integration during application startup with `builder.Services.AddMailchimp();`. The Umbraco package automatically registers the completed-checkout event component.

## Usage

Inject `IMailchimpService`. Subscription calls require an explicit `Pending` or `Subscribed` status. Use `Pending` for double opt-in workflows.

```csharp
await mailchimp.SubscribeAsync(new MailchimpSubscribeRequest
{
    StoreAlias = "default",
    Email = "person@example.com",
    Status = MailchimpSubscriptionStatus.Pending,
    FirstName = "Example",
    Tags = ["customer"],
});
```

`TrackPurchaseAsync(IOrderInfo)` maps an Ekom order and queues an idempotent Mailchimp commerce upsert. Product and variant records referenced by the order are upserted before the order. Automatic checkout tracking is controlled by `Purchases:TrackCompletedCheckouts`.

The dispatcher uses a bounded in-memory queue. When the queue is full, new work is dropped with a warning rather than blocking checkout. Work still queued during process shutdown is not durable; call the public purchase method again to replay an order safely.

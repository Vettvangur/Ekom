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
      "Purchases": {
        "Enabled": true,
        "TrackCompletedCheckouts": true
      }
    }
  }
}
```

`ServerPrefix` is inferred from the suffix of a standard Mailchimp API key. It can be set explicitly. Values can be overridden for individual Ekom stores under `Stores` by matching `Alias`.

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

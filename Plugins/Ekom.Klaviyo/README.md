<h1 align="center">
Ekom Klaviyo Plugin
 
[![Nuget](https://img.shields.io/nuget/vpre/Ekom.Klaviyo?color=ed0f0f)](https://www.nuget.org/packages/Ekom.Klaviyo/)
[![License](https://img.shields.io/badge/license-MIT-green)](./LICENSE)

</h1>

# Klaviyo Configuration

This document describes the configuration options for the Klaviyo integration, including event tracking (orders) and catalog synchronization.

The configuration is typically placed in `appsettings.json` or an environment-specific configuration file.

---

## Root Configuration

```json
"Klaviyo": {
  "Enabled": true,
  "PrivateApiKey": "secret",
  "ApiBaseUrl": "https://a.klaviyo.com",
  "Revision": "2026-01-15",
  "ProfileExternalIdProperty": "email",
  "SiteBaseUrl": "https://vettvangur.is",
  "Testing": false,
  "Stores": [],
  "Orders": {},
  "Subscriptions": {},
  "Catalog": {},
  "Tracking": {}
}
```

| Key | Type | Description |
|-----|------|-------------|
| `Enabled` | `bool` | Master switch. If `false`, Disable Klaviyo plugin. |
| `PrivateApiKey` | `string` | Klaviyo **Private API Key** used for authentication. |
| `ApiBaseUrl` | `string` | Klaviyo API base URL. Usually `https://a.klaviyo.com`. |
| `Revision` | `string` | Klaviyo API revision header (required). |
| `ProfileExternalIdProperty` | `string` | Default Email property used as the external ID for profiles. Other options: , `phone`, `username`, any property on customer `customerExternalId` . |
| `SiteBaseUrl` | `string` | Public site base URL used to generate product and checkout URLs. |
| `Testing` | `bool` | If `true`, enables testing mode (Events will be sent to same event but with Test at the end. "Placed Order Test"). |
| `Stores` | `array` | Optional per-store configuration. If empty the first store will be used. |
| `Orders` | `object` | Orders tracking configuration. |
| `Subscriptions` | `object` | Profile subscription and list configuration. |
| `Catalog` | `object` | Product catalog synchronization configuration. |
| `Tracking` | `object` | Custom event tracking configuration. |


## Stores

```json
"Stores": [
  {
    "StoreAlias": "Store",
    // "PrivateApiKey": "xxx",
    // "ListId": "LIST_ID"
  }
]
```

| Key | Type | Description |
|-----|------|-------------|
| `StoreAlias` | `string` | Store identifier (must match Ekom store alias). |
| `PrivateApiKey` | `string` | Optional API key override for this store. |
| `ListId` | `string` | Optional list ID override for this store. |

Use this when:

- Running multiple stores in a single application

- Each store uses a different Klaviyo account

## Orders

```json
"Orders": {
  "Enabled": true,
  "TrackingPlacedOrders": true,
  "Dispatching": {
    "MaxBatchSize": 100,
    "FlushIntervalSeconds": 2,
    "MaxQueueSize": 10000
  }
}
```


| Key | Type | Description |
|-----|------|-------------|
| `Enabled` | `bool` | Enables Klaviyo event tracking. |
| `TrackingPlacedOrders` | `bool` | Enables automatic tracking of *Placed Order* events in Ekom Complete Checkout Event. |
| `Dispatching` | `object` | Background dispatching settings. |


| Key | Type | Description |
|-----|------|-------------|
| `MaxBatchSize` | `int` | Maximum number of queued events processed per dispatch cycle. |
| `FlushIntervalSeconds` | `int` | Interval in seconds between dispatcher flushes. |
| `MaxQueueSize` | `int` | Maximum number of queued events before backpressure applies. |


## Subscriptions

```json
"Subscriptions": {
  "Enabled": true,
  "DefaultListId": "LIST_ID",
  "Dispatching": {
    "MaxBatchSize": 100,
    "FlushIntervalSeconds": 2,
    "MaxQueueSize": 10000
  }
}
```

| Key | Type | Description |
|-----|------|-------------|
| `Enabled` | `bool` | Enables profile upsert and list add operations. |
| `DefaultListId` | `string` | Optional global list ID used when no store list is set. |
| `Dispatching` | `object` | Background dispatching settings. |

List resolution precedence:

- Explicit list ID on the profile update payload
- Store `ListId`
- `Subscriptions.DefaultListId`

If none are set, profiles are not added to a list.


## Catalog

```json
"Catalog": {
  "Enabled": true,
  "ShowPrice": true,
  "ShowInventory": true,
  "InventoryPolicy": 2,
  "ImageCrop": "?width=400&height=500&rmode=BoxPad&format=webp",
  "Dispatching": {
    "MaxBatchSize": 100,
    "FlushIntervalSeconds": 2,
    "MaxQueueSize": 10000
  },
  "SyncMode": "ApiPush",
  "DeleteMode": "Hard"
}
```

| Key | Type | Description |
|-----|------|-------------|
| `Enabled` | `bool` | Enables catalog synchronization. |
| `ShowPrice` | `bool` | Includes product prices in catalog items. |
| `ShowInventory` | `bool` | Includes inventory levels in catalog items. |
| `InventoryPolicy` | `int` | Inventory handling policy (implementation-specific). |
| `ImageCrop` | `string` | Query string appended to product image URLs. |
| `Dispatching` | `object` | Background dispatching settings. |
| `SyncMode` | `string` | Catalog sync strategy (`ApiPush` or `FeedPull`). |
| `DeleteMode` | `string` | Product deletion behavior (`Hard` or `Soft`). |

Feed pull uses the current Ekom product price for `price`. When a product discount is active, the original price, discount price, discount amount, and discount state are also included in `custom_attributes`.

Projects can customize feed items by registering one or more `IKlaviyoProductFeedItemEnricher` implementations. Enrichers run before feed serialization and can update the mapped item or its custom attributes.


## Tracking

```json
"Tracking": {
  "Enabled": true,
  "Search": true,
  "AddedToCart": true,
  "ViewedCategory": true,
  "ViewedProduct": true,
  "ActiveOnSite": true,
  "StartedCheckout": true,
  "Dispatching": {
    "MaxBatchSize": 100,
    "FlushIntervalSeconds": 2,
    "MaxQueueSize": 10000
  }
}
```

| Key | Type | Description |
|-----|------|-------------|
| `Enabled` | `bool` | Enables custom event tracking. |
| `Search` | `bool` | Enables *Search* tracking. |
| `AddedToCart` | `bool` | Enables *Added to Cart* tracking. |
| `ViewedCategory` | `bool` | Enables *Viewed Category* tracking. |
| `ViewedProduct` | `bool` | Enables *Viewed Product* tracking. |
| `ActiveOnSite` | `bool` | Enables *Active on Site* tracking. |
| `StartedCheckout` | `bool` | Enables *Started Checkout* tracking. |
| `Dispatching` | `object` | Background dispatching settings. |


## Generic Events

Use `IKlaviyoEventService` to send project-specific events that are not built into the package. Generic events require a store alias and only depend on the global `Klaviyo:Enabled` setting; they do not require `Tracking:Enabled` to be enabled.

```csharp
using Ekom.Klaviyo.Models.Events;
using Ekom.Klaviyo.Services;

public sealed class AccountEmails
{
    private readonly IKlaviyoEventService _events;

    public AccountEmails(IKlaviyoEventService events)
    {
        _events = events;
    }

    public Task VerifyEmailAsync(
        string storeAlias,
        string email,
        string name,
        string tokenUrl,
        CancellationToken ct)
        => _events.SendEventAsync(
            storeAlias,
            "Verify Email Requested",
            new { activate_url = tokenUrl },
            new KlaviyoEventProfile
            {
                Email = email,
                FirstName = name
            },
            ct: ct);
}
```

If you need full control over the Klaviyo request body, send the complete `/api/events` envelope with `SendRawEventAsync`:

```csharp
await events.SendRawEventAsync(storeAlias, payload, ct);
```


## Enrichers

Enrichers let a site add, override, or inspect Klaviyo payload data before the plugin serializes and queues it. Register one or more implementations in DI after `AddKlaviyo`; they run in DI registration order.

```csharp
builder.Services.AddKlaviyo();
builder.Services.AddSingleton<IKlaviyoProductFeedItemEnricher, ProductFeedBrandEnricher>();
builder.Services.AddSingleton<IKlaviyoProductItemEnricher, CatalogProductBrandEnricher>();
builder.Services.AddSingleton<IKlaviyoTrackingEnricher, TrackingSourceEnricher>();
builder.Services.AddSingleton<IKlaviyoPlacedOrderEnricher, OrderChannelEnricher>();
builder.Services.AddSingleton<IKlaviyoProfilesEnricher, ConsentAuditEnricher>();
```

Use singleton enrichers unless the enricher depends on scoped services. Keep enrichers fast and avoid logging secrets, API keys, or unnecessary PII.

### Product feed enrichers

Feed enrichers run only for catalog `FeedPull` responses from `/ekom/klaviyo/product/feed`. They receive the original Ekom `IProduct`, the mapped `KlaviyoProductFeedItem`, the store alias, and the current options. Use this to adjust feed data, add custom attributes, or override fields before JSON serialization.

```csharp
using Ekom.Klaviyo.Enrichers.ProductFeedEnricher;
using Ekom.Klaviyo.Models.Catalog;

public sealed class ProductFeedBrandEnricher : IKlaviyoProductFeedItemEnricher
{
    public ValueTask EnrichAsync(
        KlaviyoProductFeedItem item,
        KlaviyoProductFeedEnrichmentContext ctx,
        CancellationToken ct)
    {
        item.CustomAttributes ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        item.CustomAttributes["sku"] = ctx.Product.SKU;
        item.CustomAttributes["feed_store"] = ctx.StoreAlias;

        return ValueTask.CompletedTask;
    }
}
```

### Catalog API product enrichers

Catalog product enrichers run for catalog `ApiPush` synchronization before items are sent to Klaviyo's catalog API. Use this when you push catalog items through the background dispatcher instead of exposing the feed endpoint.

```csharp
using Ekom.Klaviyo.Enrichers.ProductEnricher;
using Ekom.Klaviyo.Models.Catalog;

public sealed class CatalogProductBrandEnricher : IKlaviyoProductItemEnricher
{
    public ValueTask EnrichAsync(
        KlaviyoProductItem item,
        KlaviyoProductEnrichmentContext ctx,
        CancellationToken ct)
    {
        item.CustomMetadata ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        item.CustomMetadata["source"] = "ekom-api-push";
        item.CustomMetadata["is_first_publish"] = ctx.IsFirstPublish;

        return ValueTask.CompletedTask;
    }
}
```

### Tracking enrichers

Tracking enrichers run before tracking payloads are mapped and queued. The context contains the tracking event type, store alias, and concrete payload object. Cast the payload to the event type you want to modify.

```csharp
using Ekom.Klaviyo.Enrichers.TrackingEnricher;
using Ekom.Klaviyo.Models.Tracking;

public sealed class TrackingSourceEnricher : IKlaviyoTrackingEnricher
{
    public ValueTask EnrichAsync(KlaviyoTrackingEnrichmentContext context, CancellationToken ct = default)
    {
        if (context.Payload is KlaviyoViewedProductEvent viewedProduct)
        {
            viewedProduct.CustomProperties["source"] = "ekom";
            viewedProduct.CustomProperties["store_alias"] = context.StoreAlias;
        }

        return ValueTask.CompletedTask;
    }
}
```

### Placed order enrichers

Placed order enrichers run before `Placed Order` events are mapped and queued. Use them to add custom event properties, normalize order data, or attach project-specific metadata.

```csharp
using Ekom.Klaviyo.Enrichers.OrderEnricher;
using Ekom.Klaviyo.Models.Orders;

public sealed class OrderChannelEnricher : IKlaviyoPlacedOrderEnricher
{
    public ValueTask EnrichAsync(KlaviyoPlacedOrder order, CancellationToken ct)
    {
        order.CustomProperties["sales_channel"] = "web";
        order.CustomProperties["store_alias"] = order.StoreAlias;

        return ValueTask.CompletedTask;
    }
}
```

### Profile enrichers

Profile enrichers run before profile upsert and subscribe operations are dispatched. The current profile enricher contract receives the profile email and consent changes, which is useful for auditing, validation, or side effects that should happen alongside Klaviyo profile updates.

```csharp
using Ekom.Klaviyo.Enrichers.ProfilesEnricher;
using Ekom.Klaviyo.Models.Profiles;
using Microsoft.Extensions.Logging;

public sealed class ConsentAuditEnricher : IKlaviyoProfilesEnricher
{
    private readonly ILogger<ConsentAuditEnricher> _logger;

    public ConsentAuditEnricher(ILogger<ConsentAuditEnricher> logger)
    {
        _logger = logger;
    }

    public ValueTask EnrichAsync(
        string email,
        IReadOnlyList<KlaviyoProfileConsentChange>? consents,
        CancellationToken ct)
    {
        if (consents is { Count: > 0 })
        {
            _logger.LogInformation(
                "Klaviyo profile consent update for {Email} with {ConsentCount} consent changes.",
                email,
                consents.Count);
        }

        return ValueTask.CompletedTask;
    }
}
```


## Typical Production Setup

```json
"Klaviyo": {
  "Enabled": true,
  "PrivateApiKey": "<secure-secret>",
  "ApiBaseUrl": "https://a.klaviyo.com",
  "Revision": "2023-10-15",
  "SiteBaseUrl": "https://example.com",
  "Orders": {
    "Enabled": true,
    "TrackingPlacedOrders": true,
    "Dispatching": {
      "MaxBatchSize": 100,
      "FlushIntervalSeconds": 2,
      "MaxQueueSize": 10000
      }
    },
  "Catalog": {
    "Enabled": true,
    "ShowPrice": true,
    "ShowInventory": true,
    "SyncMode": "ApiPush",
    "DeleteMode": "Hard"
  }
}
```

[Link to documentation](https://vettvangur.gitbook.io/ekom/)

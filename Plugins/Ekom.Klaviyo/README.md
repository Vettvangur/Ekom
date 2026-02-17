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


## Tracking

```json
"Tracking": {
  "Enabled": true,
  "Search": true,
  "AddedToCart": true,
  "ViewedCategory": true,
  "ViewedProduct": true,
  "ActiveOnSite": true,
  "CheckoutStarted": true,
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


## Tracking Enrichers

Tracking events can be enriched before being mapped and dispatched. Implement `IKlaviyoTrackingEnricher` and register it with DI to add or modify properties on the tracking payload.

```csharp
using Ekom.Klaviyo.Enrichers.TrackingEnricher;
using Ekom.Klaviyo.Models.Tracking;

public sealed class MyTrackingEnricher : IKlaviyoTrackingEnricher
{
    public ValueTask EnrichAsync(KlaviyoTrackingEnrichmentContext context, CancellationToken ct = default)
    {
        if (context.Payload is KlaviyoViewedProductEvent e)
        {
            e.CustomProperties["source"] = "ekom";
        }

        return ValueTask.CompletedTask;
    }
}
```

Register your enricher in DI:

```csharp
services.AddSingleton<IKlaviyoTrackingEnricher, MyTrackingEnricher>();
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

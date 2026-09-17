<h1 align="center">
Ekom Klaviyo Plugin

[![Nuget](https://img.shields.io/nuget/vpre/Ekom.Klaviyo?color=ed0f0f)](https://www.nuget.org/packages/Ekom.Klaviyo/)
[![License](https://img.shields.io/badge/license-MIT-green)](../../LICENSE)

</h1>

# Klaviyo Configuration

This document describes the configuration options for the Klaviyo integration, including event tracking (orders) and catalog synchronization.

## Install

Choose the package that matches the Ekom/Umbraco application:

| Package | Target | Ekom dependency |
|-----|-----|-----|
| `Ekom.Klaviyo` | .NET 8 (Ekom.U10/Umbraco 13) or .NET 10 (Umbraco 17) | `Ekom.U10` or `Ekom.U17`, selected by target framework |
| `Ekom.Klaviyo.U18` | .NET 10 and Umbraco 18 | `Ekom.U18` |

```shell
dotnet add package Ekom.Klaviyo
# Umbraco 18:
dotnet add package Ekom.Klaviyo.U18
```

Register the services during application startup. This is required for both packages; the Umbraco composers register controllers and event handlers, not the service collection.

```csharp
using Ekom.Klaviyo;

builder.Services.AddKlaviyo();
```

The configuration is read from `Ekom:Klaviyo` in `appsettings.json` or an environment-specific configuration file.

---

## Root Configuration

```json
{
  "Ekom": {
    "Klaviyo": {
      "Enabled": true,
      "PrivateApiKey": "secret",
      "ApiBaseUrl": "https://a.klaviyo.com",
      "Revision": "2026-01-15",
      "ProfileExternalIdProperty": "email",
      "SiteBaseUrl": "https://vettvangur.is",
      "ImageBaseUrl": "https://images.vettvangur.is",
      "Testing": false,
      "Stores": [
        {
          "Alias": "Store"
        }
      ],
      "Orders": {},
      "Subscriptions": {},
      "Catalog": {},
      "Tracking": {}
    }
  }
}
```

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enabled` | `bool` | `true` | Master switch. The plugin is disabled during post-configuration if no stores or revision are configured. |
| `PrivateApiKey` | `string` | `null` | Global Klaviyo private API key used when a store does not override it. Current post-configuration also requires this global value to keep catalog synchronization enabled when orders are enabled or catalog mode is `ApiPush`; store-only keys are not sufficient for that check. |
| `ApiBaseUrl` | `string` | `https://a.klaviyo.com` | Klaviyo API base URL. |
| `Revision` | `string` | required | Klaviyo API revision header. |
| `ProfileExternalIdProperty` | `string` | `email` | Customer property used as the profile external ID; for example `email`, `phone`, `username`, or a custom customer property. |
| `SiteBaseUrl` | `string` | required | Public site base URL used to generate product URLs. |
| `ImageBaseUrl` | `string` | `null` | Optional public image base URL. Falls back to `SiteBaseUrl` when empty. |
| `Testing` | `bool` | `false` | Appends ` Test` to generated event names, for example `Placed Order Test`. |
| `Stores` | `array` | `[]` | Per-store configuration. At least one store is required; the first is the feed default when `storeAlias` is omitted. |
| `Orders` | `object` | see below | Order event configuration. |
| `Subscriptions` | `object` | see below | Profile subscription and list configuration. |
| `Catalog` | `object` | see below | Product catalog synchronization configuration. |
| `Tracking` | `object` | see below | Custom event tracking configuration. |


## Stores

```json
"Stores": [
  {
    "Alias": "Store",
    "PrivateApiKey": "store-specific-key",
    "ListId": "LIST_ID",
    "CheckoutUrl": "https://example.com/checkout"
  }
]
```

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Alias` | `string` | required | Store identifier; must match an Ekom store alias. |
| `PrivateApiKey` | `string` | `null` | API key override for this store. |
| `ListId` | `string` | `null` | Subscription list ID override for this store. |
| `CheckoutUrl` | `string` | `null` | Checkout URL included in placed-order and started-checkout payloads for this store. |

Use this when:

- Running multiple stores in a single application

- Each store uses a different Klaviyo account

## Orders

```json
"Orders": {
  "Enabled": true,
  "TrackingPlacedOrders": false,
  "Dispatching": {
    "MaxBatchSize": 100,
    "FlushIntervalSeconds": 2,
    "MaxQueueSize": 10000,
    "MaxConcurrency": 3
  }
}
```


| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enabled` | `bool` | `true` | Enables Klaviyo order event tracking. |
| `TrackingPlacedOrders` | `bool` | `false` | Enables automatic *Placed Order* tracking from the Ekom complete-checkout event. |
| `Dispatching` | `object` | see below | Background dispatching settings. |


| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `MaxBatchSize` | `int` | `100` | Maximum number of queued events processed per dispatch cycle. |
| `FlushIntervalSeconds` | `int` | `2` | Interval in seconds between dispatcher flushes. |
| `MaxQueueSize` | `int` | `10000` | Maximum number of queued events before backpressure applies. |
| `MaxConcurrency` | `int` | `3` | Maximum concurrent sends for a dispatcher. |

The same dispatcher defaults apply independently under `Orders`, `Subscriptions`, `Catalog`, and `Tracking`.


## Subscriptions

```json
"Subscriptions": {
  "Enabled": true,
  "DefaultListId": "LIST_ID",
  "Dispatching": {
    "MaxBatchSize": 100,
    "FlushIntervalSeconds": 2,
    "MaxQueueSize": 10000,
    "MaxConcurrency": 3
  }
}
```

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enabled` | `bool` | `true` | Enables profile upsert and subscription operations. |
| `DefaultListId` | `string` | `null` | Global list ID used when no explicit or store list is set. |
| `Dispatching` | `object` | see dispatcher defaults above | Background dispatching settings. |

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
  "ShowInventory": false,
  "InventoryPolicy": 2,
  "ImageCrop": "",
  "Username": "feed-user",
  "Password": "feed-password",
  "Dispatching": {
    "MaxBatchSize": 100,
    "FlushIntervalSeconds": 2,
    "MaxQueueSize": 10000,
    "MaxConcurrency": 3
  },
  "SyncMode": "FeedPull",
  "DeleteMode": "Soft"
}
```

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enabled` | `bool` | `true` | Enables catalog synchronization. |
| `ShowPrice` | `bool` | `true` | Includes product prices in feed items. |
| `ShowInventory` | `bool` | `false` | Includes inventory levels in feed items. |
| `Username` | `string` | `null` | Optional HTTP Basic username for the feed endpoint. |
| `Password` | `string` | `null` | Optional HTTP Basic password for the feed endpoint. Authentication is required if either value is set. |
| `InventoryPolicy` | `int` | `2` | Inventory handling policy used by feed mapping. |
| `ImageCrop` | `string` | `""` | Query string appended to product image URLs. |
| `Dispatching` | `object` | see dispatcher defaults above | Background dispatching settings for API push. |
| `SyncMode` | `string` | `FeedPull` | Catalog sync strategy (`ApiPush` or `FeedPull`). |
| `DeleteMode` | `string` | `Soft` | API-push product deletion behavior (`Hard` or `Soft`). |

With `FeedPull`, Klaviyo reads `GET /ekom/klaviyo/product/feed?storeAlias=Store&culture=en-US`. Both query parameters are optional: the first configured store and its default culture are used when omitted. The endpoint is available only when the plugin and catalog are enabled and `SyncMode` is `FeedPull`; responses are cached for 60 minutes. Configure Klaviyo feed credentials from `Catalog:Username` and `Catalog:Password` when Basic authentication is required.

Feed pull uses the current Ekom product price for `price`. When a product discount is active, the original price, discount price, discount amount, and discount state are also included in `custom_attributes`.

Projects can customize feed items by registering one or more `IKlaviyoProductFeedItemEnricher` implementations. Enrichers run before feed serialization and can update the mapped item or its custom attributes.

Projects can also replace the product source for feed pull by subscribing to `KlaviyoProductFeedEvents.ProductFeedProductsLoadingAsync`. The event runs before Ekom fetches all products for `/ekom/klaviyo/product/feed`. Set `Handled = true` and assign `Products` to skip the default `GetAllProductsAsync` call. If `Handled` is not set, the default product fetch still runs.

```csharp
using Ekom.Klaviyo.Events;
using Ekom.Models;

KlaviyoProductFeedEvents.ProductFeedProductsLoadingAsync += async (args, ct) =>
{
    if (!args.StoreAlias.Equals("Store", StringComparison.OrdinalIgnoreCase))
        return;

    IEnumerable<IProduct> products = await LoadKlaviyoProductsAsync(args.Store, args.Culture, ct);

    args.Products = products;
    args.Handled = true;
};
```


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
    "MaxQueueSize": 10000,
    "MaxConcurrency": 3
  }
}
```

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enabled` | `bool` | `true` | Enables custom event tracking. |
| `Search` | `bool` | `false` | Enables *Search* tracking. |
| `AddedToCart` | `bool` | `false` | Enables *Added to Cart* tracking. |
| `ViewedCategory` | `bool` | `false` | Enables *Viewed Category* tracking. |
| `ViewedProduct` | `bool` | `false` | Enables *Viewed Product* tracking. |
| `ActiveOnSite` | `bool` | `false` | Enables *Active on Site* tracking. |
| `StartedCheckout` | `bool` | `false` | Enables *Started Checkout* tracking. |
| `Dispatching` | `object` | see dispatcher defaults above | Background dispatching settings. |

## Public services

`AddKlaviyo` registers these scoped entry points:

- `IKlaviyoProfilesService` upserts profiles, subscribes or unsubscribes consent, and looks profiles and list memberships up by ID, email, or phone number.
- `IKlaviyoTrackingService` queues the supported tracking events when their individual switches are enabled.
- `IKlaviyoOrderService` queues placed orders. Its fulfilled, cancelled, and refunded methods are currently present on the interface but are not implemented.
- `IKlaviyoEventService` sends project-specific or raw events directly and only requires the root `Enabled` switch.


## Generic Events

Use `IKlaviyoEventService` to send project-specific events that are not built into the package. Generic events require a store alias and only depend on the global `Ekom:Klaviyo:Enabled` setting; they do not require `Tracking:Enabled` to be enabled.

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
{
  "Ekom": {
    "Klaviyo": {
      "Enabled": true,
      "PrivateApiKey": "<secure-secret>",
      "ApiBaseUrl": "https://a.klaviyo.com",
      "Revision": "2026-01-15",
      "SiteBaseUrl": "https://example.com",
      "ImageBaseUrl": "https://images.example.com",
      "Stores": [
        {
          "Alias": "Store",
          "CheckoutUrl": "https://example.com/checkout"
        }
      ],
      "Orders": {
        "Enabled": true,
        "TrackingPlacedOrders": true,
        "Dispatching": {
          "MaxBatchSize": 100,
          "FlushIntervalSeconds": 2,
          "MaxQueueSize": 10000,
          "MaxConcurrency": 3
        }
      },
      "Catalog": {
        "Enabled": true,
        "ShowPrice": true,
        "ShowInventory": false,
        "SyncMode": "ApiPush",
        "DeleteMode": "Soft"
      }
    }
  }
}
```

See the [Ekom documentation](../../docs/README.md) and [Ekom product website](https://www.ekomcommerce.com/) for core platform guidance.

[Ekom docs](../README.md) / [Reference](README.md) / Services and dependency injection

# Services and dependency injection

Ekom registers its classes with ASP.NET Core dependency injection when the site starts. This page lists the public services you can inject, their lifetimes, the ones you can replace with your own implementation, and how to register a replacement.

For everyday work, use the classes in `Ekom.API`, described in the [.NET API](dotnet-api.md). The services on this page are for integrations and custom behavior.

On this page:

- [How Ekom is registered](#how-ekom-is-registered)
- [Register your own implementation](#register-your-own-implementation)
- [Services you can inject](#services-you-can-inject)
- [Services you can replace](#services-you-can-replace)
- [Add actions to the manager order view](#add-actions-to-the-manager-order-view)
- [Reserve stock](#reserve-stock)
- [Filter product lists](#filter-product-lists)
- [Background services](#background-services)

## How Ekom is registered

The Ekom package for Umbraco 17, 18 and 19 contains a composer, `EkomComposer`, that Umbraco runs when it builds the site. The composer calls `AddEkom`, which:

- adds a distributed memory cache and session support,
- registers Ekom's core services, options and caches,
- registers the Umbraco implementations of content, search, security, URL, member, import and cache services.

Do not call `AddEkom` yourself. Umbraco already runs the composer.

Ekom also adds its middleware through a startup filter, in this order, before your own pipeline: the request size limit, request body buffering, `UseEkomMalformedFormGuard`, session, authentication, authorization, `UseEkomMiddleware` and `UseEkomTrackingMiddleware`. You do not need to call these methods. Ekom maps its controllers after your pipeline.

## Register your own implementation

Register your implementation in `Program.cs` after the line that builds Umbraco (`.Build()` on the Umbraco builder) and before `builder.Build()`. Umbraco runs Ekom's composer inside that first `.Build()`, so your registration comes later. When a service has more than one registration, ASP.NET Core injects the last one.

```csharp
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

// Your replacements and additions come after Umbraco and Ekom are registered.
builder.Services.AddSingleton<ICheckoutStockPolicy, WholesaleStockPolicy>();
builder.Services.AddScoped<ICatalogSearchService, AlgoliaCatalogSearchService>();
builder.Services.AddTransient<IOrderManagerActionProvider, InvoiceActionProvider>();

WebApplication app = builder.Build();
```

Keep the lifetime of the service you replace. Do not inject a transient or scoped service into a singleton. `Configuration.Resolver` is the root service provider: code that needs the current request should use normal injection instead.

## Services you can inject

| Service | Lifetime | Use it to |
| --- | --- | --- |
| `Ekom.API.Catalog`, `Store`, `Order`, `Providers`, `Discounts`, `Stock`, `Warehouse` | Transient | Everyday work. See [.NET API](dotnet-api.md). |
| `Ekom.API.Prices` | Singleton | Database price overrides. |
| `Ekom.Configuration` | Singleton | Read Ekom's settings. See [Configuration reference](configuration-reference.md). |
| `IOptions<EkomOptions>` | Options | Manager and Variant App settings. Validated at startup. |
| `IOptions<StockReservationOptions>`, `IOptions<TrackingOptions>`, `IOptions<OrderDiscountCalculationOptions>` | Options | Reservation, tracking and order-discount integration settings. |
| `DiscountEvents` | Singleton | Subscribe to [discount events](events.md#discount-events). |
| `IStoreService` | Transient | Find stores by alias, domain or request, set the request's store, read domains. |
| `IOrderPayments` | Transient | Create and record payments, and read an order's payments. See [Payments](../guides/payments.md). |
| `IOrderShipments` | Transient | Create and record shipments, and read an order's shipments. See [Shipping and shipments](../guides/shipping.md). |
| `IOrderStateService` | Transient | Change an order's order, payment or fulfillment status, and move older orders to the three-status model. |
| `IOrderActivityLogService` | Transient | Write and read an order's activity log. |
| `IOrderReviewService` | Transient | Raise, list and resolve order review issues. See [Order reviews](../guides/order-reviews.md). |
| `IOrderDiscountCalculationService` | Transient | Price a coupon for a list of SKUs without a basket. |
| `IStockReservationService` | Singleton | Reserve, use up and release stock with explicit results. |
| `IWarehouseDefinitionService` | Transient | Read a store's published warehouses. |
| `IManagerAccessService` | Scoped | Check whether the current backoffice user may open the manager or a store. |
| `ICacheRefreshService` | Singleton | Rebuild Ekom's caches. `Store.RefreshCache()` calls it. |
| `IMailService` | Transient | Send an email through Umbraco's SMTP settings. |
| `INodeService`, `IMetafieldService`, `IUmbracoService`, `IMemberService`, `ISecurityService`, `IImportService` | See below | Ekom's access to Umbraco content, metafields, members, the backoffice user and imports. |
| `CheckoutControllerService` | Transient | The checkout payment pipeline. Prefer `Order.PayAsync`. |
| `RevalidateService`, `ControllerRequestHelper` | Scoped | Used by Ekom's controllers and headless revalidation. |

`INodeService`, `IMetafieldService`, `IUmbracoService`, `IMemberService` and `IImportService` are transient; `ISecurityService` is scoped. They are implemented in the Umbraco package, which compiles the same source for Umbraco 17, 18 and 19.

Most of the classes behind these services, such as `OrderService` and `CheckoutService`, are internal. You cannot inject them.

### Method signatures

```csharp
public interface IStoreService
{
    IEnumerable<IStore> GetAllStores();
    IStore? GetStoreByAlias(string? alias);
    IStore? GetStoreByDomain(string domain = "", string? culture = null);
    IStore? GetStoreFromCache();
    IStore? SetStore(string storeAlias);
    IEnumerable<UmbracoDomain> GetDomains();
}

public interface IOrderActivityLogService
{
    Task AddOrderLogAsync(Guid orderId, string message, string? userName = null,
        OrderActivityLogType logType = OrderActivityLogType.Info, CancellationToken ct = default);
    Task<IReadOnlyList<OrderActivityLogEntry>> GetOrderLogsAsync(Guid orderId, CancellationToken ct = default);
}

public interface IOrderDiscountCalculationService
{
    Task<OrderDiscountCalculationResult> CalculateByCouponAsync(
        OrderDiscountCalculationRequest request, CancellationToken ct = default);
}
```

`GetStoreFromCache` returns the store of the current request.

`AddOrderLogAsync` throws `ArgumentException` for an empty order key or message. When `userName` is empty, the entry is written as `Customer`. The returned task completes when the entry is queued, not when it is saved: a background service writes queued entries about every half second, in batches of up to 50. The queue holds 1,000 entries; when it is full, the call waits.

`CalculateByCouponAsync` is also available over HTTP. See [Order-discount integration](http-api.md#order-discount-integration).

## Services you can replace

| Service | Default | Lifetime | Replace it to |
| --- | --- | --- | --- |
| `ICheckoutStockPolicy` | Lines need stock unless the product has backorder turned on. | Singleton | Decide per line whether checkout checks and reserves stock. See [Stock](../guides/stock.md#changing-which-lines-need-stock). |
| `ICatalogSearchService` | Examine search in the Umbraco package. | Scoped | Use another search engine for catalog search. |
| `IProductFilterService` | `ProductFilterService`, which changes nothing. | Transient | Filter product lists with your own rules. See [Filter product lists](#filter-product-lists). |
| `IGiftcardValidator` | None registered. | Your choice | Turn on gift cards and decide what a code is worth. Without it, `POST /ekom/order/giftcards` returns `403`. |
| `IOrderManagerActionProvider` | Ekom registers one for releasing reservations. | Transient | Add actions to the manager. Add a registration; do not replace. |

```csharp
public interface ICheckoutStockPolicy
{
    bool RequiresStock(IOrderInfo order, IOrderLine line);
}

public interface ICatalogSearchService
{
    Task<(IEnumerable<SearchResultEntity> Results, long Total)> PublicQueryAsync(SearchRequest req, CancellationToken ct = default);
    Task<(IEnumerable<SearchResultEntity> Results, long Total)> InternalQueryAsync(SearchRequest req, CancellationToken ct = default);
    Task<(IEnumerable<int> Ids, long Total)> ProductQueryAsync(SearchRequest req, CancellationToken ct = default);
}

public interface IGiftcardValidator
{
    Task<Giftcard?> ValidateAsync(string code, IOrderInfo order, CancellationToken ct = default);
}
```

Ekom calls `ICheckoutStockPolicy` when it prepares checkout and again when it completes the order. The second call can happen without an HTTP request, for example from a payment callback. Base the decision on data saved with the order.

`IGiftcardValidator.ValidateAsync` returns `null` to reject a code. Ekom also rejects a gift card with an amount of `0` or less, or an empty code.

## Add actions to the manager order view

A manager action is a button on an order in the manager, for example "Print shipping label". Register a class that implements `IOrderManagerActionProvider`:

```csharp
public interface IOrderManagerActionProvider
{
    Task<IReadOnlyCollection<OrderManagerAction>> GetActionsAsync(IOrderInfo orderInfo, CancellationToken ct = default);
    Task<OrderManagerActionExecutionResult?> ExecuteAsync(IOrderInfo orderInfo, string actionKey, string? userName = null, CancellationToken ct = default);
}
```

- `GetActionsAsync` returns the actions that apply to the order.
- `ExecuteAsync` returns `null` when the action key is not yours. Ekom asks each provider in turn until one returns a result.
- A result can be a success message, a bad request or a file to download.
- After a result that is not a bad request, Ekom raises [`OrderManagerEvents.ActionExecutedAsync`](events.md#manager-action-events).

`IOrderManagerActionService` combines all registered providers. The manager uses it; you do not need to call it.

## Reserve stock

`IStockReservationService` holds stock for a customer while they pay. It is a singleton. Guide: [Stock reservations](../guides/reservations.md).

| Method | What it does |
| --- | --- |
| `ReserveAsync(StockReservationRequest request, CancellationToken ct = default)` | Takes the quantity from stock and creates an active reservation in one step. Supports an idempotency key, order and payment attempt. |
| `ConsumeAsync(string reservationId, CancellationToken ct = default)` | Ends an active reservation and keeps the stock deducted, for a completed sale. |
| `ReleaseAsync(string reservationId, CancellationToken ct = default)` | Ends an active reservation and returns the stock. |
| `ExpireAsync(string reservationId, CancellationToken ct = default)` | Expires a reservation that is due and returns the stock. |
| `ExpireDueAsync(int batchSize, CancellationToken ct = default)` | Expires due reservations. The background worker calls it. |
| `CleanupAsync(DateTime completedBeforeUtc, int batchSize, CancellationToken ct = default)` | Deletes finished reservations older than the given time. |

Every method returns a `StockReservationResult`. Check its `Status`: calling an operation again returns statuses such as `AlreadyExists`, `AlreadyConsumed` and `AlreadyReleased` instead of throwing. `LateConsumption` means you tried to use up a reservation after its expiry time: Ekom expires it and returns the stock instead, and you must reconcile the sale.

## Filter product lists

`IProductFilterService` runs on every product list that Ekom builds with a `ProductQuery`, after the query's own filters and search, and before the [catalog events](events.md#catalog-events). The default `ProductFilterService` returns the products unchanged. Inherit from it and override the method you need.

To filter by store, override the overload that takes the store alias. Ekom passes the store the products were loaded for, even when the query is `null` or has no `StoreAlias`:

```csharp
using Ekom.Models;
using Ekom.Services;

public sealed class OutletFilterService : ProductFilterService
{
    public override Task<IEnumerable<IProduct>> ApplyFiltersAsync(
        IEnumerable<IProduct> products,
        ProductQuery? query,
        ICategory? category,
        string storeAlias,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (storeAlias == "outlet")
        {
            products = products.Where(product => product.Available);
        }

        return base.ApplyFiltersAsync(products, query, category, storeAlias, ct);
    }
}
```

- The store-alias overloads call the older overloads without a store alias by default, so existing overrides keep working.
- The store-alias async overload has no default for the cancellation token. Pass `CancellationToken.None` when you have none.
- When `ProductQuery.RaiseEvents` is `false`, the filter service does not run.
- Calls that cannot determine a store use the overloads without a store alias. Ekom does not guess the store from the query or the products.
- To build a `ProductResponse` yourself with the store context, use `new ProductResponse(products, query, filterService, category, storeAlias)` or `ProductResponse.CreateAsync(products, query, filterService, category, storeAlias, ct)`.

## Background services

Ekom runs these hosted services on every server:

- the reservation worker, which expires and cleans up reservations (settings under `Ekom:Reservations`),
- the activity log writer,
- the GA4 and Meta tracking senders,
- a migration that copies payments stored by older Ekom versions onto their orders after startup. A failure is logged and tried again at the next startup.

Ekom's caches of stores, categories, products, variants, providers, discounts and stock are singletons. Treat the objects they return as read-only. To rebuild them, call `ICacheRefreshService.RefreshCache()` or `Store.RefreshCache()`; do not change the cached objects.

## Related pages

- [.NET API](dotnet-api.md): the classes most code should use.
- [Events](events.md): run code at fixed points without replacing a service.
- [Configuration reference](configuration-reference.md): the settings behind the options classes.
- [Stock reservations](../guides/reservations.md): how reservations work at checkout.
- [Stock](../guides/stock.md): an example of a custom `ICheckoutStockPolicy`.

[Back to Reference](README.md)

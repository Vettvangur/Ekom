# Services and Dependency Injection

Ekom registers its facades and supporting services with ASP.NET Core DI. This page describes the public extension point and the contracts intended for application code. See [.NET API reference](dotnet-api.md) for the higher-level `Ekom.API` facades.

## Registration

The installed Umbraco package discovers an Ekom composer during the normal composition pipeline. That composer calls Ekom's registration extension, adds session/distributed memory support, and registers the Umbraco implementations of content, search, security, URL, member, import, cache refresh, and metafield services. Do not call `AddEkom` manually in a standard package installation; it is an implementation/custom-host API.

The package also exposes middleware helpers: `UseEkomMiddleware`, `UseEkomTrackingMiddleware`, and `UseEkomMalformedFormGuard`. Package startup determines their normal placement.

## Facade lifetimes

The following concrete facades are transient and injectable:

- `Ekom.API.Catalog`
- `Ekom.API.Store`
- `Ekom.API.Order`
- `Ekom.API.Providers`
- `Ekom.API.Discounts`
- `Ekom.API.Stock`
- `Ekom.API.Warehouse`

`OrderService`, `CheckoutService`, `CheckoutControllerService`, `ProductDiscountService`, repositories, `IStoreService`, `IWarehouseDefinitionService`, `IOrderDiscountCalculationService`, `IOrderActivityLogService`, `IOrderManagerActionService`, and `IProductFilterService` are also transient. `RevalidateService`, `ControllerRequestHelper`, the exception filter, search/security/manager access implementations, and cache initialization are scoped. Catalog/stock caches, reservation infrastructure, dispatchers, `DiscountEvents`, configuration, and cache refresh are singleton services.

Do not capture a transient or scoped service in a singleton. `Configuration.Resolver` is a root provider; request-dependent code should use normal injection rather than resolving from it.

## Store and catalog contracts

### `IStoreService`

Provides `GetAllStores`, alias/domain/current-store lookup, `SetStore`, and `GetDomains`. Use it when building a lower-level service; use `Ekom.API.Store` in ordinary application code.

### `ICatalogSearchService`

Implemented by the installed Umbraco package and used by catalog search:

```csharp
Task<(IEnumerable<SearchResultEntity> Results, long Total)> PublicQueryAsync(SearchRequest request, CancellationToken ct);
Task<(IEnumerable<SearchResultEntity> Results, long Total)> InternalQueryAsync(SearchRequest request, CancellationToken ct);
Task<(IEnumerable<int> Ids, long Total)> ProductQueryAsync(SearchRequest request, CancellationToken ct);
```

The implementation is scoped. Replace it after Ekom registration when integrating another search backend.

### `IProductFilterService`

Applies `ProductQuery` filtering, sorting, paging inputs, and category context through sync and async methods. `ProductResponse` and the catalog facade use it internally.

### Content abstractions

`INodeService`, `IMetafieldService`, `IUrlService`, `IUmbracoService`, `IMemberService`, `ISecurityService`, and `IImportService` isolate core behavior from Umbraco-version-specific implementations. Their concrete registrations are supplied by `Ekom.U10`, `Ekom.U17`, or `Ekom.U18`; U18 compiles the shared U17 implementation where appropriate. Application code should inject interfaces.

## Order and checkout contracts

### `IOrderDiscountCalculationService`

`CalculateByCouponAsync(OrderDiscountCalculationRequest, CancellationToken)` calculates a coupon against caller-supplied SKU lines without mutating a cart. It is also exposed by the authenticated integration endpoint documented in [HTTP API](http-api.md#order-discount-integration).

### `IOrderActivityLogService`

```csharp
Task AddOrderLogAsync(Guid orderId, string message, string? userName = null,
    OrderActivityLogType logType = OrderActivityLogType.Info, CancellationToken ct = default);
Task<IReadOnlyList<OrderActivityLogEntry>> GetOrderLogsAsync(Guid orderId, CancellationToken ct = default);
```

Writes are dispatched through a singleton hosted dispatcher; consumers should not assume another process sees a new entry before the returned task completes and persistence has run.

### Manager actions

`IOrderManagerActionService` aggregates registered `IOrderManagerActionProvider` implementations. Both contracts expose `GetActionsAsync` and `ExecuteAsync`. Register provider implementations as `IOrderManagerActionProvider`; an unknown action returns `null`. Results may be success messages, bad requests, or downloadable files. After a successful action, `OrderManagerEvents.ActionExecutedAsync` runs with the order, action key, backoffice user name, and execution result. Subscribers can filter by action key, such as a shipping-label action.

### `ICheckoutStockPolicy`

`RequiresStock(IOrderInfo order, IOrderLine line)` decides whether checkout reserves/validates a line. The default returns false only for backordered products. Ekom uses `TryAddSingleton`, so register a custom singleton before Umbraco runs package composition to replace it. Implementations must use durable order data because completion may run without an HTTP request.

### Checkout implementation services

`CheckoutService` completes orders. `CheckoutControllerService` parses/coordinates payment flows and returns `CheckoutResponse`. They are concrete transient services, but most application code should call `Order.CompleteOrderAsync` or `Order.PayAsync` so order invariants and events remain centralized.

## Stock contracts

### `IStockReservationService`

This singleton is the explicit native-reservation API:

| Method | Meaning |
| --- | --- |
| `ReserveAsync(request)` | Atomically reserves available stock; supports idempotency/order/payment-attempt metadata. |
| `ConsumeAsync(id)` | Makes an active hold terminal without restoring stock. |
| `ReleaseAsync(id)` | Releases an active hold and restores stock. |
| `ExpireAsync(id)` | Expires a due hold and restores stock. |
| `ExpireDueAsync(batchSize)` | Worker operation for due reservations. |
| `CleanupAsync(completedBeforeUtc, batchSize)` | Deletes retained terminal records. |

Inspect `StockReservationResult.Status`; repeat operations deliberately return statuses such as `AlreadyExists`, `AlreadyConsumed`, and `AlreadyReleased`. `LateConsumption` requires caller reconciliation. See [Stock reservations](../guides/reservations.md).

### Warehouse services

`IWarehouseDefinitionService.GetPublishedWarehouses(storeAlias)` reads warehouse definitions from content. `IWarehouseStockRepository` is the persistence contract behind `Ekom.API.Warehouse`; prefer the facade unless replacing persistence.

## Cache and hosted infrastructure

Ekom registers singleton per-store caches for products, categories, variants, providers, discounts, and stock. Treat returned domain objects as read models; refresh through `ICacheRefreshService`/`Ekom.API.Store.RefreshCache`, not by mutating cache dictionaries.

Hosted services process reservation expiry, order activity logs, and enabled tracking dispatch. Their options are documented in [Configuration reference](configuration-reference.md).

## Custom registration examples

Register a checkout stock policy before the call that builds the Umbraco composition pipeline:

```csharp
builder.Services.AddSingleton<ICheckoutStockPolicy, MyCheckoutStockPolicy>();
```

Register replacement services and manager action providers after the Umbraco builder's `.Build()` has run package composition but before `builder.Build()` creates the web application:

```csharp
builder.Services.AddScoped<ICatalogSearchService, CustomCatalogSearchService>();
builder.Services.AddTransient<IOrderManagerActionProvider, ExportInvoiceActionProvider>();
```

## Related reference

- [.NET API](dotnet-api.md)
- [Events](events.md)
- [Configuration](configuration-reference.md)
- [Models and behavior](models-and-behavior.md)

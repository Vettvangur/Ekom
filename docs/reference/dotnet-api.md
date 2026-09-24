# .NET API Reference

This page covers the in-process C# facades in `Ekom.API`. These are registered with dependency injection and are not HTTP endpoints. For remote or browser clients, use the separate [HTTP API reference](http-api.md).

## Accessing the facades

Prefer constructor injection:

```csharp
using Ekom.API;

public sealed class CartService(Order order, Catalog catalog)
{
    public Task<IOrderInfo> AddAsync(Guid productKey, string storeAlias, CancellationToken ct)
        => order.AddOrderLineAsync(productKey, 1, storeAlias, ct: ct);
}
```

`Catalog`, `Store`, `Order`, `Providers`, `Discounts`, `Stock`, and `Warehouse` also expose a static `Instance`. It resolves through Ekom's service provider; injection gives clearer lifetimes and is safer for request-dependent code. `Order.Instance` explicitly prefers the current request service provider.

Most facades are transient. Methods without a `storeAlias` usually resolve the active store from the current request. Supply an alias in background work and other code without an HTTP context.

## Catalog

`Ekom.API.Catalog` reads the cached catalog. Product and category methods raise [catalog events](events.md#catalog-events) unless their `raiseEvent` argument is `false` or a `ProductQuery` disables events.

| Operation | Methods |
| --- | --- |
| Current routed item | `GetProduct[Async](bool raiseEvent = true)`, `GetCategory[Async](bool raiseEvent = true)` |
| Product lookup | `GetProduct[Async](string sku, ...)`, `GetProduct[Async](Guid key, ...)`, `GetProduct[Async](int id, ...)`, `GetProductByRoute[Async](string route, ...)` |
| Product lists | `GetAllProducts[Async](...)`, `GetProductsByIds[Async](...)`, `GetProductsByKeys[Async](...)`, `GetProductsBySkus[Async](...)` |
| Category products | `GetProductsRescursiveByRoute[Async](...)`; the public method name is intentionally spelled `Rescursive` |
| Category lookup | `GetCategory[Async](string/int/Guid id, ...)`, `GetCategoryByRoute[Async](...)` |
| Category lists | `GetRootCategories[Async](...)`, `GetAllCategories[Async](...)`, `GetCategoriesByIds[Async](...)`, `GetCategoriesByKeys[Async](...)` |
| Variants | `GetVariant[Async](Guid/int/string sku, ...)`, `GetVariantsByGroup[Async](int groupId, ...)` |
| Variant groups | `GetVariantGroup[Async](Guid/int, ...)` |
| Related products | `GetRelatedProducts[Async](Guid, count = 4, ...)`, `GetRelatedProductsBySku[Async](string, count = 4, ...)` |
| Search and metadata | `ProductSearchAsync(SearchRequest, ...)`, `GetMetafields()` |

Product lookup's nullable `global` argument falls back to `Ekom:GlobalCatalog`. Category lookup uses its `global` argument or that setting. Route lookup stays in the selected store. Collection methods return `ProductResponse`, which applies filtering, sorting, paging, and events.

```csharp
var result = await catalog.GetAllProductsAsync(new ProductQuery
{
    StoreAlias = "store",
    Page = 1,
    PageSize = 24,
    OrderBy = OrderBy.DateDesc,
    MetaFilters = new() { ["brand"] = ["Ekom"] },
}, ct);
```

The older store-first variant overloads are marked obsolete; use the identifier-first overloads shown above.

## Store

`Ekom.API.Store` exposes store and domain context:

| Method | Behavior |
| --- | --- |
| `GetStore()` | Returns the request store, or the service's fallback store. |
| `GetStore(string? storeAlias)` | Resolves an alias. |
| `GetStoreByDomain(string domain, string culture)` | Resolves a domain/culture mapping. |
| `GetAllStores()` | Returns configured stores. |
| `GetDomains()` | Returns known Umbraco domain mappings. |
| `SetStore(string storeAlias)` | Changes the active request store. |
| `RefreshCache()` | Rebuilds Ekom caches; use for administration, not normal reads. |

## Order and checkout

`Ekom.API.Order` is the cart/order mutation facade. Its asynchronous methods should be preferred over synchronous wrappers.

### Read and lifecycle

| Operation | Methods |
| --- | --- |
| Current cart | `GetOrderAsync()`, `GetOrderAsync(string? storeAlias)` |
| Any order by key | `GetOrderAsync(Guid uniqueId)`; may return a final order and is not a cart-only lookup |
| Completed current order | `GetCompletedOrderAsync(string storeAlias)` |
| Query by status/customer | `GetStatusOrdersAsync`, `GetStatusOrdersByCustomerIdAsync`, `GetStatusOrdersByCustomerUsernameAsync`, `GetCompleteCustomerOrdersAsync` |
| Status | `UpdateStatusAsync(...)`, `IsOrderFinal(OrderStatus?)` |
| Completion | `CompleteOrderAsync(Guid)`, `ClearCustomerOrderReferenceAsync(Guid, ...)` |
| Rebuild lines | `ReInitializeOrder(string, ...)` |
| Activity | `AddActivityLogAsync(Guid, message, userName, logType, ...)` |

`IsOrderFinal` returns true for `OfflinePayment`, `Pending`, dispatch/pickup-ready states, `Dispatched`, `Returned`, `Cancelled`, and `Closed`.

### Lines and order data

| Operation | Methods |
| --- | --- |
| Add | `AddOrderLineAsync(Guid productId, decimal quantity, string storeAlias, AddOrderSettings? settings = null, ...)` |
| Add linked group | `AddLinkedOrderLinesAsync(..., IReadOnlyCollection<LinkedOrderLineRequest>, ...)` |
| Remove | `RemoveOrderLineAsync(Guid lineId, ...)`, `RemoveOrderLineProductAsync(Guid productKey, ...)` |
| Set quantity | `UpdateOrderlineQuantityAsync(Guid lineId, decimal quantity, ...)` |
| Merge existing line metadata | `UpdateOrderLineMetadataAsync(Guid orderId, IReadOnlyCollection<OrderLineMetadataUpdate> updates, OrderSettings? settings = null, ...)` |
| Gift cards | `AddGiftcardAsync(...)`, `RemoveGiftcardAsync(...)` |
| Customer/tracking | `UpdateCustomerInformationAsync(...)`, `UpdateTrackingAsync(...)` |
| Providers | `UpdateShippingInformationAsync(...)`, `UpdatePaymentInformationAsync(...)` |
| Currency | `UpdateCurrencyAsync(...)` |
| Cookie | `DeleteOrderCookie(...)`, `EnsureOrderCookie(...)` |

`AddOrderSettings.OrderAction` defaults to `AddOrUpdate`; `VariantKey`, `CustomData`, consent, tracking, and an Algolia query ID can accompany the line. Linked lines are created atomically with their parent. See [Linked order lines](../guides/linked-order-lines.md).

`UpdateOrderLineMetadataAsync` merges `orderline*` string properties into existing lines identified by **line GUID**, including lines on completed orders. It validates every entry before a single metadata-only database update; duplicate or missing line IDs and invalid property keys reject the whole batch. Existing properties not supplied are retained. It does not recalculate prices, stock, discounts, or providers. When events are enabled, it sends one order-updated notification (not per-line events). If the stored order changes between the read and write, the update fails with a conflict; reload the order before retrying. Pass the order ID rather than `OrderSettings.OrderInfo`.

```csharp
using Ekom.Models;

var updates = new[]
{
    new OrderLineMetadataUpdate
    {
        LineId = firstLine.Key,
        Properties = new Dictionary<string, string> { ["orderlineWarehouse"] = "North" },
    },
    new OrderLineMetadataUpdate
    {
        LineId = secondLine.Key,
        Properties = new Dictionary<string, string> { ["orderlineWarehouse"] = "South" },
    },
};

await order.UpdateOrderLineMetadataAsync(orderId, updates, new OrderSettings { FireEvents = false }, ct);
```

The U17/U18 manager displays nonempty `OrderLineInfo.Properties` values on each order line; the `orderline` prefix is removed from display labels.

### Coupons

`ApplyCouponToOrderAsync`, `RemoveCouponFromOrderAsync`, `SetCouponCodeAsync`, `ApplyCouponToOrderLineAsync`, and `RemoveCouponFromOrderLineAsync` mutate cart discounts. Coupon administration is available through `InsertCouponCodeAsync`, `GenerateCouponCodesAsync`, `RemoveCouponCodeAsync`, and `GetCouponsForDiscountAsync`.

Coupon codes are normalized to lower case for application. Applying a missing or exhausted coupon throws a discount exception rather than returning false.

### Payment and reservations

`PayAsync(PaymentRequest, string storeAlias, Guid orderId, ...)` and its `IOrderInfo` overload enter the checkout payment pipeline and return `CheckoutResponse`.

`AddReservationsToOrderAsync` validates and associates reservation IDs with an order; `RemoveReservationsFromOrderAsync` only clears the association. The `AddHangfireJobsToOrderAsync` and `RemoveHangfireJobsFromOrderAsync` names are compatibility wrappers for native SQL reservations, not evidence that current reservations use Hangfire. See [Stock reservations](../guides/reservations.md).

## Providers

`Ekom.API.Providers` reads payment/shipping providers, countries, and zones.

| Method | Behavior |
| --- | --- |
| `GetShippingProviders[Async](store, countryCode, orderAmount, ...)` | Returns providers sorted by `SortOrder`, filtered by zone and amount range. |
| `GetPaymentProviders[Async](store, countryCode, orderAmount, ...)` | Same for payment providers. A three-argument async-stream overload also exists. |
| `GetShippingProvider[Async](Guid, IStore/string? store, ...)` | Reads one provider from the selected store cache. |
| `GetPaymentProvider[Async](Guid, IStore/string? store, ...)` | Reads one payment provider. |
| `GetAllCountries()` | Returns repository countries. |
| `GetAllZones()` | Returns cached zones. |

Country filtering is applied only for a two-letter code. Amount filtering is applied only when `orderAmount > 0`; an end range of zero means no upper limit. Provider events run after these filters.

## Discounts

`Ekom.API.Discounts` is a read-only facade:

- `GetDiscounts()` and `GetDiscounts(string storeAlias)` return store discounts.
- `GetGlobalDiscounts()` and `GetGlobalDiscounts(string storeAlias)` retain entries whose `GlobalDiscount` is true.

Use `Order` for applying coupons and `Stock` for discount/coupon inventory.

## Stock

`Ekom.API.Stock` manages the primary product/variant stock pool.

| Operation | Methods and behavior |
| --- | --- |
| Read | `GetStock(Guid[, storeAlias])`, `GetStockData(Guid[, storeAlias])`; a missing entry is represented as zero-valued `StockData`. |
| Validate | `ValidateOrderStockAsync(IOrderInfo)` skips backordered products and applies stock buffers. |
| Increment | `IncrementStockAsync(...)` performs an atomic delta and publishes `StockChangedAsync`. |
| Set | `SetStockAsync(...)` replaces the balance; prefer increment for concurrent deltas. |
| Reserve | `ReserveStockAsync(...)` accepts a negative compatibility value and returns a reservation ID. |
| Consume/release wrappers | `CancelRollback` consumes; `RollbackJobAsync` and `CompleteRollback` release. Prefer `IStockReservationService` for explicit async semantics. |
| Discount stock | `GetDiscountStock[Data]Async`, `UpdateDiscountStockAsync`, `ReserveDiscountStockAsync`. Supplying `coupon` addresses coupon-specific stock. |

When `Ekom:PerStoreStock` is false, store aliases are ignored and stock is global by item key. Reservation expiry is persisted and processed by Ekom's hosted worker.

## Warehouse

`Ekom.API.Warehouse` is a separate, SKU-based inventory pool. It does not replace or aggregate into `Stock` automatically.

| Method | Result |
| --- | --- |
| `GetWarehousesAsync(storeAlias)` | Visible published warehouse definitions. |
| `GetAsync(storeAlias, skus)` | Balances across visible warehouses. |
| `GetAsync(storeAlias, warehouseKey, sku/skus)` | Balance(s) in one configured warehouse. |
| `GetForSkuAsync(storeAlias, sku)` | Every visible warehouse plus nullable balance for one SKU. |
| `SetAsync(storeAlias, warehouseKey, sku, balance)` | Sets one non-negative balance. |
| `SetAsync(storeAlias, warehouseKey, balances)` | Sets unique normalized SKUs one at a time. |
| `ClearAsync(...)` | Removes a persisted balance and reports whether a row was removed. |
| `UpdateAsync(mutations)` | Applies a mixed set/clear batch and returns per-entry statuses. |

Store aliases are trimmed; SKUs are trimmed and upper-cased. Empty keys/SKUs, unknown warehouses, negative balances, and duplicate normalized identities are rejected. Batch validation/persistence failures appear as `Failed` entries, allowing partial success.

## Related reference

- [Services and dependency injection](services-and-dependency-injection.md)
- [Events](events.md)
- [Models and behavior](models-and-behavior.md)
- [Configuration](configuration-reference.md)
- [HTTP API](http-api.md)

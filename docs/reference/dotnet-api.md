[Ekom docs](../README.md) / [Reference](README.md) / .NET API

# .NET API

The classes in the `Ekom.API` namespace are how your C# code on the server talks to Ekom: read the catalog, change a basket, take payment, and read or change stock and prices. This page lists their public methods, grouped by class.

These classes run inside your site. They are not HTTP routes; for those, see the [HTTP API](http-api.md).

On this page:

- [Get an API class](#get-an-api-class)
- [Catalog](#catalog)
- [Store](#store)
- [Order and checkout](#order-and-checkout)
- [Providers](#providers)
- [Discounts](#discounts)
- [Stock](#stock)
- [Warehouse](#warehouse)
- [Prices](#prices)

## Get an API class

Inject the class in a constructor:

```csharp
using Ekom.API;
using Ekom.Models;

public sealed class BasketService(Order order)
{
    public Task<IOrderInfo> AddShirtAsync(Guid shirtKey, CancellationToken ct)
        => order.AddOrderLineAsync(shirtKey, 1, "store", ct: ct);
}
```

Where you cannot use injection, use the static `Instance` property. It resolves the class from Ekom's root service provider. `Order.Instance` first tries the services of the current HTTP request.

| Class | Use it to | Lifetime | Guide |
| --- | --- | --- | --- |
| `Catalog` | Read categories, products, variants and search. | Transient | [Catalog and products](../guides/catalog-and-products.md) |
| `Store` | Read stores and domains, and set the request's store. | Transient | [Stores](../guides/stores.md) |
| `Order` | Read and change baskets and orders, apply coupons, pay. | Transient | [Orders and baskets](../guides/orders-and-baskets.md) |
| `Providers` | Read payment and shipping providers, countries and zones. | Transient | [Payment and shipping providers](../guides/providers.md) |
| `Discounts` | Read the configured discounts. | Transient | [Discounts and coupons](../guides/discounts.md) |
| `Stock` | Read and change sellable stock and discount stock. | Transient | [Stock](../guides/stock.md) |
| `Warehouse` | Read and change warehouse stock. | Transient | [Stock](../guides/stock.md#warehouse-stock) |
| `Prices` | Read and change database price overrides. | Singleton | [Database price storage](../guides/database-price-storage.md) |

Many methods take an optional store alias. Without one, Ekom uses the store of the current HTTP request. Background jobs, imports and message handlers have no request, so always pass the store alias there.

Methods that end in `Async` have a synchronous twin in many cases. Use the asynchronous form.

## Catalog

`Ekom.API.Catalog` reads from Ekom's in-memory catalog. Guide: [Catalog and products](../guides/catalog-and-products.md).

Most methods raise the [catalog events](events.md#catalog-events). Pass `raiseEvent: false`, or set `ProductQuery.RaiseEvents = false`, to skip them.

### Products

| Method | Returns |
| --- | --- |
| `GetProductAsync(bool raiseEvent = true, CancellationToken ct = default)` | The product of the current Umbraco page, or `null`. |
| `GetProductAsync(Guid key, string? storeAlias = null, bool? global = null, bool raiseEvent = true, CancellationToken ct = default)` | A product by key, or `null`. |
| `GetProductAsync(int Id, string? storeAlias = null, bool? global = null, bool raiseEvent = true, CancellationToken ct = default)` | A product by Umbraco node ID, or `null`. |
| `GetProductAsync(string sku, string? storeAlias = null, bool? global = null, bool raiseEvent = true, CancellationToken ct = default)` | A product by SKU, or `null`. |
| `GetProductByRouteAsync(string route, string? storeAlias = null, bool raiseEvent = true, CancellationToken ct = default)` | A product by URL path, or `null`. Looks in one store only. |
| `GetAllProductsAsync(ProductQuery? query = null, CancellationToken ct = default)` | `ProductResponse` with all products of the store. An overload takes `string storeAlias` first. |
| `GetProductsByIdsAsync(ProductQuery? query = null, CancellationToken ct = default)` | `ProductResponse` for `query.Ids`. An overload takes `string storeAlias` first. |
| `GetProductsByKeysAsync(ProductQuery? query = null, CancellationToken ct = default)` | `ProductResponse` for `query.Keys`. An overload takes `string storeAlias` first. |
| `GetProductsBySkusAsync(ProductQuery? query = null, CancellationToken ct = default)` | `ProductResponse` for `query.Skus`. An overload takes `string storeAlias` first. |
| `GetProductsRescursiveByRouteAsync(string route, ProductQuery? query = null, CancellationToken ct = default)` | `ProductResponse` with the products in a category and all categories below it. The method name is spelled `Rescursive`. |
| `GetRelatedProductsAsync(Guid productId, int count = 4, string? storeAlias = null, CancellationToken ct = default)` | Up to `count` related products. |
| `GetRelatedProductsBySkuAsync(string sku, int count = 4, string? storeAlias = null, CancellationToken ct = default)` | Up to `count` related products. |
| `ProductSearchAsync(SearchRequest req, CancellationToken ct = default)` | `ProductResponse` with the products that match the search. |

`global`: when it is `null`, the `Ekom:GlobalCatalog` setting decides whether Ekom also looks in other stores. `true` or `false` overrides the setting.

A `ProductResponse` applies the query's filters, sorting and paging. See [Product queries and responses](models-and-behavior.md#product-queries-and-responses).

```csharp
ProductResponse result = await catalog.GetAllProductsAsync(new ProductQuery
{
    StoreAlias = "store",
    Page = 1,
    PageSize = 24,
    OrderBy = OrderBy.DateDesc,
    MetaFilters = new() { ["brand"] = ["Ekom"] },
}, ct);
```

### Categories

| Method | Returns |
| --- | --- |
| `GetCategoryAsync(bool raiseEvent = true, CancellationToken ct = default)` | The category of the current Umbraco page, or `null`. |
| `GetCategoryAsync(Guid Id, string? storeAlias = null, bool global = false, bool raiseEvent = true, CancellationToken ct = default)` | A category by key, or `null`. Overloads take an `int` node ID or a `string` ID. |
| `GetCategoryByRouteAsync(string route, string? storeAlias = null, bool raiseEvent = true, CancellationToken ct = default)` | A category by URL path, or `null`. Looks in one store only. |
| `GetRootCategoriesAsync(string? storeAlias = null, CancellationToken ct = default)` | The categories at the level set by `Ekom:CategoryRootLevel`. |
| `GetAllCategoriesAsync(string? storeAlias = null, CancellationToken ct = default)` | All categories of the store. |
| `GetCategoriesByIdsAsync(int[] ids, string? storeAlias = null, CancellationToken ct = default)` | Categories by node ID. |
| `GetCategoriesByKeysAsync(Guid[] keys, string? storeAlias = null, CancellationToken ct = default)` | Categories by key. |

For categories, `global` is a plain `bool`: Ekom looks in other stores when either the argument or `Ekom:GlobalCatalog` is `true`.

### Variants and metafields

| Method | Returns |
| --- | --- |
| `GetVariantAsync(Guid Id, string? storeAlias = null, CancellationToken ct = default)` | A variant by key, or `null`. Overloads take an `int` node ID or a `string` SKU. |
| `GetVariantsByGroupAsync(int id, string? storeAlias = null, CancellationToken ct = default)` | The variants in a variant group. |
| `GetVariantGroupAsync(Guid key, string? storeAlias = null, CancellationToken ct = default)` | A variant group by key, or `null`. An overload takes an `int` node ID. |
| `GetMetafields()` | All metafield definitions. |

The overloads that take the store alias first, such as `GetVariant(string storeAlias, Guid key)`, are obsolete.

## Store

`Ekom.API.Store` reads stores and the store of the current request. Guide: [Stores](../guides/stores.md).

| Method | Returns |
| --- | --- |
| `GetStore()` | The store of the current request, or the first store when there is no request store. Can be `null`. |
| `GetStore(string? storeAlias)` | A store by alias, or `null`. |
| `GetStoreByDomain(string domain, string culture)` | The store for an Umbraco domain and culture, or `null`. |
| `GetAllStores()` | All stores. |
| `GetDomains()` | All Umbraco domains Ekom knows. |
| `SetStore(string storeAlias)` | Sets the store of the current request and returns it. |
| `RefreshCache()` | Rebuilds all Ekom caches. Use it for administration, not on normal page requests. |

## Order and checkout

`Ekom.API.Order` reads and changes baskets and orders. A basket is an order that has not been paid yet. Guides: [Orders and baskets](../guides/orders-and-baskets.md), [Checkout](../guides/checkout.md).

Most changing methods take an `OrderSettings` (or a class derived from it). Its `FireEvents` property turns the [order events](events.md#order-events) for that call on or off.

### Read orders

| Method | Returns |
| --- | --- |
| `GetOrderAsync(CancellationToken ct = default)` | The current basket of the request's store, or `null`. |
| `GetOrderAsync(string? storeAlias, CancellationToken ct = default)` | The current basket of a store, or `null`. |
| `GetOrderAsync(Guid uniqueId, CancellationToken ct = default)` | Any order by key, including completed orders, or `null`. |
| `GetCompletedOrderAsync(string storeAlias, CancellationToken ct = default)` | The customer's completed order, or `null`. Found from the basket cookie for the store. In a store with `UserBasket` on, a signed-in member gets their newest completed order in that store, until they start a new basket. Throws `ArgumentException` when `storeAlias` is empty. |
| `GetStatusOrdersAsync(CancellationToken ct = default, params OrderStatus[] orderStatuses)` | Orders with any of the given legacy statuses. |
| `GetStatusOrdersByCustomerIdAsync(int customerId, CancellationToken ct = default, params OrderStatus[] orderStatuses)` | A member's orders with any of the given statuses. An overload without `customerId` uses the signed-in member. |
| `GetStatusOrdersByCustomerUsernameAsync(string customerUsername, CancellationToken ct = default, params OrderStatus[] orderStatuses)` | Orders of a username with any of the given statuses. |
| `GetCompleteCustomerOrdersAsync(int customerId, CancellationToken ct = default, string? storeAlias = null)` | A member's completed orders. An overload takes a `string userName`. |
| `GetPaymentAsync(Guid orderId, CancellationToken ct = default)` | The order's current payment, or `null`. |
| `static IsOrderFinal(OrderStatus? orderStatus)` | `true` for the legacy statuses `OfflinePayment`, `Pending`, `ReadyForDispatch`, `ReadyForPickup`, `ReadyForDispatchWhenStockArrives`, `Dispatched`, `Returned`, `Cancelled` and `Closed`. |

The `GetStatusOrders...` methods throw `ArgumentException` when you pass no status. `OrderStatus` here is the legacy `Ekom.Utilities.OrderStatus`.

### Change order lines

| Method | What it does |
| --- | --- |
| `AddOrderLineAsync(Guid productId, decimal quantity, string storeAlias, AddOrderSettings? settings = null, CancellationToken ct = default)` | Adds a product or variant to the basket. |
| `AddLinkedOrderLinesAsync(Guid productId, decimal quantity, string storeAlias, IReadOnlyCollection<LinkedOrderLineRequest> linkedProducts, AddOrderSettings? settings = null, CancellationToken ct = default)` | Adds a line and its linked lines together. See [Linked order lines](../guides/linked-order-lines.md). |
| `UpdateOrderlineQuantityAsync(Guid lineId, decimal quantity, string storeAlias, OrderSettings? settings = null, CancellationToken ct = default)` | Sets a line's quantity. |
| `RemoveOrderLineAsync(Guid lineId, string storeAlias, OrderSettings? settings = null, CancellationToken ct = default)` | Removes a line by line key. |
| `RemoveOrderLineProductAsync(Guid productKey, string storeAlias, RemoveOrderSettings? settings = null, CancellationToken ct = default)` | Removes the line of a product. Set `RemoveOrderSettings.VariantKey` for a variant. |
| `UpdateOrderLineMetadataAsync(Guid orderId, IReadOnlyCollection<OrderLineMetadataUpdate> updates, OrderSettings? settings = null, CancellationToken ct = default)` | Adds or changes `orderline...` values on existing lines of any order. See below. |
| `ReInitializeOrder(string storeAlias, OrderSettings? settings = null, CancellationToken ct = default)` | Prices the basket's lines again for the current customer, for example after login. Keeps each line, its quantity, metadata and links. Does not check stock. |

`AddOrderSettings` adds two properties to `OrderSettings`:

- `OrderAction`: `AddOrUpdate` (default) adds the quantity to an existing line of the same product, `Set` sets the quantity, `New` always adds a new line.
- `VariantKey`: the variant to add.

`AddOrderLineAsync` and `AddLinkedOrderLinesAsync` use the store of the current request when `storeAlias` is empty, and throw `ArgumentException` when there is none.

#### Order line metadata

`UpdateOrderLineMetadataAsync` adds or changes string values on existing lines, for example the warehouse a line ships from. The lines are found by line key.

- It works on any order, whatever its order or payment state, including completed orders and orders that need reconciliation.
- Ekom checks every update first. A missing or repeated line key, an empty update, or a property name that does not start with `orderline` rejects the whole call with `ArgumentException` or `OrderLineNotFoundException`. Nothing is saved.
- The values you send are merged into the line. Values you do not send stay as they are.
- It does not recalculate prices, stock, discounts or providers. It does not release holds or reset payment state.
- With events on, it raises one `OrderUpdated` event, not one per line.
- Pass the order key. A call with `OrderSettings.OrderInfo` set throws `ArgumentException`. If the order changes while the update runs, it throws `InvalidOperationException`; load the order again and retry.

The manager shows each line's non-empty values, without the `orderline` prefix in the label.

```csharp
using Ekom.API;
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

### Customer, providers, currency and custom data

| Method | What it does |
| --- | --- |
| `UpdateCustomerInformationAsync(Dictionary<string, string> form, OrderSettings? settings = null, CancellationToken ct = default)` | Saves customer and shipping fields, and can change the providers. Throws `ArgumentNullException` when `form` is `null`. |
| `UpdateTrackingAsync(string storeAlias, OrderTracking tracking, OrderSettings? settings = null, CancellationToken ct = default)` | Saves marketing attribution on the basket. |
| `UpdateShippingInformationAsync(Guid shippingProvider, string storeAlias, Dictionary<string, string> allData, OrderSettings? settings = null, CancellationToken ct = default)` | Sets the shipping provider. |
| `UpdatePaymentInformationAsync(Guid paymentProvider, string storeAlias, Dictionary<string, string> allData, OrderSettings? settings = null, CancellationToken ct = default)` | Sets the payment provider. |
| `UpdateCurrencyAsync(string currency, Guid orderId, string storeAlias, CancellationToken ct = default)` | Changes the order's currency. |
| `UpdateCustomDataAsync(Guid orderId, IReadOnlyDictionary<string, string> updates, OrderSettings? settings = null, CancellationToken ct = default)` | Adds or changes your own values on the order. See [Order custom data](../guides/order-custom-data.md). |
| `RemoveCustomDataAsync(Guid orderId, IReadOnlyCollection<string> keys, OrderSettings? settings = null, CancellationToken ct = default)` | Removes your own values from the order. |
| `DeleteOrderCookie(string? storeAlias = null)` | Removes the basket cookie. |
| `EnsureOrderCookie(Guid orderId, string? storeAlias = null)` | Sets the basket cookie to the given order. |

About `UpdateCustomerInformationAsync`:

- It works in every order and payment state, so staff can correct an order that needs reconciliation. It does not release holds or reset payment attempts.
- To change an order other than the current basket, set `OrderSettings.OrderInfo`. Ekom loads that order again before it applies the form.
- What the customer submitted for payment is kept as it was. A later change to the customer, providers or lines can therefore stop Ekom from completing the payment automatically, and staff must reconcile it.
- `FireEvents` only turns events on or off. It does not bypass any check.
- Other basket-changing methods still refuse changes while a payment is in progress.

### Coupons and gift cards

| Method | What it does |
| --- | --- |
| `ApplyCouponToOrderAsync(string coupon, string storeAlias, CancellationToken ct = default)` | Applies a coupon to the basket. Returns `false` when a better discount is already on the order. An overload without `storeAlias` uses the request's store. |
| `RemoveCouponFromOrderAsync(string? storeAlias, DiscountOrderSettings? settings, CancellationToken ct = default)` | Removes the basket's coupon. |
| `SetCouponCodeAsync(string coupon, string? storeAlias, DiscountOrderSettings? discountOrderSettings = null, CancellationToken ct = default)` | Saves the code in the basket's `Coupon` value without applying a discount. An empty code changes nothing. |
| `ApplyCouponToOrderLineAsync(Guid productKey, string coupon, string storeAlias)` | Applies a coupon to one line. |
| `RemoveCouponFromOrderLineAsync(Guid productKey, string? storeAlias, CancellationToken ct = default)` | Removes a line's coupon. |
| `AddGiftcardAsync(Giftcard giftcard, string storeAlias, OrderSettings? settings = null, CancellationToken ct = default)` | Adds a gift card you have already validated. |
| `RemoveGiftcardAsync(string code, string storeAlias, OrderSettings? settings = null, CancellationToken ct = default)` | Removes a gift card by code. |
| `InsertCouponCodeAsync(string couponCode, int numberAvailable, Guid discountId, CancellationToken ct = default)` | Creates a coupon code for a discount. |
| `GenerateCouponCodesAsync(Guid discountId, CouponGenerationRequest request, CancellationToken ct = default)` | Creates many coupon codes. |
| `RemoveCouponCodeAsync(string couponCode, Guid discountId)` | Deletes a coupon code. |
| `GetCouponsForDiscountAsync(Guid discountId, string query, int page, int pageSize, CancellationToken ct = default)` | A page of a discount's coupon codes and the number of pages. An overload without paging returns them all. |

`ApplyCouponToOrderAsync` lowercases the code and throws when it cannot apply it:

| Exception | When |
| --- | --- |
| `CouponApplicationRejectedException` | A `BeforeApplyCouponDiscountAsync` handler rejected the code. See [Discount events](events.md#discount-events). |
| `ArgumentException` | The code or store alias is empty. |
| `DiscountNotFoundException` | No coupon has this code. |
| `DiscountHasNoUsageException` | The coupon has no uses left. |
| `DiscountUnableToFindCouponException` | The coupon's discount is not in this store. |

### Statuses and activity log

| Method | What it does |
| --- | --- |
| `UpdateStatusAsync(OrderStatus newStatus, Guid orderId, string? userName = null, ChangeOrderSettings? settings = null, CancellationToken ct = default)` | Sets the legacy status of an order. The order, payment and fulfillment statuses follow. |
| `UpdateStatusAsync(string storeAlias, OrderStatus newStatus, ChangeOrderSettings? settings = null, CancellationToken ct = default)` | Same, for the current basket of a store. Throws `OrderInfoNotFoundException` when there is none. |
| `AddActivityLogAsync(Guid orderId, string message, string? userName = null, OrderActivityLogType logType = OrderActivityLogType.Info, CancellationToken ct = default)` | Writes an entry to the order's activity log. Throws `ArgumentException` for an empty key or message, and `OrderInfoNotFoundException` when the order does not exist. |

With `ChangeOrderSettings { FireEvents = false }`, `UpdateStatusAsync` raises no status change events. See [Order, payment, and fulfillment statuses](../guides/order-statuses.md).

### Payment, completion and reservations

| Method | What it does |
| --- | --- |
| `PayAsync(PaymentRequest paymentRequest, string storeAlias, Guid orderId, CancellationToken ct = default)` | Runs the checkout payment for an order and returns a `CheckoutResponse`. An overload takes an `IOrderInfo` instead of the key. `storeAlias` must not be empty. |
| `CompleteOrderAsync(Guid orderId, CancellationToken ct = default)` | Completes a paid order: deducts stock, uses up the coupon and updates the status. |
| `ClearCustomerOrderReferenceAsync(Guid orderId, OrderData? order = null, CancellationToken ct = default)` | For an order with a final status, clears the link that makes it the customer's current basket. Does nothing for other orders. |
| `AddReservationsToOrderAsync(string storeAlias, IEnumerable<string> reservationIds, IOrderInfo orderInfo, CancellationToken ct = default)` | Links stock reservations to an order. |
| `RemoveReservationsFromOrderAsync(string storeAlias, CancellationToken ct = default)` | Unlinks the reservations from the basket. It does not release them. |

`AddHangfireJobsToOrderAsync` and `RemoveHangfireJobsFromOrderAsync` are older names for the two reservation methods. Reservations do not use Hangfire. See [Stock reservations](../guides/reservations.md).

## Providers

`Ekom.API.Providers` reads payment and shipping providers, countries and zones. Guide: [Payment and shipping providers](../guides/providers.md).

| Method | Returns |
| --- | --- |
| `GetPaymentProvidersAsync(string? store = null, string? countryCode = null, decimal orderAmount = 0, CancellationToken ct = default)` | The payment providers a customer can use, sorted by sort order. |
| `GetShippingProvidersAsync(string? store = null, string? countryCode = null, decimal orderAmount = 0, CancellationToken ct = default)` | The shipping providers a customer can use, sorted by sort order. |
| `GetPaymentProviderAsync(Guid key, string? storeAlias = null, CancellationToken ct = default)` | One payment provider, or `null`. An overload takes an `IStore`. |
| `GetShippingProviderAsync(Guid key, string? storeAlias = null, CancellationToken ct = default)` | One shipping provider, or `null`. An overload takes an `IStore`. |
| `GetAllCountries()` | All countries. |
| `GetAllZones()` | All zones. |

- Ekom filters by country only when `countryCode` has two letters. A provider whose zone lists no countries is then left out.
- Ekom filters by amount only when `orderAmount` is above `0`. A provider whose range ends at `0` has no upper limit.
- The [provider events](events.md#provider-events) run after these filters.
- When no store alias is given and the request has no store, the list methods throw `ArgumentException`.

A payment provider method `GetPaymentProvidersAsync(string? store, string? countryCode, decimal orderAmount)` without a cancellation token returns an `IAsyncEnumerable<IPaymentProvider>`.

## Discounts

`Ekom.API.Discounts` reads the configured discounts. It does not change anything. Guide: [Discounts and coupons](../guides/discounts.md).

| Method | Returns |
| --- | --- |
| `GetDiscounts()` | The discounts of the request's store, or an empty list when there is no store. |
| `GetDiscounts(string storeAlias)` | The discounts of a store. |
| `GetGlobalDiscounts()` | The discounts of the request's store whose `GlobalDiscount` is `true`. |
| `GetGlobalDiscounts(string storeAlias)` | The same for a given store. |

To apply a coupon, use `Order`. To read or change how many times a discount can be used, use `Stock`.

## Stock

`Ekom.API.Stock` reads and changes sellable stock: the count that decides whether a product can be bought. Guide: [Stock](../guides/stock.md).

| Method | What it does |
| --- | --- |
| `GetStock(Guid key)`, `GetStock(Guid key, string storeAlias)` | The stock count. `0` for a product with no stock row. |
| `GetStockData(Guid key)`, `GetStockData(Guid key, string storeAlias)` | The stock row. A missing row is returned as a `StockData` with `Stock = 0`. |
| `IncrementStockAsync(Guid key, decimal value, CancellationToken ct = default)` | Changes stock by `value`. An overload takes `string storeAlias` after the key. |
| `SetStockAsync(Guid key, decimal value, CancellationToken ct = default)` | Replaces the count. An overload takes `string storeAlias` after the key. |
| `SetStockAsync(IEnumerable<StockSetRequest> requests, CancellationToken ct = default)` | Replaces many counts, in batches of 200. |
| `ValidateOrderStockAsync(IOrderInfo orderInfo, CancellationToken ct = default)` | Throws `NotEnoughLineStockException` when a line asks for more than is available. Skips backorder products and takes stock buffers into account. |
| `ReserveStockAsync(Guid key, string storeAlias, decimal value, TimeSpan timeSpan = default, CancellationToken ct = default)` | Holds stock and returns the reservation ID. `value` must be negative. Throws `NotEnoughStockException` when there is not enough. |
| `RollbackJobAsync(string jobId, CancellationToken ct = default)` | Releases a reservation and returns its stock. |
| `CancelRollback(string jobId)` | Uses up a reservation, so its stock stays deducted. Throws `StockException` when that fails. |
| `CompleteRollback(string jobId)` | Releases a reservation. Synchronous. |
| `GetDiscountStockAsync(Guid key, string coupon = null)` | How many uses a discount, or one of its coupons, has left. |
| `GetDiscountStockDataAsync(Guid key, string coupon = null)` | The discount stock row. |
| `UpdateDiscountStockAsync(Guid key, int value, string coupon = null)` | Changes discount stock by `value`. Throws `ArgumentException` when `value` is `0`. |
| `ReserveDiscountStockAsync(Guid key, int value, string coupon = null, TimeSpan timeSpan = default)` | Holds discount stock and returns the reservation ID. `value` must be negative. |

- When `Ekom:PerStoreStock` is `false`, store aliases are ignored and every store shares one count. When it is `true` and no store alias is given, Ekom uses the request's store, and throws `InvalidOperationException` when there is none.
- `IncrementStockAsync`, `SetStockAsync` and reservations raise [`StockEvents.StockChangedAsync`](events.md#stock-events).
- For reservations with explicit results, inject `IStockReservationService` instead. See [Services and dependency injection](services-and-dependency-injection.md#reserve-stock).

## Warehouse

`Ekom.API.Warehouse` reads and changes warehouse stock: how many units each warehouse holds, by SKU. It never changes what can be bought. Guide: [Stock](../guides/stock.md#warehouse-stock).

| Method | Returns |
| --- | --- |
| `GetWarehousesAsync(string storeAlias, CancellationToken ct = default)` | The store's published warehouses that are not hidden. |
| `GetAsync(string storeAlias, IEnumerable<string> skus, CancellationToken ct = default)` | Balances of the SKUs in all visible warehouses. |
| `GetAsync(string storeAlias, Guid warehouseKey, string sku, CancellationToken ct = default)` | One balance, or `null`. |
| `GetAsync(string storeAlias, Guid warehouseKey, IEnumerable<string> skus, CancellationToken ct = default)` | Balances of the SKUs in one warehouse. |
| `GetForSkuAsync(string storeAlias, string sku, CancellationToken ct = default)` | Every visible warehouse with its balance for one SKU. The balance is `null` where none is saved. |
| `SetAsync(string storeAlias, Guid warehouseKey, string sku, decimal balance, CancellationToken ct = default)` | Saves one balance. Throws `ArgumentOutOfRangeException` for a negative balance. |
| `SetAsync(string storeAlias, Guid warehouseKey, IEnumerable<WarehouseStockBalanceRequest> balances, CancellationToken ct = default)` | Saves several balances in one warehouse. |
| `ClearAsync(string storeAlias, Guid warehouseKey, string sku, CancellationToken ct = default)` | Removes a saved balance. Returns `true` when a balance was removed. |
| `UpdateAsync(IEnumerable<WarehouseStockMutationRequest> mutations, CancellationToken ct = default)` | Applies a list of set and clear changes and reports the result of each one. |

Ekom trims store aliases and SKUs, and ignores upper and lower case in SKUs. It rejects empty keys and SKUs, warehouses that are not configured for the store, negative balances, and the same store, warehouse and SKU twice in one list. In `UpdateAsync`, these become `Failed` entries and the other entries are still saved.

## Prices

`Ekom.API.Prices` reads and changes price overrides stored in the `EkomPrice` table, so prices can change without publishing Umbraco content. It is a singleton. Guide: [Database price storage](../guides/database-price-storage.md).

| Member | What it does |
| --- | --- |
| `Enabled` | `true` when `Ekom:UseDatabasePrices` is `true`. |
| `GetAsync(Guid nodeKey, string propertyAlias = "price", CancellationToken cancellationToken = default)` | The overrides of a node and price property. Empty when the feature is off. |
| `SetPriceAsync(Guid nodeKey, string storeAlias, string currency, decimal price, string propertyAlias = "price", CancellationToken cancellationToken = default)` | Saves one override. |
| `RemovePriceAsync(Guid nodeKey, string storeAlias, string currency, string propertyAlias = "price", CancellationToken cancellationToken = default)` | Removes one override. The node's own price applies again. |
| `ApplyAsync(IEnumerable<PriceMutation> changes, CancellationToken cancellationToken = default)` | Saves and removes many overrides together. A `PriceMutation` with `Price = null` removes the override. |
| `Validate(IEnumerable<PriceMutation> changes)` | Checks a batch without saving it. |
| `IsCurrent(IEnumerable<PriceMutation> changes)` | `true` when the cache already holds exactly these prices. |
| `Refresh(IEnumerable<Guid> nodeKeys)` | Reloads the cached prices of some nodes from the database. |
| `FillCacheAsync(CancellationToken cancellationToken = default)` | Reloads all cached prices. Use it after another server or direct SQL changed the table. |
| `MigrateNodePricesAsync(Guid nodeKey, string? nodeValue, IReadOnlyList<IStore> stores, string propertyAlias = "price", CancellationToken cancellationToken = default)` | Copies a node's prices into missing table rows and returns how many it added. Existing overrides are not replaced. |
| `Resolve(Guid nodeKey, string propertyAlias, string storeAlias, string nodeValue, IReadOnlyList<CurrencyModel> currencies)` | The node's price value with the overrides applied. |

- Writing while `Ekom:UseDatabasePrices` is `false` throws `InvalidOperationException`.
- A missing node key or a repeated key in one batch throws `ArgumentException`. A negative price throws `ArgumentOutOfRangeException`.
- A `PriceMutation` with `CheckExpected = true` throws `PriceConflictException` when the saved price is not `ExpectedPrice`.
- Every saved change raises [`PriceEvents.PriceChanged`](events.md#price-events).
- Synchronous twins exist: `Get`, `SetPrice`, `RemovePrice`, `Apply`, `FillCache`.

## Related pages

- [Services and dependency injection](services-and-dependency-injection.md): the lower-level services and how to replace them.
- [Events](events.md): the events these methods raise.
- [Models and behavior](models-and-behavior.md): the objects these methods return.
- [Configuration reference](configuration-reference.md): settings that change how the methods behave.
- [HTTP API](http-api.md): the same features over HTTP.

[Back to Reference](README.md)

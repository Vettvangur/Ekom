# Stock API

`Ekom.API.Stock` is the main C# entry point for reading, validating, updating, reserving, and rolling back stock.

## Example

```csharp
using Ekom.API;

public sealed class StockApplicationService
{
    private readonly Stock _stock;

    public StockApplicationService(Stock stock)
    {
        _stock = stock;
    }

    public async Task<bool> SetProductStockAsync(Guid productKey, decimal value, CancellationToken ct)
    {
        return await _stock.SetStockAsync(productKey, "Store", value, ct);
    }
}
```

## When to use `API.Stock`

Use `API.Stock` when you want to:

- read product or variant stock
- validate order stock before checkout or fulfillment
- set stock to a specific value
- increment or decrement stock
- reserve stock temporarily
- roll back or complete stock reservations
- work with discount stock

## Injecting `Stock`

The usual way to work with `API.Stock` is to inject it.

```csharp
using Ekom.API;

public sealed class StockApplicationService
{
    private readonly Stock _stock;

    public StockApplicationService(Stock stock)
    {
        _stock = stock;
    }
}
```

You can also access the static instance:

```csharp
var stockApi = Stock.Instance;
```

In most application code, constructor injection is the better option.

## Reading stock

### `GetStock(Guid key)`

```csharp
decimal stock = _stock.GetStock(productKey);
```

Returns the stock amount for an item.

When `PerStoreStock` is enabled, Ekom resolves the store from the current request context.

This method is sync-only.

### `GetStock(Guid key, string storeAlias)`

```csharp
decimal stock = _stock.GetStock(productKey, "Store");
```

Returns the stock amount for an item in a specific store.

This method is sync-only.

### `GetStockData(Guid key)`

```csharp
StockData stockData = _stock.GetStockData(productKey);
```

Returns the stock data for an item.

When `PerStoreStock` is enabled, Ekom resolves the store from the current request context.

If no stock entry exists, Ekom returns a new stock data object with `Stock` set to `0`.

This method is sync-only.

### `GetStockData(Guid key, string storeAlias)`

```csharp
StockData stockData = _stock.GetStockData(productKey, "Store");
```

Returns the stock data for an item in a specific store.

If no stock entry exists, Ekom returns a new stock data object with `Stock` set to `0`.

This method is sync-only.

## Validating stock

### `ValidateOrderStockAsync(IOrderInfo orderInfo, CancellationToken ct = default)`

```csharp
await _stock.ValidateOrderStockAsync(order, ct);
```

Validates stock for all order lines on an order.

Products that allow backorder are skipped. If stock is not available, Ekom throws a stock exception for the affected order line.

## Updating stock

### `IncrementStockAsync(Guid key, decimal value, CancellationToken ct = default)`

```csharp
await _stock.IncrementStockAsync(productKey, -1, ct);
```

Increments or decrements stock by the provided value.

When `PerStoreStock` is enabled, Ekom resolves the store from the current request context.

### `IncrementStockAsync(Guid key, string storeAlias, decimal value, CancellationToken ct = default)`

```csharp
await _stock.IncrementStockAsync(productKey, "Store", -1, ct);
```

Increments or decrements stock for an item in a specific store.

If the update would make stock negative, Ekom throws `NotEnoughStockException`.

### `SetStockAsync(Guid key, decimal value, CancellationToken ct = default)`

```csharp
bool updated = await _stock.SetStockAsync(productKey, 25, ct);
```

Sets stock to a specific value.

When `PerStoreStock` is enabled, Ekom resolves the store from the current request context.

### `SetStockAsync(Guid key, string storeAlias, decimal value, CancellationToken ct = default)`

```csharp
bool updated = await _stock.SetStockAsync(productKey, "Store", 25, ct);
```

Sets stock to a specific value for an item in a specific store.

Prefer `IncrementStockAsync(...)` unless you are intentionally replacing stock with an absolute value.

## Reserving stock

### `ReserveStockAsync(Guid key, decimal value, TimeSpan timeSpan = default, CancellationToken ct = default)`

```csharp
string jobId = await _stock.ReserveStockAsync(productKey, -1, ct: ct);
```

Temporarily reserves stock and schedules a Hangfire rollback job.

`value` must be negative. If `timeSpan` is not provided, Ekom uses the configured reservation timeout.

### `ReserveStockAsync(Guid key, string storeAlias, decimal value, TimeSpan timeSpan = default, CancellationToken ct = default)`

```csharp
string jobId = await _stock.ReserveStockAsync(productKey, "Store", -1, ct: ct);
```

Temporarily reserves stock for a specific store and schedules a Hangfire rollback job.

`value` must be negative. If `timeSpan` is not provided, Ekom uses the configured reservation timeout.

### `CancelRollback(string jobId)`

```csharp
_stock.CancelRollback(jobId);
```

Cancels a scheduled stock rollback job.

This method is sync-only.

### `RollbackJobAsync(string jobId, CancellationToken ct = default)`

```csharp
await _stock.RollbackJobAsync(jobId, ct);
```

Rolls back a scheduled stock reservation and removes the related Hangfire job.

### `CompleteRollback(string jobId)`

```csharp
_stock.CompleteRollback(jobId);
```

Requeues a scheduled rollback job from the scheduled state.

This method is sync-only.

## Discount stock

### `GetDiscountStockAsync(Guid key, string coupon = null)`

```csharp
int stock = await _stock.GetDiscountStockAsync(discountKey);
```

Returns the stock amount for a discount.

Pass `coupon` to get coupon-specific discount stock. Leave it empty to get the discount master stock.

### `GetDiscountStockDataAsync(Guid key, string coupon = null)`

```csharp
DiscountStockData stockData = await _stock.GetDiscountStockDataAsync(discountKey);
```

Returns discount stock data for a discount.

Pass `coupon` to get coupon-specific discount stock data. Leave it empty to get the discount master stock data.

### `GetDiscountStockDataAsync(string uniqueId)`

```csharp
DiscountStockData stockData = await _stock.GetDiscountStockDataAsync(uniqueId);
```

Returns discount stock data by unique id.

### `UpdateDiscountStockAsync(Guid key, int value, string coupon = null)`

```csharp
await _stock.UpdateDiscountStockAsync(discountKey, -1);
```

Updates discount stock by incrementing or decrementing it by `value`.

Pass `coupon` to update coupon-specific discount stock. Leave it empty to update the discount master stock.

### `UpdateDiscountStockAsync(string uniqueId, int value)`

```csharp
await _stock.UpdateDiscountStockAsync(uniqueId, -1);
```

Updates discount stock by unique id.

`value` cannot be `0`.

### `ReserveDiscountStockAsync(Guid key, int value, string coupon = null, TimeSpan timeSpan = default)`

```csharp
string jobId = await _stock.ReserveDiscountStockAsync(discountKey, -1);
```

Temporarily reserves discount stock and schedules a Hangfire rollback job.

`value` must be negative. If `timeSpan` is not provided, Ekom uses the configured reservation timeout.

### `UpdateDiscountStockHangfire(Guid key, int value)`

```csharp
Stock.UpdateDiscountStockHangfire(discountKey, 1);
```

Updates discount stock from a Hangfire job.

This method is static and sync-only.

## Hangfire helpers

### `UpdateStockHangfireAsync(Guid key, decimal value, CancellationToken ct)`

```csharp
await Stock.UpdateStockHangfireAsync(productKey, 1, ct);
```

Updates stock from a Hangfire job.

This method is static.

### `UpdateStockHangfireAsync(Guid key, string storeAlias, decimal value, CancellationToken ct)`

```csharp
await Stock.UpdateStockHangfireAsync(productKey, "Store", 1, ct);
```

Updates store-specific stock from a Hangfire job.

This method is static.

## Notes

- This page documents async methods where async methods exist.
- Sync-only methods are included where there is no async alternative.
- Stock reads are cache-based and may return a default stock data object with `Stock` set to `0` when no entry exists.
- `IncrementStockAsync(...)` protects against negative stock.
- `ReserveStockAsync(...)` and `ReserveDiscountStockAsync(...)` only accept negative values.
- Per-store stock behavior depends on the `PerStoreStock` configuration setting.

## Related pages

- [Configuration](configuration.md)
- [Discount API](discount-api.md)
- [Catalog API](catalog-api.md)
- [Order API](api-order-reference.md)
- [Discounts Overview](discounts-overview.md)

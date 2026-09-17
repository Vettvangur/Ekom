# Stock

Ekom has three distinct inventory concepts:

| Concept | Effect on sales |
| --- | --- |
| Product/variant stock | Sellable inventory used by availability and checkout. |
| Discount/coupon stock | Limits discount or coupon uses. |
| Warehouse stock | Display/integration balances only; never affects availability, reservations, or checkout deduction. |

## Sellable stock

Inject `Ekom.API.Stock` or use `Stock.Instance`:

```csharp
decimal current = stock.GetStock(productKey, "Store");
StockData data = stock.GetStockData(productKey, "Store");

await stock.IncrementStockAsync(productKey, "Store", -1m, ct);
await stock.SetStockAsync(productKey, "Store", 25m, ct);
```

Reads use the in-memory stock cache. A missing row is represented as a `StockData` value with stock `0`; reading does not establish sellable inventory. SQL mutations are authoritative and publish cache/event updates after commit.

Prefer `IncrementStockAsync` for deltas because the repository applies conditional SQL arithmetic and prevents a negative result. Use `SetStockAsync` only for a deliberate absolute inventory snapshot. Absolute writes must be coordinated with outstanding reservations.

Equal sets and zero increments are no-ops at the SQL layer for existing rows. `StockEvents.StockChangedAsync` is best-effort after commit and provides key, optional store alias, old value, and new value. A subscriber failure is logged and does not roll back inventory.

## Global or per-store stock

`Ekom:PerStoreStock` defaults to `false`:

```json
{
  "Ekom": {
    "PerStoreStock": true,
    "DisableStock": false
  }
}
```

With global stock, the row identity is the product/variant key and a supplied store alias is ignored. With per-store stock, the identity is `{storeAlias}_{key}` and a store is required. Overloads without an alias resolve the current request store and are unsuitable for background work unless that context is established.

`Ekom:DisableStock` disables normal stock enforcement in order processing. It does not turn warehouse balances into sellable stock and should not be used as a substitute for a backorder policy.

## Availability, backorder and buffers

`IProduct.Stock` and `IVariant.Stock` read sellable stock. Availability follows these rules:

- Backorder makes that product/variant available without positive stock.
- A product with variants is available when any variant is available.
- A product without variants requires effective stock greater than zero.
- Effective stock is `max(0, physical stock - configured buffer)`.
- A variant buffer takes precedence over its product buffer; the product buffer takes precedence over the first positive category-ancestor buffer.

`ValidateOrderStockAsync(order, ct)` skips backorder product lines and throws `NotEnoughLineStockException` when effective product or selected-variant stock is below the line quantity.

Checkout applies the same effective buffer policy. A custom eligibility rule, such as wholesale-only backorders, must be implemented through `ICheckoutStockPolicy` so preparation and completion agree.

## Discount stock

```csharp
int master = await stock.GetDiscountStockAsync(discountKey);
int coupon = await stock.GetDiscountStockAsync(discountKey, "SPRING10");

await stock.UpdateDiscountStockAsync(discountKey, -1);
await stock.UpdateDiscountStockAsync(discountKey, -1, "SPRING10");
```

The value passed to `UpdateDiscountStockAsync` is a delta and cannot be zero. Discount stock is stored in SQL and is not scoped by `PerStoreStock`. Temporary product and discount holds are documented in [Reservations](reservations.md).

## Warehouse stock

Warehouse inventory is available in Umbraco 17 and 18. Define warehouses on each published `ekmStore` with the **Ekom Warehouse Editor**. Product and variant **Warehouse Stock** editors persist balances when the content is saved; save a SKU before editing balances.

Visibility affects storefront reads only. Hidden warehouses remain valid mutation targets. Balances are identified by store alias, warehouse key, and trimmed case-insensitive SKU. Changing a product SKU does not move old balances.

```csharp
using Ekom.API;
using Ekom.Models;

IReadOnlyList<WarehouseDefinition> warehouses =
    await warehouse.GetWarehousesAsync("Store", ct);

IReadOnlyList<WarehouseStockLevel> levels =
    await warehouse.GetForSkuAsync("Store", "SKU-1", ct);

WarehouseStockBalance saved = await warehouse.SetAsync(
    "Store",
    warehouseKey,
    "SKU-1",
    8m,
    ct);

bool removed = await warehouse.ClearAsync(
    "Store",
    warehouseKey,
    "SKU-1",
    ct);
```

Batch mutations isolate validation and persistence failures per entry:

```csharp
WarehouseStockBatchResult result = await warehouse.UpdateAsync([
    new WarehouseStockMutationRequest
    {
        StoreAlias = "Store",
        WarehouseKey = warehouseKey,
        Sku = "SKU-1",
        Operation = WarehouseStockMutationOperation.Set,
        Balance = 12m,
    },
    new WarehouseStockMutationRequest
    {
        StoreAlias = "Store",
        WarehouseKey = secondaryWarehouseKey,
        Sku = "SKU-1",
        Operation = WarehouseStockMutationOperation.Clear,
    },
], ct);
```

Results are `Inserted`, `Updated`, `Cleared`, `Unchanged`, or `Failed`. Duplicate normalized identities in one batch fail validation. Negative balances are rejected. An unchanged set preserves the existing update date.

Warehouse reads use an in-memory snapshot initialized with Ekom's caches. `GetWarehousesAsync`, `GetAsync` list overloads, and `GetForSkuAsync` expose visible warehouses only; a direct warehouse-key read validates configured existence but can read a hidden warehouse.

## Operational notes

- SQL decides whether stock is available; another node's cache may lag until refresh/publication.
- Do not write stock tables directly if Ekom reservations or cache notifications are expected to remain coherent.
- Stock events are notifications, not a durable transaction log.
- `ImportProduct.Stock` and `ImportVariant.Stock` update sellable stock absolutely.
- `ImportProduct.WarehouseStock` and `ImportVariant.WarehouseStock` set or clear only listed warehouse identities; omitted identities are unchanged.

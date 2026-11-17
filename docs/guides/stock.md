[Ekom docs](../README.md) / [Guides](README.md) / Stock

# Stock

Ekom keeps a count of how many of each product or variant you can sell, and uses that count to decide what is available and whether an order can go through. This page shows how to read and change stock from code, how to choose between one shared count and a count per store, and how availability is worked out.

Ekom has three kinds of stock. Only the first one decides whether something can be bought.

| Kind | What it is for | Affects what can be bought |
| --- | --- | --- |
| Sellable stock | The number of a product or variant you can sell. | Yes |
| Discount stock | The number of times a discount or coupon can still be used. | No. It limits the discount, not the product. |
| Warehouse stock | Balances per warehouse, for display and integrations. | No |

On this page:

- [Read and change sellable stock](#read-and-change-sellable-stock)
- [Choose one shared count or a count per store](#choose-one-shared-count-or-a-count-per-store)
- [How Ekom decides that a product is available](#how-ekom-decides-that-a-product-is-available)
- [Discount stock](#discount-stock)
- [Warehouse stock](#warehouse-stock)
- [Running in production](#running-in-production)

## Read and change sellable stock

Inject `Ekom.API.Stock`, or use `Stock.Instance` where injection is not possible.

```csharp
using Ekom.API;
using Ekom.Models;

decimal inStock = stock.GetStock(productKey, "Store");
StockData details = stock.GetStockData(productKey, "Store");

// A customer returned 2 units.
await stock.IncrementStockAsync(productKey, "Store", 2m, ct);

// A stocktake counted 25 units.
await stock.SetStockAsync(productKey, "Store", 25m, ct);
```

### Reading

`GetStock` and `GetStockData` read from memory, so they are safe to call often. A product that has never been given stock returns `0`. Reading does not create a stock row.

### Changing

Use `IncrementStockAsync` for every change that is a difference: a sale, a return, a delivery. Pass a negative number to lower stock.

- If the change would take stock below zero, nothing is saved and Ekom throws `NotEnoughStockException`. This also happens when you lower stock for a product that has no stock row yet.
- The new value is rounded to 2 decimal places.
- A change of `0` does nothing.

Use `SetStockAsync` only when you want to replace the count, for example after a stocktake or a full sync from an ERP system. It overwrites the stored value and does not take open reservations into account. Read [Stock reservations](reservations.md) before you use it on a store that takes orders. Setting the value that is already stored does nothing.

### Setting many values at once

Pass a list of `StockSetRequest` values to set many counts in one call. This is much faster than calling `SetStockAsync` once per product.

```csharp
await stock.SetStockAsync(
[
    new StockSetRequest(shirtKey, "Store", 40m),
    new StockSetRequest(mugKey, "Store", 12m),
], ct);
```

- Each value replaces the stored count, as the single-item method does.
- Ekom writes the rows in batches of 200. Each batch is one database transaction. If a later batch fails, the earlier batches stay saved.
- If the same product and store appear more than once, the last value is used.
- `StoreAlias` is only used when per-store stock is turned on. Otherwise pass `null`.

### Reacting to a change

Subscribe to `StockEvents.StockChangedAsync` to run code when stock changes. The event gives you `Key`, `StoreAlias`, `OldValue` and `NewValue`.

The event runs after the change is saved. If your handler throws, Ekom logs a warning and the stock change stays saved. The event is a notification only: do not use it as a record of every change, because a failed handler is not retried.

## Choose one shared count or a count per store

By default every store shares one count per product. Set `Ekom:PerStoreStock` to `true` to keep a separate count for each store.

```json
{
  "Ekom": {
    "PerStoreStock": true
  }
}
```

| Setting | Default | Effect |
| --- | --- | --- |
| `Ekom:PerStoreStock` | `false` | `false`: one count per product, and any store alias you pass is ignored. `true`: one count per product and store, and a store is required. |
| `Ekom:DisableStock` | `false` | `true`: Ekom no longer rejects an order line because there is too little stock. |

With per-store stock turned on, the methods that take no store alias use the store of the current storefront request. Background jobs, imports and message handlers have no such request. There Ekom falls back to the first store without an error, so the wrong store's stock can be read or changed. Always pass the store alias in that code. See [Use Ekom outside a web request](../examples/background-jobs.md).

`Ekom:DisableStock` does not make warehouse stock sellable. If you want to sell products that are out of stock, turn on backorder for those products instead of turning stock off for the whole site.

## How Ekom decides that a product is available

`IProduct.Available` and `IVariant.Available` follow these rules:

1. A product or variant with backorder turned on is always available.
2. A product with variants is available when at least one variant is available.
3. A product without variants is available when its usable stock is above zero.

Usable stock is the stored count minus the stock buffer, and never less than zero. The buffer is a number of units you hold back from sale. Ekom uses the first buffer above zero that it finds, in this order:

1. the variant
2. the product
3. the categories above the product

### Checking an order

`ValidateOrderStockAsync(order, ct)` checks every line of an order against usable stock. Lines for backorder products are skipped. When a line asks for more than is available, it throws `NotEnoughLineStockException`. The exception tells you which line failed (`OrderLineKey`) and whether the shortage was on a variant (`Variant`).

Checkout uses the same buffer rules.

### Changing which lines need stock

By default a line needs stock unless its product has backorder turned on. To use a different rule at checkout, implement `ICheckoutStockPolicy`. This example lets only wholesale customers order a backorder product beyond its stock:

```csharp
using Ekom.Models;
using Ekom.Services;

public sealed class WholesaleBackorderPolicy : ICheckoutStockPolicy
{
    public bool RequiresStock(IOrderInfo order, IOrderLine line)
    {
        // A product without backorder always needs stock.
        if (!line.Product.Backorder) return true;

        bool isWholesale =
            order.CustomData.TryGetValue("customerType", out string? customerType)
            && customerType == "wholesale";

        // A backorder product needs stock unless the customer is wholesale.
        return !isWholesale;
    }
}
```

Register it in the site's service setup. Your registration replaces Ekom's default policy.

```csharp
builder.Services.AddSingleton<ICheckoutStockPolicy, WholesaleBackorderPolicy>();
```

What to know:

- The policy is used at checkout only. Adding a line to the basket has its own check, which always requires stock for a product without backorder. A policy therefore cannot let anyone order a product without backorder beyond its stock. It can only tighten the rule for backorder products.
- Ekom calls the policy when it prepares the checkout and again when it completes the order. The second call can happen without a web request, for example from a payment callback. Base the decision on data saved with the order, not on the current user or session.
- Only the product's backorder setting counts. A variant's own backorder setting is not checked here.

For a version that reads the customer's member group, see [Replace Ekom services](../examples/replace-services.md).

## Discount stock

Discount stock limits how many times a discount or a coupon code can be used.

```csharp
int discountUsesLeft = await stock.GetDiscountStockAsync(discountKey);
int couponUsesLeft = await stock.GetDiscountStockAsync(discountKey, "SPRING10");

// One use has been spent.
await stock.UpdateDiscountStockAsync(discountKey, -1);
await stock.UpdateDiscountStockAsync(discountKey, -1, "SPRING10");
```

- The number you pass to `UpdateDiscountStockAsync` is a change, not a new total. Passing `0` throws `ArgumentException`.
- Discount stock is shared by all stores. `Ekom:PerStoreStock` does not affect it.
- Discount stock is not the same as the usage limit an editor sets on a coupon. See [Usage limits are not discount stock](discounts/coupons.md#usage-limits-are-not-discount-stock).

To hold a discount use while a customer is paying, see [Stock reservations](reservations.md).

## Warehouse stock

Warehouse stock records how many units each warehouse holds. Use it to show customers where a product is in stock, or to mirror balances from an ERP system. It never changes what can be bought: availability, reservations and checkout only look at sellable stock.

### Set it up in Umbraco

1. On each store node (`ekmStore`), add the warehouses with the **Ekom Warehouse Editor** and publish the store.
2. Give the product or variant a SKU and save it. The stock editor is not usable until a SKU is saved.
3. Enter the balances in the **Warehouse Stock** editor on the product or variant, and save.

A balance belongs to a store, a warehouse and a SKU. Ekom trims the SKU and ignores upper and lower case, so `sku-1` and `SKU-1 ` are the same SKU. If you change the SKU of a product, its old balances stay under the old SKU.

### Read and change it from code

Inject `Ekom.API.Warehouse`, or use `Warehouse.Instance`.

```csharp
using Ekom.API;
using Ekom.Models;

IReadOnlyList<WarehouseDefinition> warehouses =
    await warehouse.GetWarehousesAsync("Store", ct);

IReadOnlyList<WarehouseStockLevel> levels =
    await warehouse.GetForSkuAsync("Store", "SKU-1", ct);

WarehouseStockBalance saved =
    await warehouse.SetAsync("Store", warehouseKey, "SKU-1", 8m, ct);

bool removed =
    await warehouse.ClearAsync("Store", warehouseKey, "SKU-1", ct);
```

A balance cannot be negative. `SetAsync` throws `ArgumentOutOfRangeException` if you pass one.

### Hidden warehouses

A warehouse can be hidden from the storefront. `GetWarehousesAsync`, `GetForSkuAsync` and the `GetAsync` overloads that return a list leave hidden warehouses out. You can still read a hidden warehouse by asking for it by key, and you can still change its balances.

### Change many balances at once

`UpdateAsync` takes a list of changes and reports on each one separately. One bad entry does not stop the others.

```csharp
WarehouseStockBatchResult result = await warehouse.UpdateAsync(
[
    new WarehouseStockMutationRequest
    {
        StoreAlias = "Store",
        WarehouseKey = mainWarehouseKey,
        Sku = "SKU-1",
        Operation = WarehouseStockMutationOperation.Set,
        Balance = 12m,
    },
    new WarehouseStockMutationRequest
    {
        StoreAlias = "Store",
        WarehouseKey = outletWarehouseKey,
        Sku = "SKU-1",
        Operation = WarehouseStockMutationOperation.Clear,
    },
], ct);

foreach (WarehouseStockMutationResult entry in result.Entries)
{
    if (entry.Status == WarehouseStockMutationStatus.Failed)
    {
        logger.LogWarning("Warehouse stock for {Sku} was not saved: {Error}", entry.Sku, entry.Error);
    }
}
```

Each entry ends with one of these statuses: `Inserted`, `Updated`, `Cleared`, `Unchanged` or `Failed`. `result.Failed` gives the number of failed entries.

An entry fails when:

- the warehouse is not configured for that store,
- the balance is negative,
- the same store, warehouse and SKU appear earlier in the same list.

## Running in production

- **More than one server.** The database decides whether stock is available. Each server reads from its own memory, so right after a change another server can show the old count for a short time. An order is always checked against the database.
- **Do not write to the stock tables yourself.** Ekom's memory copy and reservations will not see the change. Use the methods on this page.
- **Imports.** `ImportProduct.Stock` and `ImportVariant.Stock` replace the sellable count. `ImportProduct.WarehouseStock` and `ImportVariant.WarehouseStock` change only the warehouses you list and leave the rest as they are. See [Catalog imports](imports.md).

## Related pages

- [Stock reservations](reservations.md): hold stock for a customer while they pay.
- [Checkout](checkout.md): where stock is checked and deducted during an order.
- [Catalog imports](imports.md): update stock in bulk from another system.
- [Events](../reference/events.md): every event Ekom raises, including stock changes.
- [Configuration reference](../reference/configuration-reference.md): all `Ekom` settings.

[Back to Guides](README.md)

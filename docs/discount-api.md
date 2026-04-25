# Discount API

`Ekom.API.Discounts` is the main C# entry point for reading discounts configured for the current store or a specific store.

## Example

```csharp
using Ekom.API;
using Ekom.Models;

public sealed class DiscountApplicationService
{
    private readonly Discounts _discounts;

    public DiscountApplicationService(Discounts discounts)
    {
        _discounts = discounts;
    }

    public IEnumerable<IDiscount> GetGlobalDiscounts()
    {
        return _discounts.GetGlobalDiscounts("Store");
    }
}
```

## When to use `API.Discounts`

Use `API.Discounts` when you want to:

- list all discounts for the current store
- list all discounts for a specific store
- list only global discounts

Use `API.Order` when you want to apply or remove coupons from an order.

## Injecting `Discounts`

The usual way to work with `API.Discounts` is to inject it.

```csharp
using Ekom.API;

public sealed class DiscountApplicationService
{
    private readonly Discounts _discounts;

    public DiscountApplicationService(Discounts discounts)
    {
        _discounts = discounts;
    }
}
```

You can also access the static instance:

```csharp
var discountsApi = Discounts.Instance;
```

In most application code, constructor injection is the better option.

## Methods

### `GetDiscounts()`

```csharp
IEnumerable<IDiscount> discounts = _discounts.GetDiscounts();
```

Returns all discounts for the current request store.

If Ekom cannot resolve the current store, this returns an empty collection.

This method is sync-only.

### `GetDiscounts(string storeAlias)`

```csharp
IEnumerable<IDiscount> discounts = _discounts.GetDiscounts("Store");
```

Returns all discounts for a specific store alias.

This method is sync-only.

### `GetGlobalDiscounts()`

```csharp
IEnumerable<IDiscount> discounts = _discounts.GetGlobalDiscounts();
```

Returns global discounts for the current request store.

If Ekom cannot resolve the current store, this returns an empty collection.

This method is sync-only.

### `GetGlobalDiscounts(string storeAlias)`

```csharp
IEnumerable<IDiscount> discounts = _discounts.GetGlobalDiscounts("Store");
```

Returns discounts where `GlobalDiscount` is enabled for a specific store alias.

This method is sync-only.

## Notes

- Discount lookup is read-only.
- Discount application and coupon operations are documented on [Order API](api-order-reference.md).
- Discount stock operations are documented on [Stock API](stock-api.md).

## Related pages

- [Discounts Overview](discounts-overview.md)
- [Order Discounts](order-discounts.md)
- [Product Discounts](product-discounts.md)
- [Coupon Discounts](coupon-discounts.md)
- [Order API](api-order-reference.md)
- [Stock API](stock-api.md)

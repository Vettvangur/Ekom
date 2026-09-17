# Discounts and coupons

Ekom supports product discounts, order discounts, automatic global order discounts, coupons, native product discount prices, and stock limits for discounts/coupons. Prices and discount applicability are resolved in the active store and currency.

## Discount types

| Type | Scope |
| --- | --- |
| Product discount | Competes to reduce an eligible product or variant price. |
| Native discount price | `ekmDiscountPrice` target selling price converted into a product-discount candidate. |
| Order discount | Applies to eligible order lines and may be activated by a coupon. |
| Global order discount | `GlobalDiscount = true`; evaluated automatically without a coupon. |

Discount values may be fixed or percentage based. `IDiscount.Amount` returns the fixed amount or a percentage as a fraction (`28.5%` becomes `0.285`). Constraints, included `DiscountItems`, `ExcludeDiscountItems`, stock, and stacking determine applicability.

When an order discount is not stackable, it competes with product discounts. A stackable order discount can apply on top of the product's selected price reduction.

## Read configured discounts

```csharp
using Ekom.API;
using Ekom.Models;

IEnumerable<IDiscount> all = discounts.GetDiscounts("Store");
IEnumerable<IDiscount> automatic = discounts.GetGlobalDiscounts("Store");
```

The overloads without a store alias use the current request store and return an empty sequence when none can be resolved. These APIs read configuration; applying a coupon is an order operation.

## Product discounts

Product discounts are evaluated against product/category paths, configured include/exclude items, price ranges, disabled state, and other constraints. Ekom chooses the effective candidate rather than stacking multiple product discounts.

`DiscountEvents` is an injected singleton, not a static event class. It exposes:

- `BeforeEvaluateDiscountsAsync`, with path, store, price, categories, and ambient `PricingContext`.
- `AfterApplicableDiscountsAsync`, with the same context and a mutable `ApplicableDiscounts` list.

```csharp
discountEvents.AfterApplicableDiscountsAsync += (sender, args) =>
{
    if (!args.PricingContext.TryGetValue("customerGroup", out var group) || group != "member")
    {
        args.ApplicableDiscounts.RemoveAll(discount => discount.Title == "Members");
    }

    return Task.CompletedTask;
};
```

If custom pricing changes by audience, also partition `PriceCache` generation using the same relevant context; otherwise one audience's cached price can be reused for another.

## Native discount price

Products and variants have an optional `ekmDiscountPrice` using the Ekom Price editor. It is a target selling price, not an amount off. A positive target below the normal price becomes a fixed product-discount candidate and competes with configured product discounts. Missing, zero, negative, invalid, or non-reducing values are ignored.

An independently priced variant uses its own target. A variant inheriting its parent's price also inherits the parent's selected discount. Scalar import values apply only to the store's first configured currency; use the normal store/currency price structure for multicurrency data:

```json
{
  "Store": [
    { "Currency": "en-US", "Price": 85 }
  ]
}
```

## Order discounts and quantity rules

Order discounts select reward lines through `DiscountItems` and `ExcludeDiscountItems`. Quantity modes add whole-unit qualification:

| Mode | Behavior |
| --- | --- |
| `None` | Normal order-discount behavior. |
| `Threshold` | Once `RequiredQuantity` qualifying units exist, discount all whole eligible reward units. |
| `Repeating` | Every complete `RequiredQuantity` group unlocks `RewardQuantity` discounted units. |

`QualifyingItems` identifies products/categories counted toward the rule. Invalid rules do not apply: selectors must be present, required quantity must be positive, and repeating reward quantity must be positive. Fractional quantities are rounded down for qualification/allocation. A line matching multiple qualifying selectors is counted once. Limited repeating rewards are assigned to the cheapest eligible units first and move to another line when an equal or better product discount wins.

For "buy 3, get 1 discounted", choose `Repeating`, `RequiredQuantity = 3`, and `RewardQuantity = 1`.

## Apply and remove coupons

```csharp
bool changed = await orderApi.ApplyCouponToOrderAsync("SPRING10", "Store", ct);
await orderApi.RemoveCouponFromOrderAsync("Store", settings: null, ct);
```

HTTP equivalents:

```http
POST /ekom/order/coupon/apply
Content-Type: application/json

{ "coupon": "SPRING10", "storeAlias": "Store" }
```

`POST /ekom/order/coupon/remove?storeAlias=Store` removes it. Both routes use the `order-coupon` rate-limit policy. Apply returns `450` when a valid request does not change the order because a better discount is already selected.

Coupon and master-discount stock are separate identities. Passing a coupon to stock APIs addresses `{discountKey}_{coupon}`; omitting it addresses master stock. Completion marks the order coupon used according to the checkout policy.

## Quote a coupon without creating an order

Configure a secret and call the calculation endpoint:

```json
{
  "Ekom": {
    "OrderDiscountCalculation": {
      "ApiKey": "integration-secret"
    }
  }
}
```

```http
POST /ekom/order-discounts/calculate
X-Ekom-Api-Key: integration-secret
Content-Type: application/json

{
  "couponCode": "SPRING10",
  "storeAlias": "Store",
  "lines": [
    {
      "clientLineId": "basket-1",
      "sku": "SKU-123",
      "variantSku": "VAR-123",
      "quantity": 2,
      "pricingContext": {
        "customerGroup": "member"
      }
    }
  ]
}
```

`variantSku`, `variantKey`, `clientLineId`, and `pricingContext` are optional. `clientLineId` is echoed in the result. The response reports whether the discount applied, constraints/applicable-line state, totals, messages, and each line's prices, discount amount, VAT, and `discountedQuantity`.

The endpoint is disabled when `ApiKey` is empty and compares `X-Ekom-Api-Key` in constant time. It is rate limited. `pricingContext` is a case-insensitive string dictionary made ambient as `Ekom.PricingContext` while each line is priced; Ekom does not interpret its keys.

The same API-key authorization protects `POST /ekom/order-discounts/update-stock` and `POST /ekom/order-discounts/coupon/mark-used`. Expose these integration endpoints only to trusted callers.

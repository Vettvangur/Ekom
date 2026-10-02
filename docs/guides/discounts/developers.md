# Discounts and coupons for developers

[Discounts overview](../discounts.md) | [Editor guides](editors.md)

Use this guide for runtime behavior, .NET and HTTP APIs, custom eligibility rules, and integrations. For Umbraco setup instructions and worked offers, use the [editor guides](editors.md).

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

The member-group example above is custom eligibility logic. Ekom does not infer customer restrictions from a discount title, a coupon code, or its usage limit.

## Native discount price

Products and variants have an optional `ekmDiscountPrice` using the Ekom Price editor. It is a target selling price, not an amount off. A positive target below the normal price becomes a fixed product-discount candidate and competes with configured product discounts. Missing, zero, negative, invalid, or non-reducing values are ignored.

The candidate's fixed amount is `normalPrice - discountPrice`; it is then priced through the ordinary fixed-discount path. With `DiscountAlwaysBeforeVAT` enabled for VAT-inclusive prices, that amount is subtracted before VAT is added back, so the final selling price can differ from the entered target. Verify the configured VAT policy and rounded result rather than assuming `ekmDiscountPrice` always equals the final displayed price.

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

For non-global discounts, `DiscountItems` must overlap the line's product/category paths; an empty inclusion selection does not match all products. `GlobalDiscount` bypasses that inclusion check, broadening reward targets to eligible non-excluded lines, even when `DiscountItems` contains a narrower selection. Exclusions still apply. Threshold/Repeating rule validation still requires nonempty `DiscountItems`, including for global discounts.

Automatic global selection skips discounts linked to any coupon-cache entry, including exhausted codes. Keep automatic offers couponless. Global targeting semantics also apply if a global discount is explicitly reached through a coupon.

| Mode | Behavior |
| --- | --- |
| `None` | Normal order-discount behavior. |
| `Threshold` | Once `RequiredQuantity` qualifying units exist, discount all whole eligible reward units. |
| `Repeating` | Every complete `RequiredQuantity` group unlocks `RewardQuantity` discounted units. |

`QualifyingItems` identifies products/categories counted toward the rule. Invalid rules do not apply: selectors must be present, required quantity must be positive, and repeating reward quantity must be positive. Fractional quantities are rounded down for qualification/allocation. A line matching multiple qualifying selectors is counted once. Limited repeating rewards are assigned to the cheapest eligible units first and move to another line when an equal or better product discount wins.

For "buy 3, get 1 discounted", choose `Repeating`, `RequiredQuantity = 3`, and `RewardQuantity = 1`.

Qualification floors each matching line's quantity separately before summing. Excluded reward items can still count towards qualification if they match `QualifyingItems`; products with Disable Discounts are removed from both qualification and reward processing. When qualifying and reward selectors overlap, reward units also count towards qualification: the example is not necessarily three full-price units plus an additional discounted unit.

Fixed amounts are deducted per discounted unit, not once per order, and unit prices are clamped at zero. Range checks for order discounts use current line amounts for products that allow discounts, not only selected rewards or shipping/payment fees; product-discount ranges use the product unit price.

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

### Coupon usage is code-based, not customer-based

The coupon editor's **Usage limits** maps to `CouponData.NumberAvailable`: the remaining number of uses for that code. Coupon application rejects values of zero or less. `0` does not mean unlimited.

This counter belongs to the coupon record. It is not keyed by customer ID, username, email, or login state. A code with one remaining use is single-use overall, not once per customer. A shared code with ten remaining uses has ten uses available in total, not ten per customer. Generating several codes gives each its own configured allowance.

Applying a code to a basket does not itself decrement this counter. Standard checkout completion calls `CouponRepository.MarkUsedAsync` for the order-level coupon, which decrements `NumberAvailable`. Trusted integrations can also explicitly mark a coupon used. Custom checkout flows must account for this lifecycle; do not equate a successful calculation preview with a consumed use.

Customer-specific restrictions such as "once per customer", membership, or first-order eligibility require custom validation and, where appropriate, customer redemption history. The application-time hook below is one extension point, not a complete checkout enforcement mechanism.

### Usage and stock are separate limits

Coupon usage counts code uses; discount stock is a separate allowance handled by stock/checkout APIs. Coupon and master-discount stock are separate identities. Passing a coupon to stock APIs addresses `{discountKey}_{coupon}`; omitting it addresses master stock. See the [stock guide](../stock.md) for stock management and the [coupon editor guide](coupons.md) for usage examples.

### Reject a coupon before validation

The wholesale-role check below demonstrates **custom** customer eligibility. It is not built-in coupon usage-limit behavior.

Subscribe to the singleton `DiscountEvents.BeforeApplyCouponDiscountAsync` during application startup and unsubscribe during shutdown. The hook runs once at the beginning of either whole-order `ApplyCouponToOrderAsync` overload, before any coupon validation, normalization, lookup, or order loading. The overload without a supplied store alias raises the event before resolving the current store, so its `StoreAlias` argument is null.

```csharp
discountEvents.BeforeApplyCouponDiscountAsync += BeforeCouponAsync;

Task BeforeCouponAsync(object? sender, DiscountEvents.BeforeApplyCouponDiscountEventArgs args)
{
    args.CancellationToken.ThrowIfCancellationRequested();
    if (string.Equals(args.CouponCode, "WHOLESALE10", StringComparison.OrdinalIgnoreCase)
        && httpContextAccessor.HttpContext?.User.IsInRole("Wholesale") != true)
    {
        args.Reject("This coupon is only available to wholesale customers.");
    }

    return Task.CompletedTask;
}
```

Inject `DiscountEvents` and `IHttpContextAccessor` for this example. The code and alias are raw, unvalidated input and may be null or empty. A handler can perform its own customer/order lookups if needed; avoid recursively calling the coupon application API from the handler.

`Reject(reason)` requires a nonblank customer-facing message. The first rejection stops further handlers and throws `CouponApplicationRejectedException` with that message, leaving the existing order, discounts, and totals unchanged. The HTTP endpoint returns 400 with `code=couponApplicationRejected` and `message` containing the reason. Storefronts should display the message as escaped text. Without rejection, existing coupon checks and application continue normally.

This hook does not run for line-level coupons, global discounts, calculation previews, `SetCouponCodeAsync`, or direct discount-service calls. It is an application-time hook, not a replacement for checkout validation or a revalidation of previously applied coupons.

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

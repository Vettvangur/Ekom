[Ekom docs](../../README.md) / [Guides](../README.md) / [Discounts and coupons](../discounts.md) / Discount APIs and behavior

# Discount APIs and behavior

This page is for developers. It explains how Ekom picks and calculates discounts, how to apply coupons from .NET code and over HTTP, how to add your own eligibility rules, and how another system can quote a coupon. To set up discounts in the backoffice, see the [editor guides](editors.md).

Prices and discounts are always worked out for one store and one currency.

On this page:

- [Kinds of discount](#kinds-of-discount)
- [Read configured order discounts](#read-configured-order-discounts)
- [Product discounts](#product-discounts)
- [Discount Price field](#discount-price-field)
- [Order discounts and quantity rules](#order-discounts-and-quantity-rules)
- [Apply and remove coupons](#apply-and-remove-coupons)
- [Reject a coupon with your own rule](#reject-a-coupon-with-your-own-rule)
- [Quote a coupon without creating an order](#quote-a-coupon-without-creating-an-order)

## Kinds of discount

| Kind | Document type | What it does |
| --- | --- | --- |
| Product discount | `ekmProductDiscount` | Competes to lower the price of a product or variant. The best one wins. |
| Discount Price field | `ekmDiscountPrice` property on `ekmProduct` and `ekmProductVariant` | A sale price. Ekom turns it into one more fixed product discount, titled "Sale price". |
| Order discount | `ekmOrderDiscount` | Lowers the price of order lines in a basket. Applied by a coupon, or automatically. |
| Global order discount | `ekmOrderDiscount` with `globalDiscount` on | An order discount that Ekom applies automatically, without a coupon. |

`IDiscount.Type` is `DiscountType.Fixed` or `DiscountType.Percentage`. `IDiscount.Amount` returns the fixed amount, or a percentage as a fraction: an editor value of `28.5` returns `0.285`. The amount is read for the currency in the customer's currency cookie, and falls back to the first currency in the value.

A basket holds at most one order discount (`IOrderInfo.Discount`) and its coupon (`IOrderInfo.Coupon`). Each order line can also carry a product discount.

## Read configured order discounts

Inject `Ekom.API.Discounts`, or use `Discounts.Instance`.

```csharp
using Ekom.API;
using Ekom.Models;

IEnumerable<IDiscount> orderDiscounts = discounts.GetDiscounts("Store");
IEnumerable<IDiscount> automatic = discounts.GetGlobalDiscounts("Store");
```

- These methods return order discounts only. Product discounts are not included.
- A discount that is disabled for the store, directly or through its folder, is not returned for that store.
- The overloads without a store alias use the store of the current request. Outside a request Ekom falls back to the first store and gives no error, so pass the store alias in background code.
- Reading discounts does not change any basket. To use a coupon, see [Apply and remove coupons](#apply-and-remove-coupons).

## Product discounts

For each product or variant price, Ekom collects the product discounts that fit and picks one:

1. A discount fits when the product's content path, or one of its categories, is in `Discount Items`, and none of them is in `Exclude Discount Items`. An empty `Discount Items` matches nothing.
2. A discount marked disabled for the store is skipped.
3. A discount is skipped when the product's unit price is outside `Start of Range` and `End of Range` (both inclusive, `End of Range` `0` means no limit), or when it would take off nothing.
4. Of the rest, Ekom keeps the discount that takes the most off the unit price. Product discounts are never added together.

A product with **Disable Discounts** (`disableDiscounts`) turned on gets no product discount and no order discount, and its variants get none either.

### Change which product discounts apply

`DiscountEvents` is a singleton that you inject. It is not a static class. It has two events for product discounts:

- `BeforeEvaluateDiscountsAsync` runs before Ekom looks for discounts. Its arguments (`Path`, `StoreAlias`, `Price`, `Categories`) can be changed, and Ekom uses the changed values.
- `AfterApplicableDiscountsAsync` runs after Ekom has found the discounts that fit. Add to or remove from `ApplicableDiscounts`. Ekom then picks the best one from that list.

Both give you `PricingContext`, a read-only, case-insensitive dictionary.

```csharp
using Ekom.Events;

discountEvents.AfterApplicableDiscountsAsync += (sender, args) =>
{
    bool isMember = args.PricingContext.TryGetValue("customerGroup", out string? group)
        && group == "member";

    if (!isMember)
    {
        args.ApplicableDiscounts.RemoveAll(discount => discount.Title == "Members");
    }

    return Task.CompletedTask;
};
```

This is your own rule. Ekom never limits a discount by its title, its coupon code or its usage limit.

`PricingContext` is empty unless something activates it. Ekom activates it per line in the [order-discount calculation endpoint](#quote-a-coupon-without-creating-an-order). In your own code, wrap the pricing work in `using var scope = PricingContext.Activate(values);`.

Ekom caches product prices. If your rule makes the price depend on the audience, also make the price cache depend on it: handle `PriceCache.OnGenerationCreatedAsync`, whose arguments include `PricingContext`, and set `Generation` to a value that includes the audience. Otherwise a price worked out for one audience can be shown to another.

## Discount Price field

`ekmDiscountPrice` uses the Ekom Price editor, like `price`. It holds the price to sell at, not the amount to take off.

- When the value is above 0 and below the normal price, Ekom adds a fixed product discount of `normalPrice - discountPrice` to the candidates. It competes with the configured product discounts.
- An empty, zero, negative, unreadable or non-reducing value adds nothing.
- A variant with its own price uses its own `ekmDiscountPrice`. A variant without its own price (empty or `0`) uses the product's price, the product's `ekmDiscountPrice` and the product's discounts.
- A fixed discount is taken off the price as stored. `Price.DiscountAlwaysBeforeVAT` changes this for VAT-inclusive prices: the amount is then taken off the price without VAT, and VAT is added back. The property is `false` by default and no Ekom setting turns it on. When it is `true`, the final price can differ from the entered sale price.
- When [database price storage](../database-price-storage.md) is on, Ekom reads `ekmDiscountPrice` through it.

A plain number, as an import can write it, belongs to the store's first currency only. For more than one currency, use the store and currency structure:

```json
{
  "Store": [
    { "Currency": "en-US", "Price": 85 },
    { "Currency": "is-IS", "Price": 12900 }
  ]
}
```

## Order discounts and quantity rules

### Which lines get the discount

A line can get an order discount when all of these are true:

1. The product does not have **Disable Discounts** turned on.
2. The basket amount is inside `Start of Range` and `End of Range` (both inclusive, `End of Range` `0` means no limit). The basket amount is the sum of the line amounts of all products that allow discounts. Shipping and payment fees are not part of it.
3. The line matches the targets: its product path, or the paths of its categories, overlap `Discount Items`, and do not overlap `Exclude Discount Items`. When `GlobalDiscount` is `true`, the `Discount Items` check is skipped and every line that is not excluded matches.

An empty `Discount Items` matches nothing, unless `GlobalDiscount` is `true`.

### How the order discount meets a product discount

- **Stackable off:** for each line, Ekom compares the line total with the product discount and with the order discount, and keeps the lower one. When they are equal, the product discount is kept.
- **Stackable on:** the order discount is calculated from the price after the product discount.

A fixed amount is taken off each discounted unit, not once per order. A unit price never goes below 0.

### Quantity modes

`IDiscount.QuantityDiscountMode` is `OrderDiscountQuantityMode.None`, `Threshold` or `Repeating`.

| Mode | Behavior |
| --- | --- |
| `None` | Every matching line gets the discount on its full quantity. |
| `Threshold` | Once the basket holds `RequiredQuantity` qualifying units, every whole unit of every matching line gets the discount. |
| `Repeating` | Each complete group of `RequiredQuantity` qualifying units unlocks `RewardQuantity` discounted units. |

For "buy 3, get 1 discounted", use `Repeating`, `RequiredQuantity = 3` and `RewardQuantity = 1`.

Rules for `Threshold` and `Repeating`:

- The rule is valid only when `RequiredQuantity` is above 0, `QualifyingItems` is not empty and `DiscountItems` is not empty. `Repeating` also needs `RewardQuantity` above 0. An invalid rule never applies. This is true for global discounts too, even though `GlobalDiscount` skips the `DiscountItems` check when it picks the reward lines.
- Qualifying units are counted from lines that match `QualifyingItems`. `ExcludeDiscountItems` is not applied to this count. Lines whose product has **Disable Discounts** are left out.
- Each line's quantity is rounded down to whole units before it is counted, and before rewards are given.
- A line that matches several entries in `QualifyingItems` is counted once.
- When the reward is limited, Ekom gives it to the cheapest matching units first. With `Stackable` off, the price used is the price before product discounts; with `Stackable` on, the price after them. If a unit's existing product discount is equal or better, Ekom skips that line and gives the reward to the next one.
- When qualifying and reward items overlap, reward units also count as qualifying units. "Buy 3, get 1" can mean that one of the three is the discounted unit.

### When Ekom applies and removes order discounts

Each time a basket is saved, Ekom:

1. Removes the order discount (and its coupon) if its range is no longer met, or if no line gets any of it.
2. Removes line discounts that no longer fit, and all discounts from lines whose product has **Disable Discounts**.
3. Tries every global order discount of the store. A global discount is skipped when any coupon code exists for it, including a code with no uses left. A global discount replaces the current order discount only when it gives a lower total.

## Apply and remove coupons

```csharp
using Ekom.API;

bool applied = await order.ApplyCouponToOrderAsync("SPRING10", "Store", ct);

await order.RemoveCouponFromOrderAsync("Store", settings: null, ct);
```

`ApplyCouponToOrderAsync` lowercases the code, finds it, and applies its order discount to the current basket of that store. It returns `true` when the discount was applied. It returns `false` when there is no basket, when the discount does not fit the basket, or when the basket already has an order discount that gives an equal or lower total.

It throws:

| Exception | When |
| --- | --- |
| `ArgumentException` | The code or the store alias is empty. |
| `DiscountNotFoundException` | No code with that value exists. |
| `DiscountHasNoUsageException` | The code has 0 uses left. |
| `DiscountUnableToFindCouponException` | The code's discount is not available in that store, for example because it is disabled there or not published. |
| `DiscountDuplicateException` | The basket already has this discount. |
| `CouponApplicationRejectedException` | A handler of `BeforeApplyCouponDiscountAsync` rejected the code. See [Reject a coupon with your own rule](#reject-a-coupon-with-your-own-rule). |

`RemoveCouponFromOrderAsync` removes the order discount only when the basket has a coupon. It does nothing to an order discount that was applied without a coupon. The store alias is required; an empty one throws `ArgumentException`.

`SetCouponCodeAsync` only writes the code on the order. It does not check the code and does not apply a discount.

### Over HTTP

```http
POST /ekom/order/coupon/apply
Content-Type: application/json

{ "coupon": "SPRING10", "storeAlias": "Store" }
```

```http
POST /ekom/order/coupon/remove?storeAlias=Store
```

Responses from `coupon/apply`:

| Status | Meaning |
| --- | --- |
| `200` | The discount was applied. The body is empty. Read the basket again to show the new totals. |
| `450` | Not applied: the discount does not fit the basket, or a better discount is already on it. The body is "Discount not modified, better discount found". |
| `400` | The code is empty (body "Coupon code can not be empty"), or a handler rejected it (body `{ "code": "couponApplicationRejected", "message": "..." }`). |
| `404` | The code does not exist, or its discount is not available in this store. |
| `406` | The code has no uses left. |
| `429` | Too many requests from this IP address. |
| `500` | Any other error, including an empty `storeAlias` and applying the same discount twice. |

`coupon/remove` returns `200` with an empty body. Both routes use the `order-coupon` rate-limit policy: 13 requests at most, refilled by 10 per minute, for each client IP address.

### Usage limits count uses of a code, not customers

The **Usage limits** field in the coupon editor is `CouponData.NumberAvailable`: the uses left for that code. Applying a code with `NumberAvailable` of 0 or less throws `DiscountHasNoUsageException`. `0` does not mean unlimited.

The counter belongs to the code. It is not tied to a customer ID, user name, email address or login. A code with one use left can be used once in total. A shared code with ten uses left has ten uses in total. Each generated code has its own counter.

Applying a code does not change the counter. When an order completes, checkout calls `CouponRepository.MarkUsedAsync` for the order's coupon. It takes 1 off `NumberAvailable` in one conditional database update, so the value never goes below 0 even when orders complete at the same time. If no use was left, the order still completes; Ekom logs a warning, raises an order review with reason `coupon-exhausted`, and writes an alert to the order's activity log.

Rules such as "once per customer", "members only" or "first order only" need your own check, and for "once per customer" a record of which customer used which code. The [rejection hook](#reject-a-coupon-with-your-own-rule) is one place for such a check, but it runs only when a code is applied, not at checkout.

### Usage limits and discount stock are separate

Discount stock is a separate count, read and changed through `Ekom.API.Stock` (see [Discount stock](../stock.md#discount-stock)). Changing one does not change the other.

- Stock for a whole discount uses the discount key. Stock for one code uses `{discountKey}_{coupon}`. Pass the coupon to the stock methods to address the code's stock; leave it out for the discount's stock.
- Checkout reserves 1 unit of the discount's stock for each order line whose discount has `IDiscount.HasMasterStock` set to `true`. `HasMasterStock` reads a `masterStock` property on the discount node. Ekom does not add this property to `ekmOrderDiscount`; add a true/false property with that alias yourself to use it.
- Checkout also reserves 1 unit of code stock for each order line that has its own line-level coupon. The order-level coupon from `ApplyCouponToOrderAsync` is counted with `MarkUsedAsync` instead, and does not use discount stock.
- If there is not enough discount stock when the reservation completes, Ekom throws `NotEnoughStockException`. See [Stock reservations](../reservations.md).

## Reject a coupon with your own rule

Subscribe to `DiscountEvents.BeforeApplyCouponDiscountAsync` when the application starts, and unsubscribe when it stops. The example lets only wholesale customers use one code.

```csharp
using Ekom.Events;
using Microsoft.AspNetCore.Http;

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

Inject `DiscountEvents` and `IHttpContextAccessor` for this example.

How the hook works:

- It runs once, at the start of either `ApplyCouponToOrderAsync` overload, before Ekom checks, lowercases or looks up the code, and before it loads the basket.
- `CouponCode` and `StoreAlias` are the raw input. Either can be null or empty. The overload without a store alias raises the event before it resolves the store, so `StoreAlias` is null there.
- `Reject(reason)` needs a message that is not empty or white space. The first rejection wins: later handlers do not run.
- After a rejection, Ekom throws `CouponApplicationRejectedException` with your message, and the basket, its discounts and its totals stay as they were. Over HTTP, this is a `400` with `code` `couponApplicationRejected` and your `message`. Show the message to the customer as plain, escaped text.
- Without a rejection, Ekom continues as normal.
- Your handler can look up customers or orders itself. Do not call the coupon API from inside the handler.

The hook does not run for line-level coupons, global discounts, the [calculation endpoint](#quote-a-coupon-without-creating-an-order), `SetCouponCodeAsync`, or direct calls to the order service. It does not check coupons that are already on a basket again, and it does not replace a check at checkout.

## Quote a coupon without creating an order

Another system, such as a point-of-sale or an ERP, can ask Ekom what a coupon would do to a set of lines. Ekom works this out without a basket and without changing any data.

1. Set a secret in configuration. The endpoint is closed while the value is empty, and it is empty by default.

   ```json
   {
     "Ekom": {
       "OrderDiscountCalculation": {
         "ApiKey": "replace-with-a-long-random-secret"
       }
     }
   }
   ```

2. Call the endpoint with the secret in the `X-Ekom-Api-Key` header.

   ```http
   POST /ekom/order-discounts/calculate
   X-Ekom-Api-Key: replace-with-a-long-random-secret
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

Request fields:

| Field | Required | Notes |
| --- | --- | --- |
| `couponCode` | Yes | Lowercased before lookup. |
| `storeAlias` | No | Without it, the store of the current request is used. |
| `lines` | Yes, at least one | |
| `lines[].sku` | Yes | The product SKU. |
| `lines[].variantSku` or `lines[].variantKey` | No | The variant must belong to the product. `variantKey` wins when both are set. |
| `lines[].quantity` | No | Default `1`. Must be above 0. |
| `lines[].clientLineId` | No | Your own line ID, returned in the result. Must not be blank, and must be unique in the request. |
| `lines[].pricingContext` | No | Strings that Ekom makes the active `PricingContext` while it prices that line. Ekom does not read the keys itself; your handlers do. |

The response has `applied`, `orderConstraintsMet`, `hasApplicableLines`, `couponCode`, `discountId`, `discountTitle`, `currency`, `subTotal`, `discountTotal`, `grandTotal`, `messages`, and a `lines` list. Each line has `clientLineId`, `sku`, `variantSku`, `quantity`, `discountedQuantity`, `couponApplicable`, `unitPriceBeforeDiscount`, `lineTotalBeforeDiscount`, `discountAmount`, `lineTotalAfterDiscount`, `vat` and `discountApplied`.

Responses:

| Status | Meaning |
| --- | --- |
| `200` | The result. `applied` is `false` when the range or quantity rule is not met; `messages` says which. |
| `401` | The key is missing or wrong, or no key is configured. |
| `404` | The code, its discount in this store, a product or a variant was not found. |
| `406` | The code has no uses left. |
| `429` | Too many requests (`order-coupon` policy). |
| `500` | Any other error, including an unknown store, no lines, a duplicate `clientLineId` or a quantity of 0. |

Ekom compares the key in constant time.

### Other endpoints that use the same key

| Route | Body | Effect |
| --- | --- | --- |
| `POST /ekom/order-discounts/update-stock` | `{ "key": "<discount key>", "value": -1, "coupon": null }` | Changes discount stock by `value`. `value` `0` returns `400`. Not rate limited. |
| `POST /ekom/order-discounts/coupon/mark-used` | `{ "coupon": "spring10" }` | Takes one use off the code. Returns `200`, or `409` when the code does not exist or has no uses left. |

`coupon/mark-used` does not lowercase the code. Send it in lower case, as Ekom stores it.

Call these endpoints only from servers you trust. Keep the key out of browser code.

## Related pages

- [Order discounts](order-discounts.md): the editor's view of the same rules, with worked examples.
- [Coupons](coupons.md): adding and generating codes in the backoffice.
- [Stock](../stock.md): discount stock and the `Stock` API.
- [Headless storefronts](../headless.md): the other HTTP endpoints a storefront uses.
- [Events](../../reference/events.md): every event Ekom raises.
- [Configuration reference](../../reference/configuration-reference.md): all `Ekom` settings.

[Back to Guides](../README.md)

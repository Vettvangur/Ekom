# Orders and baskets

In Ekom, a basket is the current non-final order for a store and customer context. The same persisted model becomes an order as it moves through checkout and fulfillment. Use current-basket methods for shopping flows and explicit order-ID methods only where the caller is authorized to read that persisted order.

## Basket identity and resolution

Read the current basket through the injected `Ekom.API.Order` facade:

```csharp
using Ekom.API;
using Ekom.Models;

IOrderInfo? basket = await orderApi.GetOrderAsync("Store", ct);
```

`GetOrderAsync()` resolves the current request store. `GetOrderAsync(storeAlias)` is safer when the store is already known and is required outside a storefront request.

For a normal cookie-backed basket, Ekom reads a GUID from:

- `ekmOrder-{StoreAlias}` when baskets are isolated per store.
- `ekmOrder` when the store's `ShareBasketBetweenStores` setting is enabled.

The cookie is created when Ekom creates the first empty order as part of adding a line; merely reading the basket does not create one. Its lifetime comes from `Ekom:BasketCookieLifetime` in days. Ekom writes it for `/`, with `SameSite=Lax`, `Secure` on HTTPS requests, and currently `HttpOnly=false`.

When the store's `UserBasket` setting is enabled and an authenticated member is available, Ekom uses the member's saved `orderId` instead. Finalizing a member basket clears that reference. Code that supports both modes should always use the `Order` facade rather than reading cookies or member properties directly.

Current-basket methods return `null` when there is no basket or when the referenced order has a status Ekom considers final. `WaitingForPayment` is not currently classified as final, so a payment attempt can still be returned by current-basket lookup. Treat it as payment-owned state and do not offer mutation while provider confirmation is outstanding.

`DeleteOrderCookie(storeAlias)` removes a cookie-backed basket reference. `EnsureOrderCookie(orderId, storeAlias)` restores a missing cookie, as the built-in payment return path does, but deliberately does not replace a different non-empty basket cookie. It is a recovery tool, not an order switcher, and has no effect for member baskets.

Browser and headless clients must retain cookies for the entire flow. See [Headless storefronts](headless.md#store-and-basket-context).

## Add order lines

Adding the first line creates an `Incomplete` order when no basket exists:

```csharp
IOrderInfo basket = await orderApi.AddOrderLineAsync(
    productKey,
    quantity: 2,
    storeAlias: "Store",
    settings: new AddOrderSettings
    {
        VariantKey = variantKey,
        OrderAction = OrderAction.AddOrUpdate,
    },
    ct);
```

Ekom resolves the product and optional variant in the selected store, verifies that the variant belongs to the product, validates required variant selection and stock, updates totals and discounts, and persists the order. Relevant failures are exceptions such as missing product/variant, required variant, invalid quantity, or insufficient stock.

`OrderAction` controls line identity and quantity behavior:

| Action | Behavior |
| --- | --- |
| `AddOrUpdate` | Default. Add the quantity to a matching line or create it. Negative deltas can reduce a line, but cannot produce a negative quantity. |
| `Set` | Set the matching line to the supplied quantity. |
| `New` | Always create a separate line, even for the same product/variant. |

`AddOrderSettings` can also carry line custom data, consent, tracking, and an Algolia query ID. Store application-specific line values under `CustomData`; only keys using Ekom's `orderline*` convention become `OrderLineInfo` properties.

The HTTP equivalent is `POST /ekom/order/add`. It accepts JSON or form data and can include `productId`, `variantId`, `storeAlias`, decimal `quantity`, `action`, consent/tracking, and linked products. Consult the [HTTP API reference](../reference/http-api.md#mutations) for the current transport contract rather than duplicating the endpoint surface here.

## Update and remove lines

Use the stable line `Key`, not the product key, when editing a specific line:

```csharp
basket = await orderApi.UpdateOrderlineQuantityAsync(
    lineKey,
    quantity: 3,
    storeAlias: "Store",
    ct: ct);

basket = await orderApi.RemoveOrderLineAsync(
    lineKey,
    "Store",
    ct: ct);
```

Setting quantity to zero or less through the C# API removes the line. The HTTP quantity-update model accepts a non-negative integer, whereas add requests and the C# methods use decimal quantities.

`RemoveOrderLineProductAsync(productKey, storeAlias, settings)` is available when product/variant identity is all the caller has, but a basket may contain multiple separate lines for the same product. Prefer removal by line key to avoid ambiguity.

All mutations return the recalculated `IOrderInfo`. Render totals and line state from that result instead of adjusting client totals optimistically.

## Linked lines

Linked lines model a parent product plus independently quantified child services or products. Create the complete group atomically with `AddLinkedOrderLinesAsync` or the `linkedProducts` add payload. A child stores its direct parent line key in `OrderLineSettings.Link`.

Children are otherwise normal lines: they contribute to quantity, stock, discounts, tax, and totals, and can be updated or removed independently. Removing a parent also removes its directly linked children. Removing a child does not remove its parent or siblings.

See [Linked order lines](linked-order-lines.md) for request examples, validation, custom data, and atomic persistence behavior.

## Customer, consent, and tracking data

Customer and shipping values are stored on the active order:

```csharp
basket = await orderApi.UpdateCustomerInformationAsync(
    new Dictionary<string, string>
    {
        ["storeAlias"] = "Store",
        ["customerName"] = "Jane Doe",
        ["customerEmail"] = "jane@example.com",
        ["customerAddress"] = "Example Street 1",
        ["customerCity"] = "Reykjavik",
        ["customerZipCode"] = "101",
        ["shippingName"] = "Jane Doe",
    },
    ct: ct);
```

The dictionary must contain `storeAlias`. Keys beginning with lowercase `customer` update customer properties; keys beginning with lowercase `shipping` update shipping properties. Customer email is validated when supplied. The built-in payment validation requires a non-empty customer name and email.

`OrderSettings.Consent` and `OrderSettings.Tracking` can accompany line or customer updates. `UpdateTrackingAsync` explicitly replaces tracking and optional consent while the order is still mutable. See [Tracking and consent](tracking-and-consent.md) for capture and privacy behavior.

Adding the first non-empty customer email raises `CustomerEmailAddedAsync`, which optional tracking integrations use as the checkout-started signal.

## Shipping and payment providers

Provider selection saves an ordered snapshot and recalculates the basket; it does not submit payment:

```csharp
basket = await orderApi.UpdateShippingInformationAsync(
    shippingProviderKey,
    "Store",
    new Dictionary<string, string>(),
    ct: ct);

basket = await orderApi.UpdatePaymentInformationAsync(
    paymentProviderKey,
    "Store",
    new Dictionary<string, string>(),
    ct: ct);
```

Resolve available providers for the same store, destination country, and order amount before accepting a selection. Additional dictionary values are stored with the ordered provider and can also update recognized customer fields. Treat all client-provided provider data as untrusted. See [Payment and shipping providers](providers.md).

Currency belongs to the store and order context as well. See [Stores](stores.md#currencies) before offering a basket currency switch.

## Coupons and gift cards

Apply or remove an order coupon through `Order`:

```csharp
bool changed = await orderApi.ApplyCouponToOrderAsync("SPRING10", "Store", ct);
await orderApi.RemoveCouponFromOrderAsync("Store", settings: null, ct);
```

Coupon input is normalized to lower case. Missing, exhausted, or store-inapplicable coupons raise discount exceptions. A valid coupon may still lose to a better discount; the HTTP apply route reports that no-change case with status `450`. Coupon usage is marked during trusted checkout completion, not merely when the code is attached to a basket. Full rule and stock behavior is documented in [Discounts and coupons](discounts.md).

Gift cards are attached separately:

```csharp
basket = await orderApi.AddGiftcardAsync(
    new Giftcard
    {
        Code = "GIFT-100",
        Amount = 100m,
        ValidUntil = DateTime.UtcNow.AddMonths(6),
    },
    "Store",
    ct: ct);

basket = await orderApi.RemoveGiftcardAsync("GIFT-100", "Store", ct: ct);
```

Codes must be non-empty, amounts must be positive, and duplicate codes are rejected case-insensitively. Applicable gift-card amounts reduce `ChargedAmount` and `GrandTotal`, never below zero. The core add operation accepts a `Giftcard` value; it does not itself prove that the code, balance, claim, or client-supplied amount came from a trusted gift-card system. Validate and claim gift cards at the application's trust boundary before calling it, and protect the public gift-card route if exposing it would allow untrusted values.

## Statuses and lifecycle

A typical built-in flow is:

1. The first line creates an `Incomplete` order.
2. Customer and provider updates enrich and recalculate that order.
3. Online payment submission changes it to `WaitingForPayment` before the provider request.
4. A trusted successful payment integration calls the completion pipeline.
5. Completion validates/consumes stock work, marks the coupon used, writes an activity log, clears a final member-basket reference, and normally moves the order to `ReadyForDispatch`.
6. An offline provider sets `OfflinePayment` and completes immediately without replacing that status with `ReadyForDispatch`.
7. Fulfillment or manager workflows can later use statuses such as `ReadyForPickup`, `ReadyForDispatchWhenStockArrives`, `Dispatched`, `Returned`, `Cancelled`, or `Closed`.

The enum also includes `PaymentFailed`, `Pending`, `Wishlist`, and the states above. Ekom's `Order.IsOrderFinal(status)` classification, which controls current-basket reads, currently treats these as final:

- `OfflinePayment`
- `Pending`
- `ReadyForDispatch`
- `ReadyForPickup`
- `ReadyForDispatchWhenStockArrives`
- `Dispatched`
- `Returned`
- `Cancelled`
- `Closed`

`Incomplete`, `WaitingForPayment`, `PaymentFailed`, and `Wishlist` are not classified as final. This classification is an Ekom basket rule, not a claim that every non-final status should remain user-editable.

`UpdateStatusAsync` changes status and raises status events; it is not equivalent to checkout completion. Use `CompleteOrderAsync(orderId)` only from a trusted payment or offline integration after the payment outcome has been verified. Completion performs stock, coupon, logging, and customer-reference work that a direct status change does not.

## Checkout handoff

Once the basket has customer data and selected providers, call the payment pipeline rather than changing status yourself:

```csharp
CheckoutResponse response = await orderApi.PayAsync(
    new PaymentRequest
    {
        PaymentProvider = paymentProviderKey,
        ShippingProvider = shippingProviderKey,
        StoreAlias = "Store",
        Culture = "en-US",
        ReturnUrl = "/checkout/receipt",
    },
    "Store",
    basket.UniqueId,
    ct);
```

Depending on the provider, the response can contain provider HTML, a redirect, a validation error, or a stock error. A browser return is not proof of payment. The payment integration must verify the provider result and invoke completion server-side. See [Checkout](checkout.md) for the preparation, reservation, online/offline, callback, and response behavior.

## Access final orders safely

Choose the read API according to intent:

| Method | Intended use |
| --- | --- |
| `GetOrderAsync(storeAlias)` | Current non-final basket identified by member or cookie context. |
| `GetCompletedOrderAsync(storeAlias)` | Final order still identified by the current member/cookie context, suitable for a receipt immediately after completion. |
| `GetOrderAsync(orderId)` | Any persisted order by GUID, including final orders. It does not enforce cart semantics or customer ownership. |

For a receipt page, carry the completed order ID through the trusted checkout result, load it server-side, verify it belongs to the signed-in customer or protected checkout session, and ensure its status is appropriate before rendering customer data. A GUID is not authorization.

The public `GET /ekom/order/{orderId}` endpoint has no authorization attribute in Ekom source. Protect it at the host/proxy or avoid exposing it where order identifiers and customer details must remain confidential. Do not use unrestricted ID lookup to populate a basket page; it can return final orders that must no longer be mutated.

## Order events

`OrderEvents` provides hooks for order updates, customer information, status changes, lines, and provider selection. The most relevant async events are:

- `OrderUpdatingAsync` and `OrderUpdatedAsync`
- `CustomerEmailAddedAsync`
- `CustomerInformationUpdatingAsync` and `CustomerInformationUpdatedAsync`
- `OrderStatusChangingAsync` and `OrderStatusChangedAsync`
- `AddingOrderlineAsync`, `AddedOrderlineAsync`, `UpdatedOrderlineAsync`, and `RemovedOrderlineAsync`
- `ShippingProviderAddedAsync` and `PaymentProviderAddedAsync`

Checkout has separate `CheckoutEvents` hooks for payment preparation, processing, payment items, and completion. These can modify pipeline behavior, including stock validation and status updates, so cover handlers with integration tests.

`OrderSettings.FireEvents` is the mutation master switch. More specific flags can suppress order-updated or status-changing notifications. Handlers may run while Ekom holds an order mutation lock; avoid recursively mutating the same order unless deliberately using the event-handler settings path. Keep completion handlers idempotent because payment callbacks can retry, and unsubscribe static handlers during application shutdown.

See the [Events reference](../reference/events.md#order-events) for event arguments, mutable values, sync compatibility events, and handler guidance.

## Related reference

- [.NET Order and checkout API](../reference/dotnet-api.md#order-and-checkout)
- [HTTP orders and carts API](../reference/http-api.md#orders-and-carts)
- [Models and behavior](../reference/models-and-behavior.md#orders)
- [Checkout](checkout.md)
- [Stock reservations](reservations.md)
- [Headless storefronts](headless.md)

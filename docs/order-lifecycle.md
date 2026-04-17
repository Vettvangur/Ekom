# Order Lifecycle

This page describes how an order typically moves through Ekom from an active basket to a completed order.

It is written for developers who need to understand where order state changes happen, what side effects occur, and which API methods or services are responsible.

## Lifecycle overview

At a high level, an Ekom order usually moves through these stages:

1. a basket/current order is created or loaded
2. order lines are added or updated
3. customer information is updated
4. checkout is considered started
5. shipping and payment providers are assigned
6. payment is processed or offline payment is selected
7. the order is completed
8. the order moves into a final or near-final status

Along the way, Ekom can also:

- apply discounts and coupons
- update tracking and consent data
- write order activity logs
- raise events

## 1. Basket creation and current order resolution

Most commerce flows begin by loading the current order for the active store.

```csharp
IOrderInfo? order = await _order.GetOrderAsync(ct);
```

If there is no active order yet, Ekom can create one as part of normal add-to-order and checkout flows.

### Important behavior

- active basket resolution is store-aware
- basket persistence can depend on cookie and store configuration
- some setups may use member-linked basket behavior when configured

## 2. Adding products to the order

Products are added through `API.Order.AddOrderLineAsync(...)`.

```csharp
IOrderInfo order = await _order.AddOrderLineAsync(
    productId,
    1,
    "Store",
    ct: ct);
```

### What happens here

- Ekom validates the product and quantity
- existing lines may be updated instead of creating a new line
- totals and order data are recalculated
- the order is persisted

### Activity log behavior

If a brand new order line is created, Ekom adds an `Info` log entry:

- `Order line added. Product: {ProductTitle}`

If the existing line is only updated, this specific activity log is not written.

## 3. Updating customer information

Customer information is updated through `UpdateCustomerInformationAsync(...)`.

```csharp
IOrderInfo order = await _order.UpdateCustomerInformationAsync(
    new Dictionary<string, string>
    {
        ["storeAlias"] = "Store",
        ["customerEmail"] = "customer@example.com",
        ["customerName"] = "Jane Doe"
    },
    ct: ct);
```

### What happens here

- customer fields are copied into the order
- shipping and payment provider values in the same payload can also be processed
- order tracking and consent can be updated in the same broader flow
- the order is persisted and order-updated events may fire

## 4. Checkout started

Ekom currently treats checkout as started when customer email is added for the first time.

In the core flow this happens when:

- the previous customer email is empty
- the new customer email is not empty

### Why this matters

This is the point where the order becomes more than just a basket with products. It now has enough customer identity to behave like an active checkout.

### Related behavior

- the `CustomerEmailAdded` event flow runs
- integrations can hook into this stage
- activity logging may be triggered by the core order flow around checkout state changes

## 5. Updating shipping provider

Shipping selection happens through `UpdateShippingInformationAsync(...)`.

```csharp
IOrderInfo order = await _order.UpdateShippingInformationAsync(
    shippingProviderId,
    "Store",
    new Dictionary<string, string>(),
    ct: ct);
```

### What happens here

- the provider is resolved for the store
- provider validity is checked
- an `OrderedShippingProvider` is created and assigned to the order
- provider-related customer/order data is refreshed
- the order is persisted

### Activity log behavior

If the shipping provider actually changed, Ekom writes an `Info` log entry:

- `Shipping provider added. Provider: {ProviderTitle}`

If the same provider is submitted again, that log is not written.

## 6. Updating payment provider

Payment selection happens through `UpdatePaymentInformationAsync(...)`.

```csharp
IOrderInfo order = await _order.UpdatePaymentInformationAsync(
    paymentProviderId,
    "Store",
    new Dictionary<string, string>(),
    ct: ct);
```

### What happens here

- the payment provider is resolved for the store
- an `OrderedPaymentProvider` is assigned to the order
- related order data is updated
- the order is persisted

### Activity log behavior

If the payment provider actually changed, Ekom writes an `Info` log entry:

- `Payment provider added. Provider: {ProviderTitle}`

## 7. Tracking and consent updates

Tracking data can be attached to the order during the lifecycle.

```csharp
IOrderInfo order = await _order.UpdateTrackingAsync(
    "Store",
    new OrderTracking
    {
        LandingUrl = "https://example.com/products/shoe",
        ReferrerUrl = "https://google.com"
    },
    ct: ct);
```

### Typical use cases

- store consent decisions on the order
- capture landing/referrer information
- make tracking data available for purchase dispatch or integrations later

## 8. Payment submission

When the order is ready to be submitted to payment, the payment flow is entered through `PayAsync(...)`.

```csharp
CheckoutResponse response = await _order.PayAsync(
    paymentRequest,
    "Store",
    orderId,
    ct);
```

This hands off the order into the payment-processing flow.

### Notes

- online payment and offline payment can diverge here
- payment completion does not happen just by assigning the provider
- this stage is part of the handoff into the checkout completion pipeline

## 9. Completing the order

Order completion happens through `CompleteOrderAsync(...)`.

```csharp
await _order.CompleteOrderAsync(orderId, ct);
```

Internally, `CheckoutService.CompleteAsync(...)` handles the completion process.

### What happens during completion

- the order is loaded from the repository
- stock validation and stock updates can run
- coupon usage can be finalized
- discount stock usage can be updated
- checkout completion events can fire
- the order status may be updated
- a success activity log entry is written

## 10. Completion outcomes and status behavior

For normal paid completion, Ekom may move the order to:

- `ReadyForDispatch`

For offline payment flows, the lifecycle differs slightly:

- the order may already be in `OfflinePayment`
- completion still finalizes the order
- status update behavior can differ from online payment

### Built-in completion activity logs

Current success logs include:

- `Order Completed.`
- `Order Completed. Offline payment.`

Both are written as `Success` activity log entries.

## 11. Manual status changes

Orders can also be updated manually through status changes.

```csharp
await _order.UpdateStatusAsync(
    OrderStatus.ReadyForDispatch,
    orderId,
    "admin@example.com",
    ct: ct);
```

### Important distinction

A manual status change is not the same thing as the full order completion flow.

Use `CompleteOrderAsync(...)` when you want the actual completion pipeline to run.

Use `UpdateStatusAsync(...)` when you want to change the order status directly.

### Activity log behavior

Status changes create an activity log entry such as:

- `Order status changed. From: Incomplete To: WaitingForPayment`

## 12. Activity logs through the lifecycle

Activity logs are now part of the core order lifecycle.

Current built-in events include:

- order line added
- shipping provider added
- payment provider added
- manual order status changed
- order completed
- order completed with offline payment

### Log types

Ekom currently supports:

- `OrderActivityLogType.Info`
- `OrderActivityLogType.Success`
- `OrderActivityLogType.Alert`

### Important behavior

Activity logs are queued and written in the background using batched persistence.

That means:

- writes are eventually consistent
- logs may not appear instantly after an action
- hot paths such as add-to-cart do not insert directly into SQL for every log write

## 13. Recommended developer usage pattern

If you are building a custom checkout flow, a typical progression looks like this:

```csharp
IOrderInfo order = await _order.AddOrderLineAsync(productId, 1, "Store", ct: ct);

order = await _order.UpdateCustomerInformationAsync(
    new Dictionary<string, string>
    {
        ["storeAlias"] = "Store",
        ["customerEmail"] = "customer@example.com"
    },
    ct: ct);

order = await _order.UpdateShippingInformationAsync(
    shippingProviderId,
    "Store",
    new Dictionary<string, string>(),
    ct: ct);

order = await _order.UpdatePaymentInformationAsync(
    paymentProviderId,
    "Store",
    new Dictionary<string, string>(),
    ct: ct);

await _order.CompleteOrderAsync(order.UniqueId, ct);
```

This is only an example sequence. Real-world checkouts may have more validation, payment redirects, and tracking or consent steps.

## Common pitfalls

### Assuming completion is just a status change

It is not. Completion performs business logic beyond changing status.

### Assuming provider assignment completes checkout

Assigning payment or shipping providers only updates the order. It does not complete the order.

### Assuming logs appear immediately

Activity logs are batched in the background.

### Mixing current-order and order-id reads carelessly

The current-order methods are best for active basket/checkout flows. Order-id reads can return final orders too.

## Related pages

- [Quick Start](quick-start.md)
- [API.Order Reference](api-order-reference.md)
- [Checkout Flow Overview](checkout-flow-overview.md)
- [Activity Logs](activity-logs.md)
- [Order Endpoints](order-endpoints.md)

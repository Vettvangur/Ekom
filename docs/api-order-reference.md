# API.Order Reference

`Ekom.API.Order` is the main C# entry point for working with carts, orders, checkout state, providers, discounts, completion, and order activity logs.

This page focuses on practical server-side usage for developers building on top of Ekom.

## When to use `API.Order`

Use `API.Order` when you are working inside the Umbraco/.NET application and want to:

- read the current cart or a specific order
- add, remove, or update order lines
- update customer information
- update shipping or payment providers
- apply coupons
- complete an order
- update order status
- add custom activity log entries

Use HTTP endpoints instead when you are building a headless frontend or calling Ekom from an external client.

## Injecting `Order`

The usual way to work with `API.Order` is to inject it.

```csharp
using Ekom.API;

public sealed class CheckoutApplicationService
{
    private readonly Order _order;

    public CheckoutApplicationService(Order order)
    {
        _order = order;
    }
}
```

You can also access the static instance:

```csharp
var orderApi = Order.Instance;
```

In most application code, constructor injection is the better option.

## Reading orders

### Get the current order for the current store context

```csharp
IOrderInfo? order = await _order.GetOrderAsync(ct);
```

This is the normal way to get the current basket/cart for the active store.

### Get the current order for a specific store

```csharp
IOrderInfo? order = await _order.GetOrderAsync("Store", ct);
```

### Get a specific order by id

```csharp
IOrderInfo? order = await _order.GetOrderAsync(orderId, ct);
```

This lookup can return completed/final orders as well. Do not use it as a replacement for the current cart lookup in checkout UI code.

### Get the completed order for a store

```csharp
IOrderInfo? completedOrder = await _order.GetCompletedOrderAsync("Store", ct);
```

### Get orders by status

```csharp
IEnumerable<IOrderInfo> orders = await _order.GetStatusOrdersAsync(
    ct,
    OrderStatus.ReadyForDispatch,
    OrderStatus.Closed);
```

There are also overloads for:

- current logged in customer id
- explicit customer id
- customer username

## Adding order lines

Use `AddOrderLineAsync(...)` to add a product to the order.

```csharp
IOrderInfo order = await _order.AddOrderLineAsync(
    productId,
    1,
    "Store",
    ct: ct);
```

### Parameters

- `productId`: product key
- `quantity`: decimal quantity to add
- `storeAlias`: target store alias
- `settings`: optional `AddOrderSettings`

### Important behavior

- if the line already exists, Ekom may update the quantity instead of creating a new line
- if a brand new line is created, Ekom writes an `Info` activity log entry
- stock and variant validation still apply

### Common exceptions

- `ArgumentException`
- `OrderLineNegativeException`
- `ProductNotFoundException`
- `VariantNotFoundException`
- `NotEnoughStockException`

## Updating customer information

Use `UpdateCustomerInformationAsync(...)` to update customer-related order data.

```csharp
IOrderInfo order = await _order.UpdateCustomerInformationAsync(
    new Dictionary<string, string>
    {
        ["email"] = "customer@example.com",
        ["name"] = "Jane Doe",
        ["address"] = "Example Street 1"
    },
    ct: ct);
```

### Important behavior

- the exact keys depend on your checkout form/data flow
- when customer email is added for the first time, Ekom treats that as checkout started
- that checkout-started behavior can trigger activity logging and event flows

## Updating tracking information

Use `UpdateTrackingAsync(...)` to attach tracking data to an order.

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

This is useful when you are integrating analytics or consent-aware order tracking flows.

## Updating shipping provider

Use `UpdateShippingInformationAsync(...)` to assign a shipping provider to the current order.

```csharp
IOrderInfo order = await _order.UpdateShippingInformationAsync(
    shippingProviderId,
    "Store",
    new Dictionary<string, string>(),
    ct: ct);
```

### Important behavior

- the shipping provider must exist and be valid for the current order/store
- Ekom updates the order and provider-related data together
- when the provider actually changes, Ekom writes an `Info` activity log entry

## Updating payment provider

Use `UpdatePaymentInformationAsync(...)` to assign a payment provider.

```csharp
IOrderInfo order = await _order.UpdatePaymentInformationAsync(
    paymentProviderId,
    "Store",
    new Dictionary<string, string>(),
    ct: ct);
```

### Important behavior

- the payment provider must exist and be valid
- when the provider actually changes, Ekom writes an `Info` activity log entry
- offline payment and online payment flows can behave differently later in checkout

## Removing order lines

There are two common remove flows.

### Remove by product key

```csharp
IOrderInfo order = await _order.RemoveOrderLineProductAsync(
    productId,
    "Store",
    ct: ct);
```

### Remove by line id

```csharp
IOrderInfo order = await _order.RemoveOrderLineAsync(
    lineId,
    "Store",
    ct: ct);
```

Use the line-id overload when you already know the exact line you want to remove.

## Updating order line quantity

Use `UpdateOrderlineQuantityAsync(...)` to set a specific quantity.

```csharp
IOrderInfo order = await _order.UpdateOrderlineQuantityAsync(
    lineId,
    3,
    "Store",
    ct: ct);
```

### Important behavior

- this sets the quantity to the provided value
- it does not count as a new order-line-added event
- stock checks still apply

## Completing an order

Use `CompleteOrderAsync(...)` to finalize an order.

```csharp
await _order.CompleteOrderAsync(orderId, ct);
```

### Important behavior

Completing an order may:

- update stock
- finalize coupon/discount behavior
- update the order status
- write a success activity log

Current built-in completion logs include:

- `Order Completed.`
- `Order Completed. Offline payment.`

## Updating order status

There are two common status update patterns.

### Update status for the current store order

```csharp
await _order.UpdateStatusAsync(
    "Store",
    OrderStatus.ReadyForDispatch,
    ct: ct);
```

### Update status for a specific order id

```csharp
await _order.UpdateStatusAsync(
    OrderStatus.ReadyForDispatch,
    orderId,
    "admin@example.com",
    ct: ct);
```

### Important behavior

- status changes create activity log entries
- a manual status change is not the same thing as payment completion
- use completion methods for real checkout/payment completion flows

## Activity logs

Use `AddActivityLogAsync(...)` to add a custom activity log entry to an existing order.

```csharp
await _order.AddActivityLogAsync(
    orderId,
    "ERP sync completed.",
    "BusinessCentral",
    OrderActivityLogType.Success,
    ct);
```

### Parameters

- `orderId`: target order id
- `message`: required log message
- `userName`: optional actor/source name
- `logType`: `Info`, `Success`, or `Alert`

### Important behavior

- the order must exist
- blank messages are rejected
- log writes are queued and batched in the background
- because of batching, log entries are eventually consistent and may not appear instantly

## Coupons and discounts

Discount and coupon operations live on the same API surface.

### Apply coupon to the current order

```csharp
bool applied = await _order.ApplyCouponToOrderAsync("spring10", ct);
```

### Apply coupon to the current order for a specific store

```csharp
bool applied = await _order.ApplyCouponToOrderAsync("spring10", "Store", ct);
```

### Remove coupon from the current order

```csharp
await _order.RemoveCouponFromOrderAsync(ct);
```

### Apply coupon to an order line

```csharp
bool applied = await _order.ApplyCouponToOrderLineAsync(productId, "spring10", "Store");
```

### Remove coupon from an order line

```csharp
await _order.RemoveCouponFromOrderLineAsync(productId, "Store", ct);
```

### Coupon administration helpers

There are also helper methods for:

- `SetCouponCodeAsync(...)`
- `InsertCouponCodeAsync(...)`
- `RemoveCouponCodeAsync(...)`
- `GetCouponsForDiscountAsync(...)`

## Payment submission

Use `PayAsync(...)` to submit the order to the payment flow.

### Pay by order id

```csharp
CheckoutResponse response = await _order.PayAsync(
    paymentRequest,
    "Store",
    orderId,
    ct);
```

### Pay by passing an order instance

```csharp
CheckoutResponse response = await _order.PayAsync(
    paymentRequest,
    "Store",
    order,
    ct);
```

This is the handoff point into the payment-processing flow.

## Utility methods

`API.Order` also contains a few utility helpers.

### Re-initialize order lines

```csharp
IOrderInfo order = await _order.ReInitializeOrder("Store", ct: ct);
```

### Update currency

```csharp
IOrderInfo? order = await _order.UpdateCurrencyAsync("USD", orderId, "Store", ct);
```

### Delete order cookie

```csharp
_order.DeleteOrderCookie("Store");
```

### Hangfire job references on orders

There are helper methods for adding and removing Hangfire job ids linked to an order:

- `AddHangfireJobsToOrderAsync(...)`
- `RemoveHangfireJobsFromOrderAsync(...)`

## Common pitfalls

### Using order-id lookup for cart UI

`GetOrderAsync(Guid)` can return completed/final orders. Use the current-order methods for cart/checkout display.

### Assuming `AddOrderLineAsync(...)` always creates a new line

If the product already exists on the order, quantity may be updated instead.

### Assuming activity logs are written immediately

Activity log writes are queued and batched in the background.

### Confusing status change with completion

Changing an order status does not mean the full checkout completion flow ran.

### Using invalid store aliases

Many methods depend on a valid store alias when store context cannot be resolved automatically.

## Related pages

- [Quick Start](quick-start.md)
- [Order Lifecycle](order-lifecycle.md)
- [Checkout Flow Overview](checkout-flow-overview.md)
- [Activity Logs](activity-logs.md)
- [Order Endpoints](order-endpoints.md)

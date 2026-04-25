# Order API

`Ekom.API.Order` is the main C# entry point for working with carts, orders, checkout state, providers, discounts, payment submission, order status, and order activity logs.

## Example

```csharp
using Ekom.API;
using Ekom.Models;

public sealed class CartApplicationService
{
    private readonly Order _order;

    public CartApplicationService(Order order)
    {
        _order = order;
    }

    public async Task<IOrderInfo> AddToCartAsync(Guid productKey, CancellationToken ct)
    {
        return await _order.AddOrderLineAsync(
            productKey,
            quantity: 1,
            storeAlias: "Store",
            ct: ct);
    }
}
```

## When to use `API.Order`

Use `API.Order` when you want to:

- read the current cart or a specific order
- add, remove, or update order lines
- update customer, shipping, payment, tracking, or currency information
- apply or remove coupons
- complete an order
- submit an order to payment
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

### `GetOrderAsync(CancellationToken ct = default)`

```csharp
IOrderInfo? order = await _order.GetOrderAsync(ct);
```

Returns the current order for the active request store.

This is the normal method for reading the current cart or basket.

### `GetOrderAsync(string? storeAlias, CancellationToken ct = default)`

```csharp
IOrderInfo? order = await _order.GetOrderAsync("Store", ct);
```

Returns the current order for a specific store.

### `GetOrderAsync(Guid uniqueId, CancellationToken ct = default)`

```csharp
IOrderInfo? order = await _order.GetOrderAsync(orderId, ct);
```

Returns an order by id regardless of status.

This may return completed or final orders. Do not use this as the normal cart lookup for checkout UI.

### `GetCompletedOrderAsync(string storeAlias, CancellationToken ct = default)`

```csharp
IOrderInfo? order = await _order.GetCompletedOrderAsync("Store", ct);
```

Returns the completed order for a store using cookie/session data.

### `GetStatusOrdersAsync(CancellationToken ct = default, params OrderStatus[] orderStatuses)`

```csharp
IEnumerable<IOrderInfo> orders = await _order.GetStatusOrdersAsync(
    ct,
    OrderStatus.ReadyForDispatch,
    OrderStatus.Closed);
```

Returns orders matching one or more statuses.

### `GetStatusOrdersByCustomerIdAsync(int customerId, CancellationToken ct = default, params OrderStatus[] orderStatuses)`

```csharp
IEnumerable<IOrderInfo> orders = await _order.GetStatusOrdersByCustomerIdAsync(
    customerId,
    ct,
    OrderStatus.Closed);
```

Returns orders matching one or more statuses for a specific customer id.

### `GetStatusOrdersByCustomerUsernameAsync(string customerUsername, CancellationToken ct = default, params OrderStatus[] orderStatuses)`

```csharp
IEnumerable<IOrderInfo> orders = await _order.GetStatusOrdersByCustomerUsernameAsync(
    "customer@example.com",
    ct,
    OrderStatus.Closed);
```

Returns orders matching one or more statuses for a customer username.

### `GetCompleteCustomerOrdersAsync(int customerId, CancellationToken ct = default, string? storeAlias = null)`

```csharp
IEnumerable<IOrderInfo> orders = await _order.GetCompleteCustomerOrdersAsync(customerId, ct, "Store");
```

Returns completed customer orders for a customer id.

### `GetCompleteCustomerOrdersAsync(string userName, CancellationToken ct = default, string? storeAlias = null)`

```csharp
IEnumerable<IOrderInfo> orders = await _order.GetCompleteCustomerOrdersAsync("customer@example.com", ct, "Store");
```

Returns completed customer orders for a username.

## Updating order state

### `UpdateStatusAsync(string storeAlias, OrderStatus newStatus, ChangeOrderSettings? settings = null, CancellationToken ct = default)`

```csharp
await _order.UpdateStatusAsync("Store", OrderStatus.ReadyForDispatch, ct: ct);
```

Updates the status for the current order in the provided store.

### `UpdateStatusAsync(OrderStatus newStatus, Guid orderId, string? userName = null, ChangeOrderSettings? settings = null, CancellationToken ct = default)`

```csharp
await _order.UpdateStatusAsync(OrderStatus.ReadyForDispatch, orderId, "admin@example.com", ct: ct);
```

Updates the status for a specific order id.

### `CompleteOrderAsync(Guid orderId, CancellationToken ct = default)`

```csharp
await _order.CompleteOrderAsync(orderId, ct);
```

Completes an order through the checkout completion service.

### `ClearCustomerOrderReferenceAsync(Guid orderId, OrderData? order = null, CancellationToken ct = default)`

```csharp
await _order.ClearCustomerOrderReferenceAsync(orderId, ct: ct);
```

Clears the customer order reference for an order.

### `ReInitializeOrder(string storeAlias, OrderSettings? settings = null, CancellationToken ct = default)`

```csharp
IOrderInfo order = await _order.ReInitializeOrder("Store", ct: ct);
```

Reinitializes order lines for a store.

This method returns a `Task` but is named `ReInitializeOrder` in the API.

## Updating order data

### `UpdateCustomerInformationAsync(Dictionary<string, string> form, OrderSettings? settings = null, CancellationToken ct = default)`

```csharp
IOrderInfo order = await _order.UpdateCustomerInformationAsync(
    new Dictionary<string, string>
    {
        ["email"] = "customer@example.com",
        ["name"] = "Jane Doe"
    },
    ct: ct);
```

Updates customer-related order data.

### `UpdateTrackingAsync(string storeAlias, OrderTracking tracking, OrderSettings? settings = null, CancellationToken ct = default)`

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

Updates tracking data on the current order for a store.

### `UpdateShippingInformationAsync(Guid shippingProvider, string storeAlias, Dictionary<string, string> allData, OrderSettings? settings = null, CancellationToken ct = default)`

```csharp
IOrderInfo order = await _order.UpdateShippingInformationAsync(
    shippingProviderId,
    "Store",
    new Dictionary<string, string>(),
    ct: ct);
```

Assigns shipping information and a shipping provider to the order.

### `UpdatePaymentInformationAsync(Guid paymentProvider, string storeAlias, Dictionary<string, string> allData, OrderSettings? settings = null, CancellationToken ct = default)`

```csharp
IOrderInfo order = await _order.UpdatePaymentInformationAsync(
    paymentProviderId,
    "Store",
    new Dictionary<string, string>(),
    ct: ct);
```

Assigns payment information and a payment provider to the order.

### `UpdateCurrencyAsync(string currency, Guid orderId, string storeAlias, CancellationToken ct = default)`

```csharp
IOrderInfo? order = await _order.UpdateCurrencyAsync("USD", orderId, "Store", ct);
```

Updates the order currency.

## Order lines

### `AddOrderLineAsync(Guid productId, decimal quantity, string storeAlias, AddOrderSettings? settings = null, CancellationToken ct = default)`

```csharp
IOrderInfo order = await _order.AddOrderLineAsync(productKey, 1, "Store", ct: ct);
```

Adds a product to the order.

If the line already exists, Ekom may update the existing line instead of creating a new one.

### `RemoveOrderLineProductAsync(Guid productKey, string storeAlias, RemoveOrderSettings? settings = null, CancellationToken ct = default)`

```csharp
IOrderInfo order = await _order.RemoveOrderLineProductAsync(productKey, "Store", ct: ct);
```

Removes an order line by product key.

### `RemoveOrderLineAsync(Guid lineId, string storeAlias, OrderSettings? settings = null, CancellationToken ct = default)`

```csharp
IOrderInfo order = await _order.RemoveOrderLineAsync(lineId, "Store", ct: ct);
```

Removes an order line by line id.

### `UpdateOrderlineQuantityAsync(Guid lineId, decimal quantity, string storeAlias, OrderSettings? settings = null, CancellationToken ct = default)`

```csharp
IOrderInfo order = await _order.UpdateOrderlineQuantityAsync(lineId, 3, "Store", ct: ct);
```

Sets the order line quantity to a specific amount.

## Coupons and discounts

### `ApplyCouponToOrderAsync(string coupon, CancellationToken ct = default)`

```csharp
bool applied = await _order.ApplyCouponToOrderAsync("spring10", ct);
```

Applies a coupon to the current order for the current request store.

### `ApplyCouponToOrderAsync(string coupon, string storeAlias, CancellationToken ct = default)`

```csharp
bool applied = await _order.ApplyCouponToOrderAsync("spring10", "Store", ct);
```

Applies a coupon to the current order for a specific store.

### `RemoveCouponFromOrderAsync(CancellationToken ct = default)`

```csharp
await _order.RemoveCouponFromOrderAsync(ct);
```

Removes a coupon from the current order for the current request store.

### `RemoveCouponFromOrderAsync(string? storeAlias, CancellationToken ct = default)`

```csharp
await _order.RemoveCouponFromOrderAsync("Store", ct);
```

Removes a coupon from the current order for a specific store.

### `ApplyCouponToOrderLineAsync(Guid productKey, string coupon)`

```csharp
bool applied = await _order.ApplyCouponToOrderLineAsync(productKey, "spring10");
```

Applies a coupon to an order line using the current request store.

### `ApplyCouponToOrderLineAsync(Guid productKey, string coupon, string storeAlias)`

```csharp
bool applied = await _order.ApplyCouponToOrderLineAsync(productKey, "spring10", "Store");
```

Applies a coupon to an order line in a specific store.

### `RemoveCouponFromOrderLineAsync(Guid productKey, CancellationToken ct = default)`

```csharp
await _order.RemoveCouponFromOrderLineAsync(productKey, ct);
```

Removes a coupon from an order line using the current request store.

### `RemoveCouponFromOrderLineAsync(Guid productKey, string? storeAlias, CancellationToken ct = default)`

```csharp
await _order.RemoveCouponFromOrderLineAsync(productKey, "Store", ct);
```

Removes a coupon from an order line in a specific store.

### `SetCouponCodeAsync(string coupon, DiscountOrderSettings? discountOrderSettings = null, CancellationToken ct = default)`

```csharp
await _order.SetCouponCodeAsync("spring10", ct: ct);
```

Sets a coupon code on the current order context.

### `InsertCouponCodeAsync(string couponCode, int numberAvailable, Guid discountId, CancellationToken ct = default)`

```csharp
await _order.InsertCouponCodeAsync("spring10", 100, discountId, ct);
```

Creates a coupon code for a discount.

### `RemoveCouponCodeAsync(string couponCode, Guid discountId)`

```csharp
await _order.RemoveCouponCodeAsync("spring10", discountId);
```

Removes a coupon code from a discount.

### `GetCouponsForDiscountAsync(Guid discountId, string query, int page, int pageSize, CancellationToken ct = default)`

```csharp
var result = await _order.GetCouponsForDiscountAsync(discountId, "spring", 1, 20, ct);
```

Returns coupon data for a discount.

## Activity logs

### `AddActivityLogAsync(Guid orderId, string message, string? userName = null, OrderActivityLogType logType = OrderActivityLogType.Info, CancellationToken ct = default)`

```csharp
await _order.AddActivityLogAsync(
    orderId,
    "ERP sync completed.",
    "BusinessCentral",
    OrderActivityLogType.Success,
    ct);
```

Adds a custom activity log entry to an order.

## Payment submission

### `PayAsync(PaymentRequest paymentRequest, string storeAlias, Guid orderId, CancellationToken ct = default)`

```csharp
CheckoutResponse response = await _order.PayAsync(paymentRequest, "Store", orderId, ct);
```

Submits an order to the payment flow by order id.

### `PayAsync(PaymentRequest paymentRequest, string storeAlias, IOrderInfo order, CancellationToken ct = default)`

```csharp
CheckoutResponse response = await _order.PayAsync(paymentRequest, "Store", order, ct);
```

Submits an order to the payment flow using an order instance.

## Hangfire job references

### `AddHangfireJobsToOrderAsync(IEnumerable<string> hangfireJobs, IOrderInfo orderInfo, string? storeAlias = null, CancellationToken ct = default)`

```csharp
await _order.AddHangfireJobsToOrderAsync(jobIds, order, "Store", ct);
```

Adds Hangfire job ids to an order.

### `AddHangfireJobsToOrderAsync(string storeAlias, IEnumerable<string> hangfireJobs, IOrderInfo orderInfo, CancellationToken ct = default)`

```csharp
await _order.AddHangfireJobsToOrderAsync("Store", jobIds, order, ct);
```

Adds Hangfire job ids to an order for a specific store.

### `RemoveHangfireJobsFromOrderAsync(string storeAlias, CancellationToken ct = default)`

```csharp
await _order.RemoveHangfireJobsFromOrderAsync("Store", ct);
```

Removes Hangfire job ids from the current order for a store.

## Cookie and status helpers

### `DeleteOrderCookie(string? storeAlias = null)`

```csharp
_order.DeleteOrderCookie("Store");
```

Deletes the order cookie for the resolved store.

This method is sync-only.

### `EnsureOrderCookie(Guid orderId, string? storeAlias = null)`

```csharp
_order.EnsureOrderCookie(orderId, "Store");
```

Ensures the order cookie points to the provided order id.

This method is sync-only.

### `IsOrderFinal(OrderStatus? orderStatus)`

```csharp
bool final = Order.IsOrderFinal(order.Status);
```

Returns whether an order status is considered final by Ekom.

This method is static and sync-only.

## Notes

- This page documents async methods where async methods exist.
- Sync-only methods are included where there is no async alternative.
- Many methods require a valid store alias when store context cannot be resolved automatically.
- `GetOrderAsync(Guid)` can return completed or final orders. Use current-order methods for cart and checkout UI.
- Activity log writes may be queued or batched, so they can be eventually consistent.
- Updating order status is not the same as running checkout completion.

## Related pages

- [Order Lifecycle](order-lifecycle.md)
- [Checkout Flow](checkout-flow.md)
- [Activity Logs](activity-logs.md)
- [Order Endpoints](order-endpoints.md)
- [Store API](store-api.md)

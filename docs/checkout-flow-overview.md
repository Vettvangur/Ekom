# Checkout Flow Overview

This page explains the checkout flow in Ekom from the point where an order has products and customer data to the point where payment is submitted and the order is completed.

It is written for developers building or customizing checkout logic.

## Checkout flow at a glance

The normal checkout flow in Ekom usually looks like this:

1. load the current order
2. update customer information
3. assign shipping provider
4. assign payment provider
5. submit payment
6. handle payment return or offline completion
7. complete the order

Along the way, Ekom may also:

- update tracking and consent
- update status
- write activity logs
- raise checkout and order events

## 1. Load the current order

Most flows start by loading the current active order.

```csharp
IOrderInfo? order = await _order.GetOrderAsync(ct);
```

If you are working in a multi-store setup and need to be explicit, use the store alias overload.

```csharp
IOrderInfo? order = await _order.GetOrderAsync("Store", ct);
```

## 2. Update customer information

Before payment can be processed, checkout usually needs customer information such as email, name, and address.

```csharp
IOrderInfo order = await _order.UpdateCustomerInformationAsync(
    new Dictionary<string, string>
    {
        ["storeAlias"] = "Store",
        ["customerEmail"] = "customer@example.com",
        ["customerName"] = "Jane Doe",
        ["shippingAddress"] = "Example Street 1"
    },
    ct: ct);
```

### Important behavior

- customer data is written onto the order
- shipping and payment provider ids can also be present in the same form payload
- adding customer email for the first time is treated as checkout started

## 3. Assign shipping provider

Shipping is assigned before final payment submission.

```csharp
IOrderInfo order = await _order.UpdateShippingInformationAsync(
    shippingProviderId,
    "Store",
    new Dictionary<string, string>(),
    ct: ct);
```

### What this does

- resolves the shipping provider for the current store
- validates that the provider can be used
- attaches an `OrderedShippingProvider` to the order
- persists the updated order

### Activity log behavior

If the shipping provider actually changed, Ekom writes:

- `Shipping provider added. Provider: {ProviderTitle}`

## 4. Assign payment provider

Payment provider selection is similar.

```csharp
IOrderInfo order = await _order.UpdatePaymentInformationAsync(
    paymentProviderId,
    "Store",
    new Dictionary<string, string>(),
    ct: ct);
```

### What this does

- resolves the payment provider
- attaches an `OrderedPaymentProvider` to the order
- persists the updated order

### Activity log behavior

If the payment provider actually changed, Ekom writes:

- `Payment provider added. Provider: {ProviderTitle}`

## 5. Submit the order to payment

Once the order has customer information and providers assigned, payment can be submitted.

The programmatic C# surface is `API.Order.PayAsync(...)`.

```csharp
CheckoutResponse response = await _order.PayAsync(
    paymentRequest,
    "Store",
    orderId,
    ct);
```

There is also a public checkout HTTP endpoint:

- `POST /ekom/checkout/pay`

That endpoint accepts either:

- form data
- JSON body

The controller maps the request into a `PaymentRequest` and hands it to `CheckoutControllerService`.

## 6. Payment provider handling inside checkout

Inside `CheckoutControllerService.ProcessPaymentAsync(...)`, Ekom:

- resolves the selected payment provider
- checks whether the provider is offline or online
- builds success and error URLs
- updates order status before or during payment handoff

## 7. Online payment path

For online payment providers, Ekom typically:

1. updates order status to `WaitingForPayment`
2. builds provider-specific payment settings
3. forwards the order to the payment provider integration
4. waits for the user/provider return flow

### Important behavior

Setting the payment provider does not complete the order.

Submitting payment does not necessarily complete the order immediately either. Completion depends on the provider flow and return path.

## 8. Offline payment path

For offline payment providers, the flow is different.

Ekom currently:

1. updates the order status to `OfflinePayment`
2. raises pay events
3. calls `CheckoutService.CompleteAsync(...)`
4. redirects to the configured success URL

### Important behavior

Offline payment still uses the order completion pipeline, but the status update rules differ from the normal paid flow.

## 9. Payment return endpoint

Ekom exposes a payment return endpoint:

- `GET|POST /ekom/checkout/payment-return`

This endpoint:

- reads query/form callback data
- requires `orderId`
- loads the order
- resolves the payment provider
- restores the order cookie
- redirects to the correct success/cancel/error URL

This is part of the standard payment return handling flow.

## 10. Completing the order

The order is finalized through `CheckoutService.CompleteAsync(...)`.

From the public API, this is normally accessed through:

```csharp
await _order.CompleteOrderAsync(orderId, ct);
```

### What completion does

During completion, Ekom may:

- validate stock
- decrement stock
- mark coupons as used
- update discount stock usage
- fire checkout completion events
- update order status
- write a success activity log entry

## 11. Status changes during checkout

A typical successful online flow may involve:

- `Incomplete`
- `WaitingForPayment`
- `ReadyForDispatch`

An offline flow may involve:

- `Incomplete`
- `OfflinePayment`
- completion without the normal paid-status update path

### Important distinction

A status change is only one part of checkout.

The full checkout completion flow includes stock, coupon, event, and activity-log side effects as well.

## 12. Activity logs created during checkout

Checkout-related activity logs now include:

- shipping provider added
- payment provider added
- order completed
- order completed with offline payment
- status changes that happen during checkout or payment preparation

### Log types

- provider assignment logs are `Info`
- order completion logs are `Success`

### Important behavior

Activity logs are written through a background batched dispatcher.

That means they are eventually consistent and may appear slightly after the action that created them.

## 13. Typical programmatic checkout flow

This is a simplified example of a normal application-side checkout flow.

```csharp
IOrderInfo order = await _order.UpdateCustomerInformationAsync(
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

CheckoutResponse response = await _order.PayAsync(
    paymentRequest,
    "Store",
    order.UniqueId,
    ct);
```

In a real checkout, the payment provider may then redirect the user, return to Ekom, and continue through the completion path.

## 14. Common pitfalls

### Assuming provider selection completes the order

It does not. It only updates the order state.

### Assuming `PayAsync(...)` always completes the order immediately

Online payment providers often continue through redirects and callback/return flows.

### Ignoring offline payment differences

Offline payment providers have a different status and completion path.

### Treating status changes as the whole checkout flow

Completion involves more than a status update.

### Expecting activity logs instantly

Activity logs are queued and persisted in the background.

## Related pages

- [Quick Start](quick-start.md)
- [API.Order Reference](api-order-reference.md)
- [Order Lifecycle](order-lifecycle.md)
- [Activity Logs](activity-logs.md)
- [Order Endpoints](order-endpoints.md)

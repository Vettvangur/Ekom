# Complete Checkout

This page focuses on the final step of checkout: submitting the order to payment and completing the order.

In Ekom, this is the point where the checkout flow moves from a prepared order into payment processing and final completion behavior.

## What “complete checkout” means

There are two related but different actions in this stage:

1. submit the order to payment
2. complete the order

Submitting payment starts the provider-specific payment flow.

Completing the order runs the Ekom completion pipeline, which may:

- validate stock
- decrement stock
- update status
- finalize coupons and discount stock
- write activity logs
- raise checkout completion events

## Razor example

In a Razor or server-rendered setup, the normal pattern is to submit payment through `API.Order.PayAsync(...)`.

### Example

```csharp
using Ekom.API;
using Ekom.Models;

public sealed class CheckoutService
{
    private readonly Order _order;

    public CheckoutService(Order order)
    {
        _order = order;
    }

    public async Task<CheckoutResponse> PayAsync(Guid orderId, Guid paymentProviderId, Guid shippingProviderId, CancellationToken ct)
    {
        var paymentRequest = new PaymentRequest
        {
            PaymentProvider = paymentProviderId.ToString(),
            ShippingProvider = shippingProviderId.ToString(),
            StoreAlias = "Store",
            Culture = "en-US",
            ReturnUrl = "/checkout/success"
        };

        return await _order.PayAsync(paymentRequest, "Store", orderId, ct);
    }
}
```

### When to use this

Use this when:

- your checkout is server-rendered
- you are working inside the Umbraco/.NET application
- payment submission should happen from your application code

### Important behavior

- the order should already have customer information
- the order should normally already have shipping and payment providers assigned
- `PayAsync(...)` starts the payment flow, but does not guarantee that the order is already fully completed at that exact moment

## Headless example

In a headless setup, the normal pattern is to submit payment through the public checkout endpoint.

### Example

```http
POST /ekom/checkout/pay?culture=is-IS
Content-Type: application/json

{
  "PaymentProvider": "00000000-0000-0000-0000-000000000020",
  "ShippingProvider": "00000000-0000-0000-0000-000000000010",
  "StoreAlias": "Store",
  "Culture": "is-IS",
  "ReturnUrl": "/success-page"
}
```

### When to use this

Use this when:

- your frontend is headless
- the client application is talking to Ekom over HTTP
- order preparation has already happened through the order endpoints

### Typical headless sequence before pay

1. get the current order
2. add or update order lines
3. update customer information
4. update shipping provider
5. update payment provider
6. call `POST /ekom/checkout/pay`

## Completing the order programmatically

If you need to run the completion pipeline directly in application code, use:

```csharp
await _order.CompleteOrderAsync(orderId, ct);
```

This is useful when:

- an offline payment flow should complete immediately
- a custom flow decides completion explicitly in server-side code

## Common pitfalls

### Assuming payment provider selection is enough

Selecting the payment provider does not submit payment.

### Assuming pay always means immediate final completion

External payment providers may redirect, callback, or require a return flow before the order reaches its final completed state.

### Completing too early

Do not call completion before the order is actually ready. Customer information, shipping, payment, and stock-sensitive checkout steps should already be in place.

## Related pages

- [Checkout Overview](checkout-overview.md)
- [Checkout Flow](checkout-flow.md)
- [Checkout Endpoints](checkout-endpoints.md)
- [Order API](api-order-reference.md)
- [Payment Provider Selection](payment-provider-selection.md)
- [Shipping Provider Selection](shipping-provider-selection.md)

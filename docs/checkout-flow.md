# Checkout Flow

This page explains the practical checkout flow in Ekom from the point where an order has products to the point where payment is submitted and the order is completed.

It focuses on the normal sequence used in both Razor and headless implementations.

## Checkout flow at a glance

The normal checkout flow in Ekom usually looks like this:

1. load the current order
2. update customer information
3. assign shipping provider
4. assign payment provider
5. submit payment
6. handle return or offline completion
7. complete the order

## 1. Load the current order

Most flows start by loading the current active order.

```csharp
IOrderInfo? order = await _order.GetOrderAsync(ct);
```

If you need to be explicit in a multi-store setup:

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
- attaches an ordered shipping provider to the order
- persists the updated order

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
- attaches an ordered payment provider to the order
- persists the updated order

## 5. Submit the order to payment

Once the order has customer information and providers assigned, payment can be submitted.

In C#, the programmatic entry point is:

```csharp
CheckoutResponse response = await _order.PayAsync(
    paymentRequest,
    "Store",
    order.UniqueId,
    ct);
```

For headless flows, the HTTP entry point is:

- `POST /ekom/checkout/pay`

## 6. Online and offline payment paths

After payment submission, the flow can split depending on the provider.

### Online payment providers

For online providers, Ekom typically:

1. updates the order state for payment handoff
2. builds provider-specific payment settings
3. forwards the order to the provider integration
4. waits for the provider return flow

### Offline payment providers

For offline providers, Ekom can complete the checkout flow without an external payment handoff.

This still uses the normal completion pipeline, but the status rules are different.

## 7. Complete the order

The order is finalized through the completion pipeline.

From the public API, this is normally reached through:

```csharp
await _order.CompleteOrderAsync(order.UniqueId, ct);
```

During completion, Ekom may:

- validate stock
- decrement stock
- mark coupons as used
- update discount stock usage
- raise checkout completion events
- update order status
- write success activity log entries

## Common pitfalls

### Setting the payment provider is not the same as paying

Selecting a payment provider only prepares the order for the payment step.

### Paying is not always the same as immediate completion

Depending on the payment provider, payment submission may hand off to an external flow before order completion happens.

### Skipping customer data too early

Most checkout flows need customer information on the order before payment submission is useful.

## Related pages

- [Checkout Overview](checkout-overview.md)
- [Complete Checkout](complete-checkout.md)
- [Payment Provider Selection](payment-provider-selection.md)
- [Shipping Provider Selection](shipping-provider-selection.md)
- [Checkout Endpoints](checkout-endpoints.md)
- [Order Lifecycle](order-lifecycle.md)

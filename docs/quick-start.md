# Quick Start

This page shows a minimal developer flow for working with Ekom:

1. query products
2. add a product to the current order
3. read the order
4. update providers
5. complete the order

This is not a full production checkout. It is a fast way to understand the core API shape.

## Prerequisites

Before using these examples:

- Ekom is installed
- Ekom root content has been created
- your site can resolve products and stores
- you have added the required Ekom imports/injections

## Inject the Ekom APIs

```csharp
using Ekom.API;

public class CheckoutExampleService
{
    private readonly Catalog _catalog;
    private readonly Order _order;

    public CheckoutExampleService(Catalog catalog, Order order)
    {
        _catalog = catalog;
        _order = order;
    }
}
```

## Query products

Use `Catalog` to load products.

```csharp
var product = _catalog.GetProduct(productKey);
```

You can use this to fetch the product before adding it to an order.

## Add a product to the current order

```csharp
await _order.AddToOrderAsync(productKey, 1, ct: ct);
```

This adds one unit of the product to the current order.

### Important behavior

- if the order line already exists, quantity may be updated instead of creating a new line
- if a new order line is created, Ekom writes an activity log entry

## Read the current order

```csharp
var currentOrder = await _order.GetOrderAsync(ct);
```

Use this after cart changes to inspect:

- order lines
- totals
- selected providers
- customer data

## Update shipping provider

```csharp
await _order.UpdateShippingProviderAsync(orderId, shippingProviderId, ct: ct);
```

### Important behavior

- shipping provider must exist and be valid for the order/store
- successful provider changes create an `Info` activity log entry

## Update payment provider

```csharp
await _order.UpdatePaymentProviderAsync(orderId, paymentProviderId, ct: ct);
```

### Important behavior

- payment provider must exist and be valid
- successful provider changes create an `Info` activity log entry

## Complete the order

```csharp
await _order.CompleteOrderAsync(orderId, ct);
```

### Important behavior

Completing an order may:

- update stock
- update discounts/coupons
- change order status
- write success activity logs

## Add a custom activity log

You can also add your own activity log entries:

```csharp
await _order.AddActivityLogAsync(
    orderId,
    "ERP sync completed.",
    "BusinessCentral",
    OrderActivityLogType.Success,
    ct);
```

### Important behavior

- the order must exist
- messages cannot be empty
- log writes are queued and batched in the background, so they may not appear instantly

## Next steps

After the quick start, continue with:

- [API.Order Reference](api-order-reference.md)
- [Order Lifecycle](order-lifecycle.md)
- [Checkout Flow](checkout-flow.md)
- [Activity Logs](activity-logs.md)

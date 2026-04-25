# Add Customer Information to an Order

This page shows the normal ways to add customer information to the current order in Ekom.

In Ekom, customer information is written onto the active order during checkout preparation.

## When to use this approach

Use this when:

- you already have a current order or cart
- you want to attach customer details before payment submission
- you are building checkout flows in Razor or headless setups

## Basic approach

The most common programmatic entry point is:

- `Order.UpdateCustomerInformationAsync(...)`

The most common headless HTTP entry point is:

- `POST /ekom/order/updatecustomer`

## C# example

This is the normal server-side pattern.

```csharp
using Ekom.API;
using Ekom.Models;

public sealed class CheckoutCustomerService
{
    private readonly Order _order;

    public CheckoutCustomerService(Order order)
    {
        _order = order;
    }

    public async Task<IOrderInfo> AddCustomerInformationAsync(CancellationToken ct)
    {
        return await _order.UpdateCustomerInformationAsync(
            new Dictionary<string, string>
            {
                ["storeAlias"] = "Store",
                ["customerEmail"] = "customer@example.com",
                ["customerName"] = "Jane Doe",
                ["customerPhone"] = "+3541234567",
                ["shippingAddress"] = "Example Street 1"
            },
            ct: ct);
    }
}
```

## What this example does

- uses `API.Order`
- updates the current order with customer data
- returns the updated order

## Razor form example

If you are rendering a checkout form in Razor, a common pattern is to post customer fields back to Ekom.

```cshtml
<form method="post" action="/ekom/order/updatecustomer">
    <input type="hidden" name="storeAlias" value="Store" />

    <input type="email" name="customerEmail" placeholder="Email" />
    <input type="text" name="customerName" placeholder="Full name" />
    <input type="text" name="customerPhone" placeholder="Phone" />
    <input type="text" name="shippingAddress" placeholder="Address" />

    <button type="submit">Continue checkout</button>
</form>
```

This is useful when you want the storefront to post directly into the public order endpoint instead of calling `API.Order` in application code.

## Headless example

For headless flows, the normal pattern is to call the public order endpoint directly.

```http
POST /ekom/order/updatecustomer
Content-Type: application/json

{
  "storeAlias": "Store",
  "customerEmail": "customer@example.com",
  "customerName": "Jane Doe",
  "customerPhone": "+3541234567",
  "shippingAddress": "Example Street 1"
}
```

## Important request fields

- `storeAlias`: target store
- `customerEmail`: customer email
- `customerName`: customer name
- `customerPhone`: customer phone

Depending on your checkout flow, you can also include:

- address fields
- shipping or payment-related form fields
- `consent`
- `tracking`

## What happens when customer information is added

When customer information is added to an order, Ekom may:

- update the customer data stored on the order
- keep related checkout form values for later steps
- treat first-time customer email as checkout started
- trigger order and checkout-related event flows

## Common pitfalls

### Assuming there is already an active order

Customer information is added to the current order. Make sure the order already exists before posting checkout data.

### Using field names that do not match your flow

The exact form keys depend on your checkout implementation. Keep your frontend field names aligned with the data you expect Ekom to process later.

### Forgetting store context

In multi-store setups, always make sure the correct `storeAlias` is supplied when the request context is not enough on its own.

## Related pages

- [Add Product to Cart](add-product-to-cart.md)
- [Checkout Flow](checkout-flow.md)
- [Complete Checkout](complete-checkout.md)
- [Order API](api-order-reference.md)
- [Order Endpoints](order-endpoints.md)

# Add Product to Cart

This page shows the normal ways to add a product to the current order in Ekom.

In Ekom, “cart” is the current active order for the current store.

## When to use this approach

Use this when:

- you are rendering a server-side storefront in Razor
- you need to add a product or variant to the current order
- you want to understand the difference between programmatic and HTTP-based cart updates

## Basic approach

The most common programmatic entry point is:

- `Order.AddOrderLineAsync(...)`

The most common headless HTTP entry point is:

- `POST /ekom/order/add`

## C# example

This is the normal server-side pattern.

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

    public async Task<IOrderInfo> AddToCartAsync(Guid productKey, Guid? variantKey, CancellationToken ct)
    {
        var settings = new AddOrderSettings();

        if (variantKey.HasValue)
        {
            settings.Variant = variantKey.Value;
        }

        return await _order.AddOrderLineAsync(
            productKey,
            quantity: 1,
            storeAlias: "Store",
            settings: settings,
            ct: ct);
    }
}
```

## What this example does

- uses `API.Order`
- adds a product to the current order
- optionally includes a variant
- returns the updated order

## Razor form example

If you are rendering a product page in Razor, a common pattern is to post the product and variant information back to Ekom.

```cshtml
@model Ekom.Models.IProduct

<form method="post" action="/ekom/order/add">
    <input type="hidden" name="ProductId" value="@Model.Key" />
    <input type="hidden" name="StoreAlias" value="Store" />
    <input type="number" name="Quantity" value="1" min="1" step="1" />

    @if (Model.PrimaryVariant != null)
    {
        <input type="hidden" name="VariantId" value="@Model.PrimaryVariant.Key" />
    }

    <input type="hidden" name="Action" value="AddOrUpdate" />

    <button type="submit">Add to cart</button>
</form>
```

This is useful when you want the storefront to post directly into the public order endpoint instead of calling `API.Order` in application code.

## Headless example

For headless flows, the normal pattern is to call the public order endpoint directly.

```http
POST /ekom/order/add
Content-Type: application/json

{
  "ProductId": "cb6906db-c156-4c88-814c-6cc181bd1ae3",
  "VariantId": "5abc5a27-cf22-4438-bcb6-f12c2a8564c9",
  "StoreAlias": "Store",
  "Quantity": 1,
  "Action": "AddOrUpdate"
}
```

## Important request fields

- `ProductId`: product key
- `VariantId`: optional variant key
- `StoreAlias`: target store
- `Quantity`: quantity to add
- `Action`: how Ekom should treat the add request

Typical action values include:

- `AddOrUpdate`
- `Set`
- `New`

## What happens when a product is added

When a product is added to cart, Ekom may:

- create a new order line
- increase the quantity on an existing line
- validate product and variant availability
- validate stock
- write activity log entries when needed
- raise order events such as order line events

## Common pitfalls

### Assuming add always creates a new order line

It may update an existing line instead, depending on the request and current order state.

### Forgetting variant selection

If the product depends on a specific variant, you usually need to pass `VariantId` as well.

### Forgetting store context

In multi-store setups, always make sure the correct `StoreAlias` is supplied when the request context is not enough on its own.

### Ignoring quantity behavior

`Quantity` is decimal-based in the API. Make sure your UI and request shape reflect the quantity rules your store expects.

## Related pages

- [Render a Product Page](render-product-page.md)
- [Order API](api-order-reference.md)
- [Order Endpoints](order-endpoints.md)
- [Checkout Overview](checkout-overview.md)

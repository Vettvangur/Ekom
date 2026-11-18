# Quick start

[Ekom docs](../README.md) / [Getting started](README.md) / Quick start

This page takes a site with Ekom installed to a working basket. You create a store and a product in the backoffice, then write a small service that adds the product to the visitor's basket and reads the basket back. It does not cover payment or a full checkout; the last section points you to the guides for those.

Before you start, install Ekom as described in [Installation](installation.md).

On this page:

- [Create a store](#create-a-store)
- [Create a category and a product](#create-a-category-and-a-product)
- [Add the product to a basket from code](#add-the-product-to-a-basket-from-code)
- [Read the basket](#read-the-basket)
- [Go on to checkout](#go-on-to-checkout)

## Create a store

A store is the unit that Ekom keeps catalog content, prices, providers and orders for. A site has one or more stores. Each store has an alias, which is the name of its node. Your code uses the alias to tell Ekom which store it works with.

1. In the **Content** section, open **Ekom** > **Stores**.
2. Create a **Store** node. Its name is the store alias. The examples on this page use `Store`.
3. Set **Store Root Node** to the root content node of the website the store belongs to.
4. Set **Vat**, the VAT rate of the store, and choose the store's **Currency**.
5. Save and publish.

See [Stores](../guides/stores.md) for domains, cultures and more than one store.

## Create a category and a product

1. Open **Ekom** > **Catalog** and create a **Category** inside it. Give it a **Title** and a **Slug**, then save and publish.
2. Inside the category, create a **Product**. Give it a **Title**, a **Slug**, a **SKU** such as `SHIRT-BLUE`, a **Price** for the store's currency, and a **Stock** count above zero.
3. Save and publish.

Ekom refuses to add a product to a basket when there is not enough stock, unless backorder is turned on for the product or `Ekom:DisableStock` is `true`. See [Stock](../guides/stock.md).

## Add the product to a basket from code

Ekom registers its API classes with dependency injection, so you can inject them into your own classes. This service uses two of them: `Catalog`, which reads products, and `Order`, which works with the visitor's basket.

```csharp
using Ekom.API;
using Ekom.Models;

public sealed class BasketService(Catalog catalog, Order order)
{
    private const string StoreAlias = "Store";

    public async Task<IOrderInfo?> AddToBasketAsync(string sku, CancellationToken ct)
    {
        IProduct? product = catalog.GetProduct(sku, StoreAlias);

        if (product is null)
        {
            return null;
        }

        return await order.AddOrderLineAsync(product.Key, 1m, StoreAlias, ct: ct);
    }

    public Task<IOrderInfo?> GetBasketAsync(CancellationToken ct)
        => order.GetOrderAsync(StoreAlias, ct);
}
```

Register the service in `Program.cs`:

```csharp
builder.Services.AddScoped<BasketService>();
```

Call it from code that runs during a storefront request, such as a controller action: `await basketService.AddToBasketAsync("SHIRT-BLUE", ct)`.

What happens:

- `GetProduct` returns `null` when the store has no published product with that SKU.
- If the visitor has no basket yet, `AddOrderLineAsync` creates one and sets a cookie that links it to the visitor. The cookie lasts for `Ekom:BasketCookieLifetime` days.
- If the product is already in the basket, the quantity is added to the existing line.
- `AddOrderLineAsync` returns the updated basket, with its totals worked out.

`AddOrderLineAsync` throws when it cannot add the line. These are the exceptions you are most likely to see first, all in the `Ekom.Exceptions` namespace:

| Exception | When |
| --- | --- |
| `ProductNotFoundException` | No product with that key in the store. |
| `VariantRequiredException` | The product has variants and you did not choose one. Pass the variant key in `AddOrderSettings.VariantKey`. |
| `NotEnoughStockException` | There is not enough stock for the quantity. |

## Read the basket

`GetOrderAsync` returns the visitor's basket, or `null` when there is none.

```csharp
using System.Text;
using Ekom.Models;

IOrderInfo? basket = await basketService.GetBasketAsync(ct);

if (basket is null)
{
    return "Your basket is empty.";
}

var summary = new StringBuilder();

foreach (IOrderLine line in basket.OrderLines)
{
    summary.AppendLine($"{line.Quantity} x {line.Product.Title} ({line.Product.SKU})");
}

summary.AppendLine($"Total: {basket.GrandTotal.CurrencyString}");

return summary.ToString();
```

Reading the basket does not create one. Once an order is completed, `GetOrderAsync` no longer returns it as the basket.

## Go on to checkout

A real checkout also needs a shipping method, a payment method, customer details and a payment provider. Ekom's `Order` class has the methods for each step, for example `UpdateShippingInformationAsync` and `UpdatePaymentInformationAsync`, and `Providers` lists the shipping and payment providers of a store. Read these guides in order:

1. [Orders and baskets](../guides/orders-and-baskets.md): change and remove lines, and the rules for baskets.
2. [Payment and shipping providers](../guides/providers.md): set up the options a customer can choose.
3. [Checkout](../guides/checkout.md): take the basket through payment to a completed order.

Do not call `Order.CompleteOrderAsync` from a storefront endpoint that any visitor can reach. It completes an order without checking or recording a payment. Ekom completes an order itself when the payment provider records a captured or authorized payment; see [Checkout](../guides/checkout.md).

## Related pages

- [Configuration](configuration.md): the settings that change basket and stock behavior.
- [Catalog and products](../guides/catalog-and-products.md): read categories, products and variants.
- [.NET API](../reference/dotnet-api.md): every method on `Catalog`, `Order` and the other API classes.
- [Samples](../samples/overview.md): example sites with a storefront and checkout pages.

Next: [Guides](../guides/README.md)

[Back to Getting started](README.md)

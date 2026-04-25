# Render a Product Page

This page shows a practical way to render a product page in Ekom.

The normal product-page pattern is:

1. resolve the current product
2. render the product details
3. render price, availability, and variants
4. optionally render related products

## When to use this approach

Use this when:

- you have a server-rendered Umbraco site
- the current route already represents an Ekom product page
- you want to render product details in Razor

## Basic approach

For a product page, the most common entry points are:

- `Catalog.GetProductAsync(...)` to read the current product
- `Catalog.GetProductByRouteAsync(...)` if you need to resolve a route manually
- `Catalog.GetRelatedProductsAsync(...)` if you want related products

## Razor example

This example assumes the current request already represents a product.

```cshtml
@using Ekom.API
@using Ekom.Models
@inject Catalog Catalog

@{
    IProduct? product = await Catalog.GetProductAsync();

    if (product == null)
    {
        <p>Product not found.</p>
        return;
    }

    IReadOnlyList<IProduct> relatedProducts = await Catalog.GetRelatedProductsAsync(product.Key, count: 4);
}

<article>
    <header>
        <h1>@product.Title</h1>

        @if (!string.IsNullOrWhiteSpace(product.Summary))
        {
            <p>@product.Summary</p>
        }
    </header>

    <p>SKU: @product.SKU</p>
    <p>Price: @product.Price.AfterDiscount.CurrencyString</p>
    <p>Available: @(product.Available ? "In stock" : "Out of stock")</p>

    @if (!string.IsNullOrWhiteSpace(product.Description))
    {
        <div>@Html.Raw(product.Description)</div>
    }

    @if (product.VariantGroups.Any())
    {
        <section>
            <h2>Variants</h2>

            @foreach (IVariantGroup group in product.VariantGroups)
            {
                <div>
                    <h3>@group.Title</h3>

                    <ul>
                        @foreach (IVariant variant in group.Variants)
                        {
                            <li>@variant.Title</li>
                        }
                    </ul>
                </div>
            }
        </section>
    }

    @if (relatedProducts.Any())
    {
        <section>
            <h2>Related products</h2>

            <ul>
                @foreach (IProduct relatedProduct in relatedProducts)
                {
                    <li>
                        <a href="@relatedProduct.Url">@relatedProduct.Title</a>
                    </li>
                }
            </ul>
        </section>
    }
</article>
```

## What this example does

- gets the current product from the request context
- renders basic product information
- shows price and availability
- renders variant groups when they exist
- loads and renders related products

## Resolve a product by route manually

If you need to resolve the product from a route value yourself, use `GetProductByRouteAsync(...)`.

```csharp
IProduct? product = await _catalog.GetProductByRouteAsync("/products/shoe", ct: ct);
```

This is useful when:

- the current request is not already mapped to an Ekom product
- you are building a custom route flow
- you want to render a product from a route string explicitly

## Resolve a product by id, key, or sku

If your product page is not route-based, common lookup options are:

```csharp
IProduct? bySku = await _catalog.GetProductAsync("SKU-123", ct: ct);
IProduct? byKey = await _catalog.GetProductAsync(productKey, ct: ct);
IProduct? byId = await _catalog.GetProductAsync(1234, ct: ct);
```

Use the lookup that best matches your page or integration flow.

## Render variants

Products can expose:

- `PrimaryVariant`
- `PrimaryVariantGroup`
- `VariantGroups`
- `AllVariants`

If your product has configurable variants, `VariantGroups` is usually the main property to render in the page UI.

If you need a specific variant separately, you can also resolve it through `Catalog`.

```csharp
IVariant? variant = await _catalog.GetVariantAsync(variantKey, ct: ct);
```

## Render related products

For product pages, a common addition is related product rendering.

```csharp
IReadOnlyList<IProduct> relatedProducts = await _catalog.GetRelatedProductsAsync(product.Key, count: 4, ct: ct);
```

You can also load related products by sku:

```csharp
IReadOnlyList<IProduct> relatedProducts = await _catalog.GetRelatedProductsBySkuAsync(product.SKU, count: 4, ct: ct);
```

## Common pitfalls

### Assuming the current request already has a product

`GetProductAsync()` depends on the active request context. If your route is custom or not mapped into Ekom product resolution, use `GetProductByRouteAsync(...)` or another explicit lookup instead.

### Forgetting variant rendering

If a product has configurable variants, rendering only the base product data is often not enough for a usable product page.

### Forgetting availability and pricing context

Product pages usually need more than title and description. Price, stock/availability, selected variants, and related products are often part of the page experience.

## Related pages

- [Catalog API](catalog-api.md)
- [Catalog Endpoints](catalog-endpoints.md)
- [Render a Category Page](render-category-page.md)
- [Add Product to Cart](add-product-to-cart.md)

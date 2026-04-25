# Render a Category Page

This page shows a practical way to render a category page in Ekom.

The normal category-page pattern is:

1. resolve the current category
2. load the products that belong to that category
3. render the category information and product listing

## When to use this approach

Use this when:

- you have a server-rendered Umbraco site
- the current route already represents an Ekom category page
- you want to render category details and products in Razor

## Basic approach

For a category page, the most common entry points are:

- `Catalog.GetCategoryAsync(...)` to read the current category
- `category.ProductsRecursiveAsync(...)` to get the products for the category

If you need to resolve a category by route manually, use:

- `Catalog.GetCategoryByRouteAsync(...)`

## Razor example

This example assumes the current request already represents a category.

```cshtml
@using Ekom.API
@using Ekom.Models
@inject Catalog Catalog

@{
    ICategory? category = await Catalog.GetCategoryAsync();

    if (category == null)
    {
        <p>Category not found.</p>
        return;
    }

    ProductResponse productResponse = await category.ProductsRecursiveAsync(new ProductQuery
    {
        Page = 1,
        PageSize = 24,
        OrderBy = Ekom.Utilities.OrderBy.DateDesc
    });
}

<section>
    <header>
        <h1>@category.Title</h1>

        @if (!string.IsNullOrWhiteSpace(category.Description))
        {
            <p>@category.Description</p>
        }
    </header>

    @if (!productResponse.Products.Any())
    {
        <p>No products found in this category.</p>
    }
    else
    {
        <ul>
            @foreach (IProduct product in productResponse.Products)
            {
                <li>
                    <a href="@product.Url">@product.Title</a>
                </li>
            }
        </ul>
    }
</section>
```

## What this example does

- gets the current category from the request context
- loads products recursively for that category
- applies paging and ordering through `ProductQuery`
- renders a simple category page in Razor

## Resolve a category by route manually

If you need to resolve the category from a route value yourself, use `GetCategoryByRouteAsync(...)`.

```csharp
ICategory? category = await _catalog.GetCategoryByRouteAsync("/shop/shoes", ct: ct);
```

This is useful when:

- the current request is not already mapped to an Ekom category
- you are building a custom route flow
- you want to render a category from a route string explicitly

## Control the product listing

You can shape the category listing with `ProductQuery`.

Common options include:

- `Page`
- `PageSize`
- `OrderBy`
- `MetaFilters`
- `PropertyFilters`
- `StoreAlias`

Example:

```csharp
ProductResponse productResponse = await category.ProductsRecursiveAsync(new ProductQuery
{
    Page = 1,
    PageSize = 24,
    StoreAlias = "Store",
    OrderBy = Ekom.Utilities.OrderBy.DateDesc
}, ct);
```

## Render category navigation

If you also want to render category navigation around the current page, common supporting calls are:

```csharp
IReadOnlyList<ICategory> rootCategories = await _catalog.GetRootCategoriesAsync(ct: ct);
```

and, if needed:

```csharp
IReadOnlyList<ICategory> allCategories = await _catalog.GetAllCategoriesAsync(ct: ct);
```

Use these when you want:

- top-level category navigation
- sidebar category trees
- breadcrumbs or category menus built outside the current category object

## Common pitfalls

### Assuming the current request already has a category

`GetCategoryAsync()` depends on the active request context. If your route is custom or not mapped into Ekom category resolution, use `GetCategoryByRouteAsync(...)` instead.

### Loading products without paging

If a category can contain many products, use `ProductQuery` with paging to avoid rendering too much at once.

### Forgetting store context in multi-store setups

If your flow is not already in the correct request store context, pass `StoreAlias` explicitly where needed.

## Related pages

- [Catalog API](catalog-api.md)
- [Catalog Endpoints](catalog-endpoints.md)
- [Render a Product Page](render-product-page.md)
- [Checkout Overview](checkout-overview.md)

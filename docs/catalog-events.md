# Catalog Events

Catalog events let you modify categories and products before Ekom returns them.

This is useful when you want to adjust catalog data for a specific solution without changing the underlying stored content.

## When to use catalog events

Use catalog events when you want to:

- filter products before they are returned
- hide categories in specific scenarios
- adjust returned product or category data
- customize currency string formatting

These events are best suited for read-time changes.

## Recommended registration pattern

The preferred approach is to register handlers during application startup through an Umbraco component.

```csharp
using Ekom.Events;
using Umbraco.Cms.Core.Composing;

namespace MySite;

public sealed class CatalogEventHandlers : IComponent
{
    public void Initialize()
    {
        CatalogEvents.BeforeReturnProductAsync += OnBeforeReturnProductAsync;
    }

    public void Terminate()
    {
        CatalogEvents.BeforeReturnProductAsync -= OnBeforeReturnProductAsync;
    }

    private ValueTask OnBeforeReturnProductAsync(ProductEventArgs args, CancellationToken ct)
    {
        if (args.Product is null)
        {
            return ValueTask.CompletedTask;
        }

        return ValueTask.CompletedTask;
    }
}

public sealed class CatalogEventHandlersComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Components().Append<CatalogEventHandlers>();
    }
}
```

## Available events

### `BeforeReturnCategoryAsync`

Runs before a single category is returned.

Use this when you want to replace, adjust, or hide a category.

### `BeforeReturnCategoriesAsync`

Runs before a category collection is returned.

Use this when you want to filter or reorder categories.

### `BeforeReturnProductAsync`

Runs before a single product is returned.

Use this when you want to adjust a product before it reaches the caller.

### `BeforeReturnProductsAsync`

Runs before a product collection is returned.

Use this when you want to filter or transform a product list.

### `CurrencyStringFormat`

Lets you customize how currency values are formatted.

Use this when the default currency string formatting does not match your output requirements.

## Example

This example removes products that should not be shown in a given solution.

```csharp
using Ekom.Events;
using Ekom.Models;
using System.Linq;
using Umbraco.Cms.Core.Composing;

namespace MySite;

public sealed class CatalogEventHandlers : IComponent
{
    public void Initialize()
    {
        CatalogEvents.BeforeReturnProductsAsync += OnBeforeReturnProductsAsync;
    }

    public void Terminate()
    {
        CatalogEvents.BeforeReturnProductsAsync -= OnBeforeReturnProductsAsync;
    }

    private ValueTask OnBeforeReturnProductsAsync(ProductsEventArgs args, CancellationToken ct)
    {
        args.Products = args.Products.Where(ShouldBeVisible);
        return ValueTask.CompletedTask;
    }

    private static bool ShouldBeVisible(IProduct product)
    {
        return product.Available;
    }
}
```

## Notes

- Catalog events are async-first.
- Some sync catalog events still exist for backward compatibility.
- If you only need to affect a list result, prefer the collection events instead of changing items one by one.

## Related pages

- [Catalog API](catalog-api.md)
- [Catalog Endpoints](catalog-endpoints.md)
- [Render a Category Page](render-category-page.md)
- [Render a Product Page](render-product-page.md)

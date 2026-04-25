# Catalog API

`Ekom.API.Catalog` is the main C# entry point for reading products, categories, variants, variant groups, metafields, related products, and catalog search results.

## Example

```csharp
using Ekom.API;
using Ekom.Models;

public sealed class CatalogApplicationService
{
    private readonly Catalog _catalog;

    public CatalogApplicationService(Catalog catalog)
    {
        _catalog = catalog;
    }

    public async Task<IProduct?> GetProductAsync(string sku, CancellationToken ct)
    {
        return await _catalog.GetProductAsync(sku, ct: ct);
    }
}
```

## When to use `API.Catalog`

Use `API.Catalog` when you want to:

- get the current routed product or category
- resolve products and categories by route, id, key, or sku
- list products from a store or category
- work with variants and variant groups
- return related products
- perform catalog product search

Use HTTP endpoints instead when you are building a headless frontend or calling Ekom from an external client.

## Injecting `Catalog`

The usual way to work with `API.Catalog` is to inject it.

```csharp
using Ekom.API;

public sealed class CatalogApplicationService
{
    private readonly Catalog _catalog;

    public CatalogApplicationService(Catalog catalog)
    {
        _catalog = catalog;
    }
}
```

You can also access the static instance:

```csharp
var catalogApi = Catalog.Instance;
```

In most application code, constructor injection is the better option.

## Current routed content

### `GetProduct(bool raiseEvent = true)`

```csharp
IProduct? product = _catalog.GetProduct();
```

Returns the current product from the active request `ContentRequest`.

Use this when the current route already represents a product page.

### `GetProductAsync(bool raiseEvent = true, CancellationToken ct = default)`

```csharp
IProduct? product = await _catalog.GetProductAsync(ct: ct);
```

Async version of `GetProduct()`.

### `GetCategory(bool raiseEvent = true)`

```csharp
ICategory? category = _catalog.GetCategory();
```

Returns the current category from the active request `ContentRequest`.

Use this when the current route already represents a category page.

### `GetCategoryAsync(bool raiseEvent = true, CancellationToken ct = default)`

```csharp
ICategory? category = await _catalog.GetCategoryAsync(ct: ct);
```

Async version of `GetCategory()`.

## Single product lookup

### `GetProductByRoute(string route, string? storeAlias = null, bool raiseEvent = true)`

```csharp
IProduct? product = _catalog.GetProductByRoute("/products/shoe");
```

Returns a product by route.

### `GetProductByRouteAsync(string route, string? storeAlias = null, bool raiseEvent = true, CancellationToken ct = default)`

```csharp
IProduct? product = await _catalog.GetProductByRouteAsync("/products/shoe", ct: ct);
```

Async version of `GetProductByRoute(...)`.

### `GetProduct(string sku, string? storeAlias = null, bool? global = null, bool raiseEvent = true)`

```csharp
IProduct? product = _catalog.GetProduct("SKU-123");
```

Returns a product by sku.

If `global` is `true`, or global catalog is enabled in configuration, Ekom can fall back to other stores.

### `GetProductAsync(string sku, string? storeAlias = null, bool? global = null, bool raiseEvent = true, CancellationToken ct = default)`

```csharp
IProduct? product = await _catalog.GetProductAsync("SKU-123", ct: ct);
```

Async version of `GetProduct(string ...)`.

### `GetProduct(Guid key, string? storeAlias = null, bool? global = null, bool raiseEvent = true)`

```csharp
IProduct? product = _catalog.GetProduct(productKey);
```

Returns a product by key.

### `GetProductAsync(Guid key, string? storeAlias = null, bool? global = null, bool raiseEvent = true, CancellationToken ct = default)`

```csharp
IProduct? product = await _catalog.GetProductAsync(productKey, ct: ct);
```

Async version of `GetProduct(Guid ...)`.

### `GetProduct(int id, string? storeAlias = null, bool? global = null, bool raiseEvent = true)`

```csharp
IProduct? product = _catalog.GetProduct(1234);
```

Returns a product by integer id.

### `GetProductAsync(int id, string? storeAlias = null, bool? global = null, bool raiseEvent = true, CancellationToken ct = default)`

```csharp
IProduct? product = await _catalog.GetProductAsync(1234, ct: ct);
```

Async version of `GetProduct(int ...)`.

## Product collections

### `GetAllProducts(ProductQuery? query = null)`

```csharp
ProductResponse products = _catalog.GetAllProducts(new ProductQuery
{
    StoreAlias = "Store",
    Page = 1,
    PageSize = 24
});
```

Returns all products for the current request store or for `query.StoreAlias` when provided.

### `GetAllProducts(string storeAlias, ProductQuery? query = null)`

```csharp
ProductResponse products = _catalog.GetAllProducts("Store");
```

Returns all products for a specific store.

### `GetAllProductsAsync(ProductQuery? query = null, CancellationToken ct = default)`

```csharp
ProductResponse products = await _catalog.GetAllProductsAsync(ct: ct);
```

Async version of `GetAllProducts(ProductQuery? ...)`.

### `GetAllProductsAsync(string storeAlias, ProductQuery? query = null, CancellationToken ct = default)`

```csharp
ProductResponse products = await _catalog.GetAllProductsAsync("Store", ct: ct);
```

Async version of `GetAllProducts(string ...)`.

### `GetProductsRescursiveByRoute(string route, ProductQuery? query = null, CancellationToken ct = default)`

```csharp
ProductResponse products = _catalog.GetProductsRescursiveByRoute("/shop/shoes");
```

Returns products from a category route, including recursive category descendants.

Note that the method name is spelled `Rescursive` in the API.

### `GetProductsRescursiveByRouteAsync(string route, ProductQuery? query = null, CancellationToken ct = default)`

```csharp
ProductResponse products = await _catalog.GetProductsRescursiveByRouteAsync("/shop/shoes", ct: ct);
```

Async version of `GetProductsRescursiveByRoute(...)`.

### `GetProductsByIds(ProductQuery? query = null)`

```csharp
ProductResponse products = _catalog.GetProductsByIds(new ProductQuery
{
    Ids = new[] { 1001, 1002 }
});
```

Returns products matching `query.Ids` using the current request store or `query.StoreAlias`.

### `GetProductsByIdsAsync(ProductQuery? query = null, CancellationToken ct = default)`

```csharp
ProductResponse products = await _catalog.GetProductsByIdsAsync(new ProductQuery
{
    Ids = new[] { 1001, 1002 }
}, ct);
```

Async version of `GetProductsByIds(ProductQuery? ...)`.

### `GetProductsByIds(string storeAlias, ProductQuery? query = null)`

```csharp
ProductResponse products = _catalog.GetProductsByIds("Store", new ProductQuery
{
    Ids = new[] { 1001, 1002 }
});
```

Returns products matching `query.Ids` from a specific store.

### `GetProductsByIdsAsync(string storeAlias, ProductQuery? query = null, CancellationToken ct = default)`

```csharp
ProductResponse products = await _catalog.GetProductsByIdsAsync("Store", new ProductQuery
{
    Ids = new[] { 1001, 1002 }
}, ct);
```

Async version of `GetProductsByIds(string ...)`.

### `GetProductsBySkus(ProductQuery? query = null)`

```csharp
ProductResponse products = _catalog.GetProductsBySkus(new ProductQuery
{
    Skus = new[] { "SKU-1", "SKU-2" }
});
```

Returns products matching `query.Skus` using the current request store or `query.StoreAlias`.

### `GetProductsBySkusAsync(ProductQuery? query = null, CancellationToken ct = default)`

```csharp
ProductResponse products = await _catalog.GetProductsBySkusAsync(new ProductQuery
{
    Skus = new[] { "SKU-1", "SKU-2" }
}, ct);
```

Async version of `GetProductsBySkus(ProductQuery? ...)`.

### `GetProductsBySkus(string storeAlias, ProductQuery? query = null)`

```csharp
ProductResponse products = _catalog.GetProductsBySkus("Store", new ProductQuery
{
    Skus = new[] { "SKU-1", "SKU-2" }
});
```

Returns products matching `query.Skus` from a specific store.

### `GetProductsBySkusAsync(string storeAlias, ProductQuery? query = null, CancellationToken ct = default)`

```csharp
ProductResponse products = await _catalog.GetProductsBySkusAsync("Store", new ProductQuery
{
    Skus = new[] { "SKU-1", "SKU-2" }
}, ct);
```

Async version of `GetProductsBySkus(string ...)`.

### `GetProductsByKeys(ProductQuery? query = null)`

```csharp
ProductResponse products = _catalog.GetProductsByKeys(new ProductQuery
{
    Keys = new[] { productKey1, productKey2 }
});
```

Returns products matching `query.Keys` using the current request store or `query.StoreAlias`.

### `GetProductsByKeysAsync(ProductQuery? query = null, CancellationToken ct = default)`

```csharp
ProductResponse products = await _catalog.GetProductsByKeysAsync(new ProductQuery
{
    Keys = new[] { productKey1, productKey2 }
}, ct);
```

Async version of `GetProductsByKeys(ProductQuery? ...)`.

### `GetProductsByKeys(string storeAlias, ProductQuery? query = null)`

```csharp
ProductResponse products = _catalog.GetProductsByKeys("Store", new ProductQuery
{
    Keys = new[] { productKey1, productKey2 }
});
```

Returns products matching `query.Keys` from a specific store.

### `GetProductsByKeysAsync(string storeAlias, ProductQuery? query = null, CancellationToken ct = default)`

```csharp
ProductResponse products = await _catalog.GetProductsByKeysAsync("Store", new ProductQuery
{
    Keys = new[] { productKey1, productKey2 }
}, ct);
```

Async version of `GetProductsByKeys(string ...)`.

## Single category lookup

### `GetCategory(string id, string? storeAlias = null, bool global = false, bool raiseEvent = true)`

```csharp
ICategory? category = _catalog.GetCategory(categoryKey.ToString());
```

Returns a category by string identifier.

The string can represent an integer id, guid, or UDI.

### `GetCategoryAsync(string id, string? storeAlias = null, bool global = false, bool raiseEvent = true, CancellationToken ct = default)`

```csharp
ICategory? category = await _catalog.GetCategoryAsync(categoryKey.ToString(), ct: ct);
```

Async version of `GetCategory(string ...)`.

### `GetCategory(int id, string? storeAlias = null, bool global = false, bool raiseEvent = true)`

```csharp
ICategory? category = _catalog.GetCategory(1234);
```

Returns a category by integer id.

### `GetCategoryAsync(int id, string? storeAlias = null, bool global = false, bool raiseEvent = true, CancellationToken ct = default)`

```csharp
ICategory? category = await _catalog.GetCategoryAsync(1234, ct: ct);
```

Async version of `GetCategory(int ...)`.

### `GetCategory(Guid id, string? storeAlias = null, bool global = false, bool raiseEvent = true)`

```csharp
ICategory? category = _catalog.GetCategory(categoryKey);
```

Returns a category by key.

### `GetCategoryAsync(Guid id, string? storeAlias = null, bool global = false, bool raiseEvent = true, CancellationToken ct = default)`

```csharp
ICategory? category = await _catalog.GetCategoryAsync(categoryKey, ct: ct);
```

Async version of `GetCategory(Guid ...)`.

### `GetCategoryByRoute(string route, string? storeAlias = null, bool raiseEvent = true)`

```csharp
ICategory? category = _catalog.GetCategoryByRoute("/shop/shoes");
```

Returns a category by route.

### `GetCategoryByRouteAsync(string route, string? storeAlias = null, bool raiseEvent = true, CancellationToken ct = default)`

```csharp
ICategory? category = await _catalog.GetCategoryByRouteAsync("/shop/shoes", ct: ct);
```

Async version of `GetCategoryByRoute(...)`.

## Category collections

### `GetRootCategories(string? storeAlias = null)`

```csharp
IEnumerable<ICategory> categories = _catalog.GetRootCategories();
```

Returns the store root categories based on the configured `CategoryRootLevel`.

### `GetRootCategoriesAsync(string? storeAlias = null, CancellationToken ct = default)`

```csharp
IReadOnlyList<ICategory> categories = await _catalog.GetRootCategoriesAsync(ct: ct);
```

Async version of `GetRootCategories(...)`.

### `GetAllCategories(string? storeAlias = null)`

```csharp
IEnumerable<ICategory> categories = _catalog.GetAllCategories();
```

Returns all categories for the current request store or a specific store.

### `GetAllCategoriesAsync(string? storeAlias = null, CancellationToken ct = default)`

```csharp
IReadOnlyList<ICategory> categories = await _catalog.GetAllCategoriesAsync(ct: ct);
```

Async version of `GetAllCategories(...)`.

### `GetCategoriesByIds(int[] ids, string? storeAlias = null)`

```csharp
IEnumerable<ICategory> categories = _catalog.GetCategoriesByIds(new[] { 1001, 1002 });
```

Returns categories matching integer ids.

### `GetCategoriesByIdsAsync(int[] ids, string? storeAlias = null, CancellationToken ct = default)`

```csharp
IReadOnlyList<ICategory> categories = await _catalog.GetCategoriesByIdsAsync(new[] { 1001, 1002 }, ct: ct);
```

Async version of `GetCategoriesByIds(...)`.

### `GetCategoriesByKeys(Guid[] keys, string? storeAlias = null)`

```csharp
IEnumerable<ICategory> categories = _catalog.GetCategoriesByKeys(new[] { categoryKey1, categoryKey2 });
```

Returns categories matching keys.

### `GetCategoriesByKeysAsync(Guid[] keys, string? storeAlias = null, CancellationToken ct = default)`

```csharp
IReadOnlyList<ICategory> categories = await _catalog.GetCategoriesByKeysAsync(new[] { categoryKey1, categoryKey2 }, ct: ct);
```

Async version of `GetCategoriesByKeys(...)`.

## Variants and variant groups

### `GetVariant(Guid id, string? storeAlias = null)`

```csharp
IVariant? variant = _catalog.GetVariant(variantKey);
```

Returns a variant by key.

### `GetVariantAsync(Guid id, string? storeAlias = null, CancellationToken ct = default)`

```csharp
IVariant? variant = await _catalog.GetVariantAsync(variantKey, ct: ct);
```

Async version of `GetVariant(Guid ...)`.

### `GetVariant(int id, string? storeAlias = null)`

```csharp
IVariant? variant = _catalog.GetVariant(1234);
```

Returns a variant by integer id.

### `GetVariantAsync(int id, string? storeAlias = null, CancellationToken ct = default)`

```csharp
IVariant? variant = await _catalog.GetVariantAsync(1234, ct: ct);
```

Async version of `GetVariant(int ...)`.

### `GetVariant(string sku, string? storeAlias = null)`

```csharp
IVariant? variant = _catalog.GetVariant("SKU-123");
```

Returns a variant by sku.

### `GetVariantAsync(string sku, string? storeAlias = null, CancellationToken ct = default)`

```csharp
IVariant? variant = await _catalog.GetVariantAsync("SKU-123", ct: ct);
```

Async version of `GetVariant(string ...)`.

### `GetVariantsByGroup(int id, string? storeAlias = null)`

```csharp
IEnumerable<IVariant> variants = _catalog.GetVariantsByGroup(variantGroupId);
```

Returns variants belonging to a variant group.

### `GetVariantsByGroupAsync(int id, string? storeAlias = null, CancellationToken ct = default)`

```csharp
IEnumerable<IVariant> variants = await _catalog.GetVariantsByGroupAsync(variantGroupId, ct: ct);
```

Async version of `GetVariantsByGroup(...)`.

### `GetVariantGroup(Guid key, string? storeAlias = null)`

```csharp
IVariantGroup? group = _catalog.GetVariantGroup(variantGroupKey);
```

Returns a variant group by key.

### `GetVariantGroupAsync(Guid key, string? storeAlias = null, CancellationToken ct = default)`

```csharp
IVariantGroup? group = await _catalog.GetVariantGroupAsync(variantGroupKey, ct: ct);
```

Async version of `GetVariantGroup(Guid ...)`.

### `GetVariantGroup(int id, string? storeAlias = null)`

```csharp
IVariantGroup? group = _catalog.GetVariantGroup(variantGroupId);
```

Returns a variant group by integer id.

### `GetVariantGroupAsync(int id, string? storeAlias = null, CancellationToken ct = default)`

```csharp
IVariantGroup? group = await _catalog.GetVariantGroupAsync(variantGroupId, ct: ct);
```

Async version of `GetVariantGroup(int ...)`.

## Metafields and related products

### `GetMetafields()`

```csharp
IEnumerable<Metafield> metafields = _catalog.GetMetafields();
```

Returns the catalog metafields known to Ekom.

### `GetRelatedProducts(Guid productId, int count = 4, string? storeAlias = null)`

```csharp
IEnumerable<IProduct> products = _catalog.GetRelatedProducts(productKey, count: 4);
```

Returns related products for a product key.

### `GetRelatedProductsAsync(Guid productId, int count = 4, string? storeAlias = null, CancellationToken ct = default)`

```csharp
IReadOnlyList<IProduct> products = await _catalog.GetRelatedProductsAsync(productKey, count: 4, ct: ct);
```

Async version of `GetRelatedProducts(...)`.

### `GetRelatedProductsBySku(string sku, int count = 4, string? storeAlias = null)`

```csharp
IEnumerable<IProduct> products = _catalog.GetRelatedProductsBySku("SKU-123", count: 4);
```

Returns related products for a product sku.

### `GetRelatedProductsBySkuAsync(string sku, int count = 4, string? storeAlias = null, CancellationToken ct = default)`

```csharp
IReadOnlyList<IProduct> products = await _catalog.GetRelatedProductsBySkuAsync("SKU-123", count: 4, ct: ct);
```

Async version of `GetRelatedProductsBySku(...)`.

## Search

### `ProductSearchAsync(SearchRequest req, CancellationToken ct = default)`

```csharp
ProductResponse results = await _catalog.ProductSearchAsync(new SearchRequest
{
    SearchQuery = "shoe",
    StoreAlias = "Store"
}, ct);
```

Searches products through the configured catalog search service.

If no node type aliases are provided, Ekom defaults to `ekmProduct` and `ekmVariant`.

## Supporting models

### `ProductQuery`

Use `ProductQuery` when returning product collections.

Common properties include:

- `StoreAlias`
- `Ids`
- `Keys`
- `Skus`
- `Page`
- `PageSize`
- `SearchQuery`
- `MetaFilters`
- `PropertyFilters`
- `OrderBy`
- `RaiseEvents`

### `SearchRequest`

Use `SearchRequest` with `ProductSearchAsync(...)`.

Common properties include:

- `SearchQuery`
- `StoreAlias`
- `NodeTypeAlias`
- `MetaFilters`
- `PropertyFilters`
- `OrderBy`
- `ExamineIndex`

### `ProductResponse`

Most collection methods return `ProductResponse`.

Useful response properties include:

- `Products`
- `ProductCount`
- `TotalProductCount`
- `Page`
- `PageSize`
- `PageCount`
- `Filters`
- `PropertySelectors`

## Notes

- Most lookup methods resolve the store from the current request when `storeAlias` is not provided.
- Product and category methods can raise catalog events unless `raiseEvent` is disabled.
- Product lookup can use global catalog fallback depending on `global` and configuration.
- Several collection methods use `ProductQuery` to apply filtering, paging, search, and sorting behavior.

## Related pages

- [Catalog Events](catalog-events.md)
- [Catalog Endpoints](catalog-endpoints.md)
- [Render a Category Page](render-category-page.md)
- [Render a Product Page](render-product-page.md)
- [Store API](store-api.md)

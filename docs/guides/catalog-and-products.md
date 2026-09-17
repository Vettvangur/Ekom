# Catalog and products

Ekom stores its catalog in Umbraco content and exposes the published catalog through `Ekom.API.Catalog`, product/category models, and public HTTP endpoints. Catalog reads are served from Ekom's per-store caches; pass a store alias explicitly when code does not run in a routed storefront request.

## Catalog structure

The standard content types are:

| Content type | Purpose |
| --- | --- |
| `ekmCategory` | Hierarchical catalog category. |
| `ekmProduct` | Sellable product and the parent of variant groups. |
| `ekmProductVariantGroup` | Groups selectable variants, such as a color. |
| `ekmProductVariant` | A sellable variant with its own SKU, price and stock. |

Products may belong to a primary category and additional categories. `IProduct.Categories` and `CategoryAncestors` expose those relationships. A product with variants is available when at least one variant is available; a product without variants uses its own stock. Backorder and stock-buffer behavior is covered in [Stock](stock.md).

## Read the routed catalog

Inject `Ekom.API.Catalog` for application code. `Catalog.Instance` remains available, but constructor injection is easier to test.

```csharp
using Ekom.API;
using Ekom.Models;

public sealed class ProductPageService(Catalog catalog)
{
    public Task<IProduct?> GetCurrentAsync(CancellationToken ct)
        => catalog.GetProductAsync(ct: ct);
}
```

`GetProductAsync()` and `GetCategoryAsync()` read the `ContentRequest` attached to the current HTTP request. For custom routes, background work, or integrations, use an explicit lookup:

```csharp
IProduct? bySku = await catalog.GetProductAsync("SKU-123", "Store", ct: ct);
IProduct? byKey = await catalog.GetProductAsync(productKey, "Store", ct: ct);
IProduct? byId = await catalog.GetProductAsync(1234, "Store", ct: ct);
IProduct? byRoute = await catalog.GetProductByRouteAsync("/shop/shoe", "Store", ct: ct);

ICategory? category = await catalog.GetCategoryAsync(categoryKey, "Store", ct: ct);
ICategory? byCategoryRoute = await catalog.GetCategoryByRouteAsync("/shop/shoes", "Store", ct: ct);
```

Product key, ID and SKU lookups can fall back to another store when `global` is `true`, or when `Ekom:GlobalCatalog` is enabled and `global` is omitted. Route lookup does not use that fallback. Category lookup has its own `global` argument and honors the requesting store's category `disable` value when falling back.

## Product collections

Use `ProductQuery` for filtering, ordering and paging:

```csharp
ProductResponse response = await catalog.GetAllProductsAsync(new ProductQuery
{
    StoreAlias = "Store",
    Page = 1,
    PageSize = 24,
    OrderBy = Ekom.Utilities.OrderBy.PriceAsc,
    MetaFilters = new Dictionary<string, List<string>>
    {
        ["brand"] = ["Ekom"],
    },
}, ct);
```

Important members are `Ids`, `Keys`, `Skus`, `StoreAlias`, `Page`, `PageSize`, `SearchQuery`, `MetaFilters`, `PropertyFilters`, `PropertySelectors`, `OrderBy`, `FilterOutZeroPriceProducts`, `Filter`, and `RaiseEvents`. `Filter` is a server-side delegate and cannot be supplied over JSON.

Collection entry points include:

| API | Result |
| --- | --- |
| `GetAllProductsAsync(...)` | All products in a store. |
| `GetProductsByIdsAsync(...)` | Products matching `ProductQuery.Ids`. |
| `GetProductsByKeysAsync(...)` | Products matching `ProductQuery.Keys`. |
| `GetProductsBySkusAsync(...)` | Products matching `ProductQuery.Skus`. |
| `category.ProductsAsync(...)` | Direct products of a category. |
| `category.ProductsRecursiveAsync(...)` | Products in a category and descendants. |
| `GetProductsRescursiveByRouteAsync(...)` | Recursive category products by route. The public method name is spelled `Rescursive`. |

`ProductResponse` exposes `Products`, counts, paging information, filters and property selectors.

`new ProductQuery(HttpContext.Request.Query)` recognizes `filter_*`, `property_*`, `q`, `page`/`p`, and `orderby` query values. This is the pattern used by the sample category view.

## Variants and related products

```csharp
IVariant? variant = await catalog.GetVariantAsync(variantKey, "Store", ct);
IVariant? variantBySku = await catalog.GetVariantAsync("VAR-123", "Store", ct);
IVariantGroup? group = await catalog.GetVariantGroupAsync(groupKey, "Store", ct);
IEnumerable<IVariant> groupVariants = await catalog.GetVariantsByGroupAsync(group.Id, "Store", ct);

IReadOnlyList<IProduct> related = await catalog.GetRelatedProductsAsync(
    productKey,
    count: 4,
    storeAlias: "Store",
    ct: ct);
```

Products also expose `PrimaryVariant`, `PrimaryVariantGroup`, `VariantGroups`, and `AllVariants`. Render a real variant key in add-to-order requests when the product requires a variant.

## Search

`Catalog.ProductSearchAsync(SearchRequest, ct)` delegates matching to `ICatalogSearchService`, then resolves returned IDs through the Ekom product cache. If `NodeTypeAlias` is empty, Ekom searches `ekmProduct` and `ekmVariant`.

```csharp
ProductResponse result = await catalog.ProductSearchAsync(new SearchRequest
{
    SearchQuery = "running shoe",
    StoreAlias = "Store",
    Page = 1,
    PageSize = 20,
}, ct);
```

The default Umbraco implementation uses the configured Examine index (`Ekom:ExamineSearchIndex`, default `ExternalIndex`). To replace search, register an `ICatalogSearchService` after the normal Umbraco composition pipeline has built; do not call `AddEkom(...)` manually in a standard package installation. Its async methods are `ProductQueryAsync`, `PublicQueryAsync`, and `InternalQueryAsync`.

## Catalog events

The async catalog events can replace a single result or filter a collection before it is returned:

- `CatalogEvents.BeforeReturnProductAsync`
- `CatalogEvents.BeforeReturnProductsAsync`
- `CatalogEvents.BeforeReturnCategoryAsync`
- `CatalogEvents.BeforeReturnCategoriesAsync`
- `CatalogEvents.CurrencyStringFormat` for synchronous currency formatting

```csharp
CatalogEvents.BeforeReturnProductsAsync += (args, ct) =>
{
    args.Products = args.Products.Where(product => product.Available);
    return ValueTask.CompletedTask;
};
```

Unsubscribe static event handlers during application termination. Set `raiseEvent: false` on supported single-item lookups, or `ProductQuery.RaiseEvents = false` for collection processing, when internal code needs the unmodified cached model.

## HTTP catalog API

Public routes are under `/ekom/catalog`:

| Method and route | Purpose |
| --- | --- |
| `GET /product/{guid|int}` | Product by key or ID. |
| `GET /product/sku/{sku}` | Product by SKU. |
| `GET /product/route?route=...` | Product by route. |
| `GET|POST /category/{guid|int}` | Category by key or ID. |
| `GET|POST /category/route?route=...` | Category by route. |
| `GET|POST /allproducts` | Product listing; a JSON `ProductQuery` may be posted. |
| `GET|POST /products/{guid|int}` | Direct category products. |
| `GET|POST /productsrecursive/{guid|int}` | Recursive category products. |
| `GET|POST /productsrecursive/route?route=...` | Recursive products by route. |
| `GET|POST /productsbyids` | `ProductQuery` with `Ids`. |
| `GET|POST /productsbykeys` | `ProductQuery` with `Keys`. |
| `GET|POST /productsbyskus` | `ProductQuery` with `Skus`. |
| `GET|POST /productsearch` | `SearchRequest`. |
| `GET|POST /rootcategories` | Categories at `Ekom:CategoryRootLevel`. |
| `GET|POST /allcategories` | All store categories. |
| `GET|POST /categoriesbyids` | JSON array of integer IDs. |
| `GET|POST /categoriesbykeys` | JSON array of GUID keys. |
| `GET|POST /subcategories...` | Direct or recursive child categories. |
| `GET|POST /categoryfilters/{guid|int}` | Category filter data. |
| `GET|POST /relatedproducts...` | Related products by key(s) or SKU(s). |
| `GET|POST /metafields` | Configured metafields. |

Although several actions advertise both verbs, actions with `[FromBody]` are most reliably called with `POST` and JSON. A missing single product/category returns `404`; collection routes normally return an empty result.

## Razor patterns

The repository samples use `await _catalog.GetCategoryAsync()` followed by `category.ProductsRecursiveAsync(new ProductQuery(Context.Request.Query) { ... })`, and `await _catalog.GetProductAsync()` followed by `product.Price`, `product.Available`, images and variant groups. Use `Html.BeginEkomForm(FormType.AddToOrderProduct, ...)` or `POST /ekom/order/add` with `productId`, optional `variantId`, `storeAlias`, and a positive `quantity`.

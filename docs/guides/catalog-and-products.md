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

## Backoffice catalog collection and content pickers

Umbraco 17.8 and 18.3 render a node's configured collection inside the document picker. Ekom's `Ekom.CollectionView.Catalog` uses the `Ekom.Condition.CatalogPicker` extension condition to exclude itself from picker collections by default. Ordinary workspace bulk selection does not hide the catalog view.

Keep at least one native view, such as `Umb.CollectionView.Document.Table`, enabled in the collection Data Type's layouts alongside Catalog. Umbraco filters the configured layouts against the permitted views; a collection configured with **only Catalog** would have no available picker view when Catalog is excluded. Catalog can remain the first/default layout for normal editing.

To opt in later, change the condition on `Ekom.CollectionView.Catalog` in `Ekom/Ekom.Web/Ekom.Web.U17/Client/src/manifests.ts` and rebuild both web asset projects:

```typescript
{
  alias: 'Ekom.Condition.CatalogPicker',
  allowInPickers: true,
},
```

When enabled in a picker, Catalog hosts Umbraco's native document table using the existing collection context. Umbraco owns the browsed category, allowed item filters, single/multiple selection, pagination, and picker navigation. Ekom does not load its custom catalog data, modify browser history, force multiple selection, or hide the native pagination in this mode. The custom catalog cards, sorting, breadcrumbs, and navigation remain in normal workspace collections.

Picker detection uses the explicit `selectionConfiguration.selectOnly: true` configuration, not the live `selectOnly` state (which also becomes true during workspace bulk selection). U18 shares the U17 client source.

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


## Database price storage

By default, Ekom stores prices on Umbraco nodes. For frequent ERP price updates, enable table storage and restart the application:

```json
{
  "Ekom": {
    "UseDatabasePrices": true
  }
}
```

Startup creates `EkomPrice` on SQLite and SQL Server. Each row identifies a node key, property alias, store alias, and currency. Identifiers are case-insensitive. Amounts use `decimal(18,4)` and must be non-negative. Base prices and `ekmDiscountPrice` use separate rows.

See [ERP price-only sync](imports.md#erp-price-only-sync-without-saving-or-publishing) for a complete injectable change-feed service and identifier mapping guidance.

Write ERP prices without saving or publishing content:

```csharp
await Ekom.API.Prices.Instance.SetPriceAsync(
    productKey, "Web", "en-US", 49.95m,
    cancellationToken: cancellationToken);

await Ekom.API.Prices.Instance.SetPriceAsync(
    productKey, "Web", "en-US", 39.95m,
    propertyAlias: "ekmDiscountPrice",
    cancellationToken: cancellationToken);
```

`Ekom.API.Prices` is also injectable. `GetAsync(nodeKey, propertyAlias)` returns the stored overrides. Both `IContent.SetPrice` and `SetPriceAsync` honor this setting. In node mode these helpers change the content value and the caller still saves/publishes it; in database mode they write immediately and the caller should skip saving/publishing for price-only changes. For direct ERP updates, resolve an existing node key. The built-in importer assigns keys and writes prices before first publication.

The built-in importer also routes `ImportProduct.Price` and `ImportVariant.Price` to the table when enabled. Prices are applied even when content is unchanged, and the default content hash excludes prices to avoid saves/publishes for price-only changes. See [importer behavior](imports.md#using-the-built-in-ekom-importer) for custom comparers and first-sync behavior.

The Ekom Price datatype loads table overrides in Umbraco 13, 17, and 18. Editing a field in database mode saves that currency immediately when leaving the field; no publish is required. A notice explains that the change is live. Ordinary document saves do not copy old node price JSON back into the table. Each save updates only the edited currency, so it cannot overwrite unrelated currencies changed by an ERP sync. Concurrent changes to the same price use the last committed write. The U17/U18 editor rebuilds displayed prices from the current document and the latest overrides when it loads; removed overrides restore node fallback values. Navigating to another document discards the old price state and ignores outstanding save responses for the previous document.

Existing node prices remain a fallback for individual currencies that have no table row. A stored zero is an explicit value; the existing variant rule that a zero base price inherits its parent remains unchanged. Enabling table storage does not migrate or modify node values. Populate the table through your ERP sync to make it the source for every price. Disabling the setting returns to the original node values, so copy current prices back first if you want to retain them when switching back.

Product and variant prices, native discounts, provider prices, and variant/catalog displays resolve table values. Runtime resolution loads all price overrides once per node per HTTP request, shared across base prices, discounts, and stores. The next request on any application server sees committed updates; a local write invalidates the current request snapshot. Outside HTTP requests, each resolution reads fresh data. Calculated-price caches retain only the latest price version for a given context, avoiding an entry for every ERP revision. Measure query load for large catalogs. Custom code reading raw Umbraco property JSON still sees the node value; use Ekom price models or `Prices.Resolve` to apply overrides.

Updates do not emit Umbraco publish notifications. `Ekom.Events.PriceEvents.PriceChanged` fires after commit for each changed override, including removals (`NewValue == null`). Handlers should enqueue work. Subscriber failures are logged without rolling back or retrying committed prices. The Algolia plugin queues a product upsert for published product or variant price changes, including inherited variant prices, when product indexing is enabled. Notifications and the indexing queue are best-effort, not a durable outbox: use periodic reconciliation after process failures or queue overflow. Other search indexes and page caches need their own subscriber. Prices are live data, without Umbraco draft, rollback, or scheduled-publishing semantics.


Price endpoints require an authenticated, approved backoffice user with access to the node's content root, update and publish permissions, and access to the requested Ekom store. Administrators retain node access. Custom `ISecurityService` implementations must implement `IsAuthenticated`, `CanEditContent`, and `CanEditPrices`; their defaults deny access. `CanEditContent` checks content-root access and update permission without requiring publish permission. The variant manager uses this check for reading and draft editing, including when database storage is disabled. Publishing content or changing a live table price additionally requires publish permission. An unchanged price baseline does not prevent a non-publisher from saving metadata as a draft. In database mode, variant responses contain only the stores the user can access, including both displayed values and original override baselines.

To remove an override and immediately restore the node fallback (zero is a real override, not removal):

```csharp
await prices.RemovePriceAsync(productKey, "Web", "en-US", cancellationToken: cancellationToken);
```

The authenticated backoffice equivalent is `DELETE /ekom/backoffice/Prices/{id}/{propertyAlias}/{storeAlias}/{currency}`. A batch `PriceMutation` with `Price: null` also removes an override and can use an expected-value check.

To switch back to node storage without reverting to stale prices:

1. Pause ERP writers and backoffice price editing.
2. While database mode remains enabled, load each affected product and variant as `IContent`, call `content.CopyDatabasePricesToNode()` (namespace `Ekom.Utilities`), and explicitly save/publish it using the site's usual content workflow. Repeat with `"ekmDiscountPrice"` and any custom price properties. The helper merges overrides with node fallbacks, including legacy scalar/array formats; it does not save or publish. Unknown store/currency mappings throw before changing content.
3. Verify every affected node was published successfully, then disable `UseDatabasePrices` and restart all application servers. Resume writers in node mode.

`Prices.Get`/`GetAsync` remain available for exporting existing rows when the flag is off. Overrides remain in the table and take effect again if database mode is re-enabled; remove obsolete overrides while database mode is enabled if this is not desired.


### Migrating existing node prices without an ERP

Database storage does not require an ERP. Enable `Ekom:UseDatabasePrices` and restart to create the table. Prices continue using node values until an override exists, so enabling the flag alone does not require migration. To populate the table, call `MigrateNodePricesAsync` for each product and variant using its **published raw property value**:

```csharp
using Ekom.API;
using Ekom.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

public static async Task<int> MigratePublishedPricesAsync(
    Prices prices,
    IEnumerable<IPublishedContent> publishedNodes,
    IReadOnlyList<IStore> stores,
    CancellationToken cancellationToken = default)
{
    var inserted = 0;
    foreach (var node in publishedNodes)
    {
        foreach (var alias in new[] { "price", "ekmDiscountPrice" })
        {
            if (!node.HasProperty(alias)) continue;
            inserted += await prices.MigrateNodePricesAsync(
                node.Key, node.Value<string>(alias), stores, alias, cancellationToken);
        }
    }
    return inserted;
}
```

Run this from a controlled migration job. Supply the published products and variants under the intended catalog root, and the Ekom stores applicable to that catalog. Process a large catalog in pages and record progress. Add any custom price property aliases to the loop. Do not pass calculated storefront prices: those may include discounts, VAT conversion, or inherited parent prices.

The migration copies scalar, currency-array, store-keyed, and `values`-wrapped node formats. Scalars belong to each store's default currency. Missing values and currencies/stores outside the supplied configuration are skipped; review those mappings before migration. Zero is copied as zero, preserving variant inheritance behavior. Existing table overrides are preserved, including prices already edited in the backoffice. Re-running the job only inserts missing overrides.

Each node/property batch is atomic. Validation failures leave that batch unchanged; earlier batches stay committed. A competing insertion raises `PriceConflictException` instead of overwriting the other writer; retry that node/property to skip the newly existing row. Pause price editing during the initial migration for a predictable snapshot. Use published values rather than `IContent` draft values, because table writes are immediately live. No node save or publish is performed, and source node values remain available for rollback.

After migration, prices can be managed entirely through the Ekom Price datatype and variant manager. Table updates issue the normal price-change notifications, including Algolia reindex jobs when configured; allow the indexing queue to drain for large catalogs.


Variant-manager saves validate required custom fields before writing prices. Bulk saves validate all submitted groups and variants before processing any of them, so a missing required field cannot leave an earlier variant's price changed. This validation is not a transaction spanning Umbraco content, stock, and price storage: a later infrastructure failure may still leave completed writes in place.

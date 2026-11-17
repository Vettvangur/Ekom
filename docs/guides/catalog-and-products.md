[Ekom docs](../README.md) / [Guides](README.md) / Catalog and products

# Catalog and products

The catalog is the categories, products and variants that a store sells. Editors build it as Umbraco content, and your code reads the published catalog through `Ekom.API.Catalog` or Ekom's HTTP endpoints. This page shows how to find products and categories, list and filter them, search them, and change what Ekom returns.

Ekom reads the catalog from memory, with a separate copy for each store. In code that does not run inside a storefront request, pass the store alias to every call. See [Stores](stores.md).

On this page:

- [How the catalog is organized](#how-the-catalog-is-organized)
- [Find a product or category](#find-a-product-or-category)
- [List and filter products](#list-and-filter-products)
- [Variants and related products](#variants-and-related-products)
- [Search](#search)
- [Change what the catalog returns](#change-what-the-catalog-returns)
- [HTTP catalog API](#http-catalog-api)
- [Show products in Razor views](#show-products-in-razor-views)
- [Browse the catalog in the backoffice](#browse-the-catalog-in-the-backoffice)

## How the catalog is organized

| Content type | What it is |
| --- | --- |
| `ekmCatalog` | The root of a catalog. Categories go under it. |
| `ekmCategory` | A category. Categories can contain categories and products. |
| `ekmProduct` | A product. It is the parent of its variant groups. |
| `ekmProductVariantGroup` | A group of variants, for example one color. |
| `ekmProductVariant` | A variant the customer can buy, with its own SKU, price and stock. |

A product lives under one category, its primary category. It can also be linked to more categories with its `categories` picker. `IProduct.Categories` and `IProduct.CategoryAncestors` give you those categories.

A product with variants is available when at least one of its variants is available. A product without variants uses its own stock. Backorder and the stock buffer are covered in [Stock](stock.md#how-ekom-decides-that-a-product-is-available).

## Find a product or category

Inject `Ekom.API.Catalog`. `Catalog.Instance` also works, but injection is easier to test.

### On a product or category page

On a page that Ekom routed to a product or category, call the methods without arguments:

```csharp
using Ekom.API;
using Ekom.Models;

public sealed class ProductPageService(Catalog catalog)
{
    public Task<IProduct?> GetCurrentAsync(CancellationToken ct)
        => catalog.GetProductAsync(ct: ct);
}
```

`GetProductAsync()` and `GetCategoryAsync()` read the product or category that Ekom found for the current HTTP request. They return `null` when the request is not for a product or category page.

### By SKU, key, ID or route

In custom routes, background jobs and integrations, look the item up directly:

```csharp
IProduct? bySku = await catalog.GetProductAsync("SKU-123", "Store", ct: ct);
IProduct? byKey = await catalog.GetProductAsync(productKey, "Store", ct: ct);
IProduct? byId = await catalog.GetProductAsync(1234, "Store", ct: ct);
IProduct? byRoute = await catalog.GetProductByRouteAsync("/shop/shoe", "Store", ct: ct);

ICategory? category = await catalog.GetCategoryAsync(categoryKey, "Store", ct: ct);
ICategory? byCategoryRoute = await catalog.GetCategoryByRouteAsync("/shop/shoes", "Store", ct: ct);
```

- Each method returns `null` when nothing matches.
- When you leave out the store alias, Ekom uses the store of the current request, or the first store if there is none.
- An unknown store alias throws `StoreNotFoundException`.
- `GetCategoryAsync(string id, ...)` accepts an integer ID, a GUID key or an Umbraco UDI as a string.

### Find a product or category in another store

By default a lookup only searches the store you name. A product lookup by key, ID or SKU also searches the other stores when you pass `global: true`, or when `Ekom:GlobalCatalog` is `true` and you leave `global` out.

```csharp
IProduct? anyStore = await catalog.GetProductAsync("SKU-123", "Store", global: true, ct: ct);
```

A category lookup by key or ID searches the other stores when you pass `global: true` or when `Ekom:GlobalCatalog` is `true`. If the category found in another store has its `disable` value set for the store you asked for, Ekom returns `null`.

Lookups by route never search other stores.

## List and filter products

Use a `ProductQuery` to filter, sort and page a list of products:

```csharp
using Ekom.Utilities;

ProductResponse response = await catalog.GetAllProductsAsync(new ProductQuery
{
    StoreAlias = "Store",
    Page = 1,
    PageSize = 24,
    OrderBy = OrderBy.PriceAsc,
    MetaFilters = new Dictionary<string, List<string>>
    {
        ["brand"] = ["Ekom"],
    },
}, ct);

foreach (IProduct product in response.Products)
{
    // render the product
}
```

### Where to get the list from

| Method | Products |
| --- | --- |
| `catalog.GetAllProductsAsync(query, ct)` | All products in the store. |
| `catalog.GetProductsByIdsAsync(query, ct)` | The products in `query.Ids`. |
| `catalog.GetProductsByKeysAsync(query, ct)` | The products in `query.Keys`. |
| `catalog.GetProductsBySkusAsync(query, ct)` | The products in `query.Skus`. |
| `category.ProductsAsync(query, ct)` | The products directly in a category. |
| `category.ProductsRecursiveAsync(query, ct)` | The products in a category and all categories below it. |
| `catalog.GetProductsRescursiveByRouteAsync(route, query, ct)` | The products in the category at that route and all categories below it. The method name is spelled `Rescursive`. |

### ProductQuery members

| Member | What it does |
| --- | --- |
| `StoreAlias` | The store to read from. Empty means the store of the current request. |
| `Ids`, `Keys`, `Skus` | The products to return, for the methods that take them. |
| `Page`, `PageSize` | Paging. Ekom pages the result only when both are set. |
| `OrderBy` | The sort order. The default is the `Ekom:DefaultProductOrderBy` setting, which is `DateDesc` when not set. `NoOrder` keeps the original order. |
| `MetaFilters` | Filters on metafields, as field alias to allowed values. |
| `PropertyFilters` | Filters on product properties, as property alias to allowed values. |
| `PropertySelectors`, `PropertySelectorsSeparator` | Count the values of a property across the result, for building filter menus. The key `category` counts categories. |
| `SearchQuery`, `SearchFields` | Keep only products that the search service finds for this text. See [Search](#search). |
| `AllFiltersVisible` | `true`: build `Filters` from all products before filtering. `false`: build them from the filtered products. |
| `FilterOutZeroPriceProducts` | `true`: leave out products whose price (or primary variant price) is not above zero. |
| `Filter` | A `Func<IProduct, bool>` you supply in C#. It cannot be sent over JSON. |
| `RaiseEvents` | `false`: do not run the [catalog events](#change-what-the-catalog-returns) on this list. The default is `true`. |

`OrderBy` values: `UmbracoSortOrderAsc`, `UmbracoSortOrderDesc`, `TitleAsc`, `TitleDesc`, `PriceAsc`, `PriceDesc`, `DateAsc`, `DateDesc`, `SkuAsc`, `SkuDesc`, `Score`, `NoOrder`.

### What you get back

`ProductResponse` has:

- `Products`: the products on the requested page, or all of them when you do not page.
- `ProductCount` and `TotalProductCount`: the number of products after filtering.
- `Page`, `PageSize`, `PageCount`: set when the result is paged.
- `Filters`: the metafield values you can filter on, grouped by field.
- `PropertySelectors`: the counted values for each property selector.

### Build a query from the URL

`new ProductQuery(HttpContext.Request.Query)` reads these query string values:

| Query string | Sets |
| --- | --- |
| `filter_{alias}` | `MetaFilters[alias]` |
| `property_{alias}` | `PropertyFilters[alias]` |
| `q` | `SearchQuery` |
| `page` or `p` | `Page`. The default is `1`. |
| `orderby` | `OrderBy`. An unknown value is ignored. |

It does not set `PageSize`, so set it yourself if you want paging. The sample category view does this:

```csharp
ProductResponse products = await category.ProductsRecursiveAsync(new ProductQuery(Context.Request.Query)
{
    PageSize = 6,
    AllFiltersVisible = true,
});
```

## Variants and related products

```csharp
IVariant? variant = await catalog.GetVariantAsync(variantKey, "Store", ct);
IVariant? variantBySku = await catalog.GetVariantAsync("VAR-123", "Store", ct);

IVariantGroup? group = await catalog.GetVariantGroupAsync(groupKey, "Store", ct);
if (group != null)
{
    IEnumerable<IVariant> groupVariants = await catalog.GetVariantsByGroupAsync(group.Id, "Store", ct);
}

IReadOnlyList<IProduct> related = await catalog.GetRelatedProductsAsync(
    productKey,
    count: 4,
    storeAlias: "Store",
    ct: ct);
```

`GetVariantAsync` by SKU throws `ArgumentException` when the SKU is empty. `GetRelatedProductsAsync` returns an empty list when the product is not found. `GetRelatedProductsBySkuAsync` does the same by SKU.

A product also gives you its variants directly: `PrimaryVariant`, `PrimaryVariantGroup`, `VariantGroups` and `AllVariants`.

When a product has variants, the customer must pick one. Send the key of a real variant when you add the product to the basket.

## Search

`catalog.ProductSearchAsync(request, ct)` passes the search to the registered `ICatalogSearchService`, then returns the matching products from the store's catalog.

```csharp
ProductResponse result = await catalog.ProductSearchAsync(new SearchRequest
{
    SearchQuery = "running shoe",
    StoreAlias = "Store",
    Page = 1,
    PageSize = 20,
}, ct);
```

- An empty `SearchQuery` returns an empty result.
- When `NodeTypeAlias` is empty, Ekom searches the node types `ekmProduct` and `ekmVariant`.
- `Page` and `PageSize` go to the search service, which returns that page of hits.
- `MetaFilters`, `PropertyFilters` and `OrderBy` on the request are applied to the products found. The default `OrderBy` of a `SearchRequest` is `NoOrder`.
- If no `ICatalogSearchService` is registered, the result is empty. Ekom's own search service logs a search error and returns no hits instead of throwing.

The search service that Ekom registers depends on the Umbraco version:

| Umbraco | How Ekom searches |
| --- | --- |
| 17 and 18 | Examine, using the index named in `Ekom:ExamineSearchIndex` (default `ExternalIndex`). The `SearchFields` settings (exact, wildcard, fuzzy, boost) apply. |
| 19 | Umbraco's search provider. `ExternalIndex`, or no index setting, means Umbraco's published content index. The search provider decides matching and relevance, so the `SearchFields` settings do not apply. |

`SearchRequest.ExamineIndex` overrides the index for one request.

### Use your own search

Implement `Ekom.Services.ICatalogSearchService`. It has three methods: `ProductQueryAsync`, `PublicQueryAsync` and `InternalQueryAsync`. Ekom registers its own implementation in `EkomComposer`, so register yours in a composer that runs after it:

```csharp
using Ekom.Services;
using Ekom.Umb;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

[ComposeAfter(typeof(EkomComposer))]
public sealed class SearchComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.Services.AddScoped<ICatalogSearchService, AlgoliaCatalogSearchService>();
}
```

`AlgoliaCatalogSearchService` stands for your own class. Do not call `AddEkom(...)` yourself in a normal package install; `EkomComposer` already does that.

## Change what the catalog returns

Ekom raises an event before it returns a product, a list of products, a category or a list of categories. A handler can replace a single item or change a list. The events are static and live in `Ekom.Events.CatalogEvents`:

| Event | Arguments |
| --- | --- |
| `BeforeReturnProductAsync` | `ProductEventArgs.Product` |
| `BeforeReturnProductsAsync` | `ProductsEventArgs.Products` |
| `BeforeReturnCategoryAsync` | `CategoryEventArgs.Category` |
| `BeforeReturnCategoriesAsync` | `CategoriesEventArgs.Categories` |
| `CurrencyStringFormat` | `CurrencyStringEventArgs`, a synchronous event for formatting a price as text |

This handler hides products that cannot be bought:

```csharp
using Ekom.Events;

CatalogEvents.BeforeReturnProductsAsync += (args, ct) =>
{
    args.Products = args.Products.Where(product => product.Available);
    return ValueTask.CompletedTask;
};
```

- Handlers run one after another, in the order they were added.
- Static events keep a reference to your handler. Remove the handler when the application shuts down.
- To get the cached item without the events, pass `raiseEvent: false` to a single-item lookup, or set `ProductQuery.RaiseEvents = false` for a list.

The older synchronous events `BeforeReturnProduct`, `BeforeReturnProducts`, `BeforeReturnCategory` and `BeforeReturnCategories` still run, after the async ones. They are marked obsolete; use the async events.

## HTTP catalog API

The public catalog routes are under `/ekom/catalog`:

| Method and route | Returns |
| --- | --- |
| `GET /product/{key or id}` | One product by GUID key or integer ID. |
| `GET /product/sku/{sku}` | One product by SKU. |
| `GET /product/route?route=...` | One product by route. |
| `GET` or `POST /category/{key or id}` | One category by GUID key or integer ID. |
| `GET` or `POST /category/route?route=...` | One category by route. |
| `GET` or `POST /allproducts` | All products. Post a `ProductQuery` as JSON to filter. |
| `GET` or `POST /products/{key or id}` | The products directly in a category. |
| `GET` or `POST /productsrecursive/{key or id}` | The products in a category and below it. |
| `GET` or `POST /productsrecursive/route?route=...` | The same, by category route. |
| `GET` or `POST /productsbyids` | A `ProductQuery` with `Ids`. |
| `GET` or `POST /productsbykeys` | A `ProductQuery` with `Keys`. |
| `GET` or `POST /productsbyskus` | A `ProductQuery` with `Skus`. |
| `GET` or `POST /productsearch` | A `SearchRequest`. |
| `GET` or `POST /rootcategories` | The categories at level `Ekom:CategoryRootLevel` (default `3`). |
| `GET` or `POST /allcategories` | All categories in the store. |
| `GET` or `POST /categoriesbyids` | A JSON array of integer IDs. |
| `GET` or `POST /categoriesbykeys` | A JSON array of GUID keys. |
| `GET` or `POST /subcategories/{key or id}` | The direct child categories. |
| `GET` or `POST /subcategoriesrecursive/{key or id}` | All categories below a category. |
| `GET` or `POST /categoryfilters/{key or id}` | The filter data of a category. |
| `GET` or `POST /relatedproducts/{key}/{count}` | Related products of one product. |
| `GET` or `POST /relatedproducts?ids=...&count=...` | Related products of several products, at most `count` in total. |
| `GET` or `POST /relatedproductsbysku/{sku}/{count}` | Related products by SKU. |
| `GET` or `POST /relatedproductsbyskus?skus=...&count=...` | Related products of several SKUs, at most `count` in total. |
| `GET` or `POST /metafields` | The metafields set up in Ekom. |

- Routes that take a JSON body read it with `[FromBody]`. Call them with `POST` and a JSON body.
- A single product or category that is not found returns `404`.
- `productsbyids`, `productsbykeys` and `productsbyskus` without a body return `400`. So do `categoriesbykeys` with an empty array and `category/route` without a route.
- A list that matches nothing returns an empty result.

## Show products in Razor views

The U17 sample site shows the pattern:

- The category view calls `await _catalog.GetCategoryAsync()`, then `category.ProductsRecursiveAsync(new ProductQuery(Context.Request.Query) { ... })`.
- The product view calls `await _catalog.GetProductAsync()`, then reads `product.Price`, `product.Available`, `product.Images`, `product.PrimaryVariantGroup` and `product.PrimaryVariant`.

To add a product to the basket, use `Html.BeginEkomForm(FormType.AddToOrderProduct, ...)`, or send `POST /ekom/order/add` with `productId`, `variantId` (when the product has variants), `storeAlias` and `quantity` (default `1`). See [Orders and baskets](orders-and-baskets.md#add-order-lines).

## Browse the catalog in the backoffice

Ekom adds a **Catalog** view to the collection of catalog and category nodes in the Umbraco backoffice. It shows the subcategories and products of a category as cards.

### What the Catalog view shows

A category lists:

- the products directly under it,
- the products linked to it through their `categories` picker. These have a **Linked** label.

A product is listed once per category and counted once in the totals of a branch. Linked products are read from the relations Umbraco records when a product is saved. A product whose picker value was written straight to the database appears after its next save.

Each product card shows the image, name, SKU, price (with the original price struck through while a product discount applies), number of variants, publish state, and stock. Published products with unpublished changes have an **Edits** label. A product with variants shows the total stock of its variants; a product without variants shows its own stock.

Each subcategory card shows the number of products in the whole branch and a short status: `Empty`, `Nothing published`, `N out of stock` or `N live`.

### Work with the products

- The filters **All**, **Published**, **Unpublished** and **Out of stock**, and the sort menu, apply to the products directly in the category.
- The search box in Umbraco's collection toolbar filters them by name, title or SKU.
- The checkboxes and **Select all** use Umbraco's own collection selection, so the normal selection bar and bulk actions work.
- The `...` button on a card opens Umbraco's normal document actions.
- The view has its own light and dark colors and follows the theme chosen in the backoffice user menu.

When you open an `ekmCatalog` or `ekmCategory` node whose collection has the Catalog layout, the backoffice opens on the Catalog view. If you switch to another view, your browser remembers that choice and opens that view next time.

### Set up the Catalog view

New installs create a data type called **Ekom Catalog Collection**, with Catalog as the first layout and Table as the second, and assign it to the Category document type.

Ekom does not change an existing install. To get the view there, assign a collection to the Category document type yourself and add the Catalog layout (`Ekom.CollectionView.Catalog`).

Keep at least one of Umbraco's own views, such as `Umb.CollectionView.Document.Table`, in the layouts of the collection data type next to Catalog. The reason is in the next section.

### The Catalog view in content pickers

Umbraco can show a node's collection inside a document picker. The Catalog view does not appear in pickers: its condition `Ekom.Condition.CatalogPicker` hides it there. A collection set up with only the Catalog layout would then have no view in the picker, which is why you keep a native view next to it.

Ekom treats a collection as a picker when its configuration has `selectionConfiguration.selectOnly: true`. Selecting items in a normal workspace does not count as a picker, so it does not hide the Catalog view.

To show the Catalog view in pickers too, change the condition on `Ekom.CollectionView.Catalog` in `Ekom/Ekom.Web/Ekom.Web.U17/Client/src/manifests.ts`:

```typescript
{
  alias: 'Ekom.Condition.CatalogPicker',
  allowInPickers: true,
},
```

Then rebuild the web asset projects for Umbraco 17, 18 and 19. All three build from the same client source in `Ekom.Web.U17/Client/src`.

In a picker, the Catalog view shows Umbraco's own document table. Umbraco keeps control of the browsed category, the allowed items, single or multiple selection, paging and navigation. Ekom does not load its catalog data, change the browser history, force multiple selection or hide paging there. The cards, sorting, breadcrumbs and navigation of the Catalog view appear only in normal workspace collections.

## Related pages

- [Stores](stores.md): pick the right store for each catalog call.
- [Stock](stock.md): how Ekom decides that a product is available.
- [Variant App](variant-app.md): manage the variants of a product in one view.
- [Database price storage](database-price-storage.md): keep prices in a database table instead of on the product nodes.
- [Catalog imports](imports.md): create and update the catalog from another system.
- [HTTP API reference](../reference/http-api.md#catalog): all catalog endpoints.

[Back to Guides](README.md)

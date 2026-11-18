# Use your own product, variant and category classes

[Ekom docs](../README.md) / [Examples](README.md) / Use your own product, variant and category classes

Ekom builds its catalog objects through factories. Register your own factory and Ekom creates your subclass instead of its own `Product`, `Variant`, `Category` or `Store`. The examples on this page add typed properties from the Umbraco node, give signed-in members their own prices, and add settings to a store.

On this page:

- [How Ekom creates catalog objects](#how-ekom-creates-catalog-objects)
- [Add typed properties to products](#add-typed-properties-to-products)
- [Give signed-in members their own prices](#give-signed-in-members-their-own-prices)
- [Add a computed property to categories](#add-a-computed-property-to-categories)
- [Add your own settings to a store](#add-your-own-settings-to-a-store)

## How Ekom creates catalog objects

Read this once before you copy an example. The rules apply to every class on this page.

### The factories you can replace

| Object | Factory interface | Base class constructor to call |
| --- | --- | --- |
| Product | `IPerStoreFactory<IProduct>` | `Product(UmbracoContent item, IStore store)`, protected internal |
| Variant | `IPerStoreFactory<IVariant>` | `Variant(UmbracoContent item, IStore store)`, public |
| Variant group | `IPerStoreFactory<IVariantGroup>` | `VariantGroup(UmbracoContent node, IStore store)`, public |
| Category | `IPerStoreFactory<ICategory>` | `Category(UmbracoContent item, IStore store)`, protected internal |
| Discount | `IPerStoreFactory<IDiscount>` | `Discount(UmbracoContent item, IStore store)`, public |
| Product discount | `IPerStoreFactory<IProductDiscount>` | `ProductDiscount(UmbracoContent content, IStore store)`, public |
| Shipping provider | `IPerStoreFactory<IShippingProvider>` | `ShippingProvider(UmbracoContent item, IStore store)`, protected internal |
| Payment provider | `IPerStoreFactory<Ekom.Models.IPaymentProvider>` | `PaymentProvider(UmbracoContent item, IStore store)`, protected internal |
| Store | `IObjectFactory<IStore>` | `Store(UmbracoContent item)`, protected internal |
| Zone | `IObjectFactory<IZone>` | `Zone(UmbracoContent item)`, protected internal |

The interfaces are in `Ekom.Interfaces`; the classes are in `Ekom.Models`. "Protected internal" means your subclass can call the constructor, but other code cannot. Each class also has a constructor that takes only the store (or nothing, for `Store` and `Zone`). It leaves the node's properties empty, so do not use it in a factory.

Write `Ekom.Models.IPaymentProvider` in full. `Ekom.Payments` has an interface with the same name.

### Which members you can override

Your class inherits everything. You add new properties, and you override only members that are `virtual`.

| Class | Virtual members | Not virtual |
| --- | --- | --- |
| `Product` | `Prices`, `OriginalPrice`, `Vat`, `DisableDiscounts`, `Backorder`, `Available`, `Stock`, `Description`, `Summary`, `Images`, `Metafields`, `Url`, `Urls`, `UrlsWithContext`, `Categories`, `CategoryAncestors`, `CategoriesIds`, `VariantGroups`, `AllVariants`, `PrimaryVariantGroup`, `PrimaryVariant`, `ProductDiscount(...)`, `ProductDiscountAsync(...)` | `SKU`, `Slug`, `Price`, `StockBuffer`, `AllVariantsCount`, `RelatedProducts(...)`, `RelatedProductsAsync(...)` |
| `Variant` | `Prices`, `OriginalPrice`, `Vat`, `Backorder`, `Available`, `Stock`, `Description`, `Images`, `Product`, `Categories()`, `ProductDiscountAsync(...)` | `SKU`, `Price`, `ProductId`, `VariantGroup` |
| `Category` | `Url`, `VirtualUrl`, `HasProducts()` | `Slug`, `SubCategories`, `Products(...)`, `ProductsRecursive(...)`, `Ancestors`, `RootCategory` |
| `Store` | `Alias`, `Currency`, `Currencies`, `Vat`, `VatIncludedInPrice`, `Culture`, `Cultures`, `Domains`, `StoreRootNode`, `StoreRootNodeId`, `Url`, `UrlPrefix(...)`, `OrderNumberTemplate`, `OrderNumberPrefix`, `UserBasket`, `ShareBasketBetweenStores`, `ApplyVatOnShipping` | |

All of them also inherit `Title`, `Key`, `ParentKey`, `ParentId`, `SortOrder`, `Level`, `CreateDate`, `UpdateDate` and `ContentTypeAlias` as virtual members. The per-store classes (all except `Store` and `Zone`) also have a virtual `GetValue(alias, storeAlias)`. `Price` is not virtual, but it reads `Prices`, so an override of `Prices` changes `Price` too.

### When the factory runs and how long an object lives

Ekom keeps one object per node and per store in memory. Your factory is called:

- for every catalog node, once per store, when the site starts and when the caches are rebuilt (`Store.RefreshCache()` or the rebuild in the backoffice),
- once per store when an editor publishes the node,
- for every per-store object of a store when that store node is published,
- for the catalog nodes below and above a category when its `slug` or `disable` value changes.

Between those moments every request reads the same object. A product can stay in memory for weeks. So:

- Read values that depend on the request, the customer or the time when the property is read, not in the constructor.
- Parse values that only depend on the node once, in the constructor.
- The base constructor runs before yours and reads some virtual members (`Product` reads `Vat`, `Categories` and `GetValue`; `Variant` reads `Product` and `Vat`). An override of those runs before the fields of your class are set.
- Other catalog objects that your object holds (`Categories`, `CategoryAncestors`, a category's `Ancestors` and `RootCategory`) are the objects that were in the cache when yours was built. When another node is published, yours keeps the old one. To show current data from another node, look it up through `Catalog` when you read it.

If your constructor throws during a full build at startup or a refresh, the whole build fails and Ekom logs `Ekom cache initialization failed`. If it throws when one node is published, Ekom logs a warning and the store keeps the object it had. Use `TryParse` and defaults so that bad content cannot throw.

### Register your factory so that it wins

Ekom registers its own factories with `AddSingleton` inside `EkomComposer`, which Umbraco runs inside the `.Build()` call on the Umbraco builder. ASP.NET Core injects the last registration of a service, so register yours in `Program.cs` after that call:

```csharp
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

// After the Umbraco builder: Ekom's composer has run, so this registration is the last one and wins.
builder.Services.AddSingleton<IPerStoreFactory<IProduct>, ShopProductFactory>();

WebApplication app = builder.Build();
```

A registration placed before the Umbraco builder loses to Ekom's. In a class library that cannot change `Program.cs`, use a composer that runs after Ekom's:

```csharp
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Umb;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace MyShop.Commerce;

// For a class library that cannot touch Program.cs.
[ComposeAfter(typeof(EkomComposer))]
public sealed class CatalogComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.Services.AddSingleton<IPerStoreFactory<IProduct>, ShopProductFactory>();
}
```

Register factories as singletons. Ekom's caches are singletons and keep the factory they were given for the life of the application, so a factory can only depend on singleton services. Ekom uses one factory per object type: if you need typed properties and member prices, put both in one product class.

## Add typed properties to products

This product class reads a brand, a size guide link and a "new until" date from the product node. Add the properties `brand` (text, or Ekom's per-store text editor), `sizeGuideUrl` (text) and `newUntil` (a date picker) to the `ekmProduct` document type.

```csharp
using System.Globalization;
using Ekom.Interfaces;
using Ekom.Models;

namespace MyShop.Commerce;

public sealed class ShopProductFactory : IPerStoreFactory<IProduct>
{
    public IProduct Create(UmbracoContent item, IStore store) => new ShopProduct(item, store);
}

public sealed class ShopProduct : Product
{
    public ShopProduct(UmbracoContent item, IStore store) : base(item, store)
    {
        // The node does not change while this object is cached, so parse its values once.
        Brand = GetValue("brand", Store.Alias);

        string sizeGuideUrl = GetValue("sizeGuideUrl");
        SizeGuideUrl = string.IsNullOrWhiteSpace(sizeGuideUrl) ? null : sizeGuideUrl;

        NewUntil = DateTime.TryParse(
            GetValue("newUntil"),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal,
            out DateTime newUntil)
            ? newUntil
            : null;
    }

    /// <summary>Brand name. Can differ per store.</summary>
    public string Brand { get; }

    /// <summary>Link to the size guide, or null when the editor left it empty.</summary>
    public string? SizeGuideUrl { get; }

    /// <summary>The product shows a "New" badge until this time, in UTC.</summary>
    public DateTime? NewUntil { get; }

    // Compare with the clock when the value is read: the object can stay cached for weeks.
    public bool IsNew => NewUntil is DateTime until && DateTime.UtcNow < until;
}
```

Register the factory in `Program.cs` as shown in [Register your factory so that it wins](#register-your-factory-so-that-it-wins):

```csharp
builder.Services.AddSingleton<IPerStoreFactory<IProduct>, ShopProductFactory>();
```

`Catalog` returns `IProduct`. Cast it to your class to read the new properties. In a Razor view, with `@using MyShop.Commerce` and `@inject Catalog _catalog` in `_ViewImports.cshtml`:

```cshtml
@{
    var product = await _catalog.GetProductAsync() as ShopProduct;
    if (product == null)
    {
        return;
    }
}

<p class="brand">@product.Brand</p>

@if (product.IsNew)
{
    <span class="badge">New</span>
}

@if (product.SizeGuideUrl != null)
{
    <a href="@product.SizeGuideUrl">Size guide</a>
}
```

Ekom's HTTP routes for one product (`GET /ekom/catalog/product/...`) write the whole object, so `brand`, `sizeGuideUrl`, `newUntil` and `isNew` appear in the JSON. Routes that return a list write `ProductResponse.Products`, which is typed `IEnumerable<IProduct>`. ASP.NET Core's default JSON output (System.Text.Json) writes only the members of `IProduct` for each item, so your properties are not in lists. Ekom does not set up another JSON serializer for these routes. To send your properties in a list, return your own model from your own controller:

```csharp
using Ekom.API;
using Ekom.Models;
using Microsoft.AspNetCore.Mvc;

namespace MyShop.Commerce;

public sealed record ProductCard(
    string Sku,
    string Title,
    string Brand,
    bool IsNew,
    string Price,
    string Url);

[ApiController]
[Route("api/shop/product-cards")]
public sealed class ProductCardsController(Catalog catalog) : ControllerBase
{
    [HttpGet("{categoryId:int}")]
    public async Task<ActionResult<IReadOnlyList<ProductCard>>> GetAsync(int categoryId, CancellationToken ct)
    {
        ICategory? category = await catalog.GetCategoryAsync(categoryId, "Store", ct: ct);
        if (category == null) return NotFound();

        ProductResponse response = await category.ProductsRecursiveAsync(
            new ProductQuery { Page = 1, PageSize = 24 },
            ct);

        var cards = new List<ProductCard>();
        foreach (IProduct product in response.Products)
        {
            // Every product is a ShopProduct while ShopProductFactory is registered.
            if (product is not ShopProduct shopProduct) continue;

            cards.Add(new ProductCard(
                shopProduct.SKU,
                shopProduct.Title,
                shopProduct.Brand,
                shopProduct.IsNew,
                shopProduct.Price.WithVat.CurrencyString,
                shopProduct.Url));
        }

        return cards;
    }
}
```

What to know:

- `Catalog` returns the cached object itself, so the cast works for every lookup: by key, ID, SKU, route and the current page. The cast gives `null` only if a handler of `CatalogEvents.BeforeReturnProduct` or `BeforeReturnProductAsync` replaced the product with an object of another type.
- Date pickers are stored as text in the round-trip format. A date without a time zone is read as server local time and converted to UTC. See [Custom date and time properties](../reference/models-and-behavior.md#custom-date-and-time-properties).
- `IsNew` compares with the clock each time it is read, so the badge goes away on time without a republish. Computing it in the constructor would freeze it at the value it had when the cache was built.
- To keep a public property out of the JSON, mark it `[System.Text.Json.Serialization.JsonIgnore]`.
- Order lines do not keep your object. They keep a copy of the node's properties and prices. Read the brand of a line with `line.Product.Properties.GetPropertyValue("brand", order.StoreInfo.Alias)` (`using Ekom.Utilities;`).

## Give signed-in members their own prices

This is the pattern the Umbraco 17 sample site uses in `EkomIntegration/MemberProduct.cs`: signed-in members get 20% off unless the product already has a lower sale price. Products with discounts turned off keep their price. Both the product and the variant class are replaced, because a variant has its own prices.

```csharp
using Ekom.Interfaces;
using Ekom.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Security;

namespace MyShop.Commerce;

public sealed class MemberProductFactory(IHttpContextAccessor httpContextAccessor) : IPerStoreFactory<IProduct>
{
    public IProduct Create(UmbracoContent item, IStore store)
        => new MemberProduct(item, store, httpContextAccessor);
}

public sealed class MemberVariantFactory(IHttpContextAccessor httpContextAccessor) : IPerStoreFactory<IVariant>
{
    public IVariant Create(UmbracoContent item, IStore store)
        => new MemberVariant(item, store, httpContextAccessor);
}

public sealed class MemberProduct(UmbracoContent item, IStore store, IHttpContextAccessor httpContextAccessor)
    : Product(item, store)
{
    public override List<IPrice> Prices => DisableDiscounts
        ? base.Prices
        : MemberPricing.Apply(base.Prices, Vat, Store.VatIncludedInPrice, httpContextAccessor);
}

public sealed class MemberVariant(UmbracoContent item, IStore store, IHttpContextAccessor httpContextAccessor)
    : Variant(item, store)
{
    public override List<IPrice> Prices => Product?.DisableDiscounts == true
        ? base.Prices
        : MemberPricing.Apply(base.Prices, Vat, Store.VatIncludedInPrice, httpContextAccessor);
}

internal static class MemberPricing
{
    // Shown on the price as its discount. Use your own fixed key.
    private static readonly OrderedDiscount MemberDiscount = new(
        new Guid("c1048d39-3db1-45dd-bf4f-68515d09d131"),
        "Member price",
        false,
        0.20m,
        DiscountType.Percentage,
        new List<string>(),
        new List<string>(),
        null,
        false,
        false);

    internal static List<IPrice> Apply(
        List<IPrice> prices,
        decimal vat,
        bool vatIncludedInPrice,
        IHttpContextAccessor httpContextAccessor)
    {
        // Ask the current request who the customer is. No request (a background job) means no member price.
        IMemberManager? memberManager = httpContextAccessor.HttpContext?.RequestServices.GetService<IMemberManager>();
        if (memberManager?.IsLoggedIn() != true) return prices;

        // base.Prices is shared by every request: build a new list, never change the one you were given.
        var result = new List<IPrice>(prices.Count);
        foreach (IPrice price in prices)
        {
            if (price.OriginalValue <= 0)
            {
                result.Add(price);
                continue;
            }

            var memberPrice = new Price(price.OriginalValue, price.Currency, vat, vatIncludedInPrice, MemberDiscount);

            // Keep a better catalog discount if there is one.
            result.Add(memberPrice.Value < price.Value ? memberPrice : price);
        }

        return result;
    }
}
```

Register both factories in `Program.cs`, after the Umbraco builder:

```csharp
builder.Services.AddSingleton<IPerStoreFactory<IProduct>, MemberProductFactory>();
builder.Services.AddSingleton<IPerStoreFactory<IVariant>, MemberVariantFactory>();
```

Why the member is looked up when the price is read: the factory runs when the cache is built, often at startup when there is no request and no member at all. The object it creates is then shared by every visitor until the product is published again. A member check in the constructor would freeze one answer for everybody. `Prices` is read in each request, so it can ask that request who is signed in.

What to know:

- `IMemberManager` is a scoped service. Take it from `HttpContext.RequestServices` when you need it. Do not inject it, or any other scoped service, into the factory or the product: they live as long as the application.
- `Prices` is read often: by every read of `Price`, by product lists that sort or filter on price, and when a line is added to the basket. Keep the override cheap. Do not query the database in it.
- Do not change the list that `base.Prices` returns. Ekom caches it and hands the same list to every request.
- Ekom treats the member price as a discount: it is `Price.Discount` on the product, and it becomes the order line's discount when the product is added to the basket.
- A basket line keeps the prices it had when it was added. When a guest signs in, call `Order.ReInitializeOrder(storeAlias)` in a later request to price the basket again, for example on the page the sign-in redirects to. In the request that signs the member in, the request still belongs to a guest. See [Refresh prices after a customer signs in](../guides/orders-and-baskets.md#refresh-prices-after-a-customer-signs-in).
- Code without a request, such as a product feed built by a background job, gets guest prices.
- If you also use `ShopProduct` from the previous example, put this `Prices` override in `ShopProduct`. Ekom uses only the last registered product factory.

## Add a computed property to categories

This category class adds a campaign banner with an end date and a number of columns for the product grid. Add `bannerText`, `bannerUntil` and `productColumns` to the `ekmCategory` document type.

```csharp
using System.Globalization;
using Ekom.Interfaces;
using Ekom.Models;

namespace MyShop.Commerce;

public sealed class ShopCategoryFactory : IPerStoreFactory<ICategory>
{
    public ICategory Create(UmbracoContent item, IStore store) => new ShopCategory(item, store);
}

public sealed class ShopCategory : Category
{
    public ShopCategory(UmbracoContent item, IStore store) : base(item, store)
    {
        BannerText = GetValue("bannerText", Store.Alias);

        BannerUntil = DateTime.TryParse(
            GetValue("bannerUntil"),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal,
            out DateTime bannerUntil)
            ? bannerUntil
            : null;

        // Columns in the product grid: 2 to 4, default 3.
        ProductColumns = int.TryParse(GetValue("productColumns"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int columns)
            ? Math.Clamp(columns, 2, 4)
            : 3;
    }

    public string BannerText { get; }

    public DateTime? BannerUntil { get; }

    public int ProductColumns { get; }

    // Computed when read, from values parsed once in the constructor. Cheap enough for every category in a menu.
    public bool ShowBanner =>
        !string.IsNullOrWhiteSpace(BannerText)
        && (BannerUntil is not DateTime until || DateTime.UtcNow < until);
}
```

Register it in `Program.cs`, after the Umbraco builder:

```csharp
builder.Services.AddSingleton<IPerStoreFactory<ICategory>, ShopCategoryFactory>();
```

In a category view, cast the category: `var category = await _catalog.GetCategoryAsync() as ShopCategory;`.

What to know:

- Keep computed properties cheap. Menus and breadcrumbs read them for many categories in one request. Do not read `Products(...)` or `ProductsRecursive(...)` in a property: each call runs a full product query, with the filter service and catalog events.
- `Ancestors` and `RootCategory` are the category objects that were cached when this category was built. If an editor renames a parent category, this category keeps the old parent object until it is built again. For a breadcrumb with current titles, look up each ancestor through `Catalog` by ID when you render it.
- The `Category` constructor already opens a dependency injection scope and reads the ancestors from Umbraco. Do not add more lookups of that kind to yours: it runs for every category in every store when the caches are built.

## Add your own settings to a store

A store class is useful for settings that every page needs, such as a support email address and a return period. Add `supportEmail` and `returnDays` to the `ekmStore` document type.

```csharp
using System.Globalization;
using Ekom.Interfaces;
using Ekom.Models;

namespace MyShop.Commerce;

public sealed class ShopStoreFactory : IObjectFactory<IStore>
{
    // Ekom casts stores to Ekom.Models.Store, so always return a subclass of Store.
    public IStore Create(UmbracoContent item) => new ShopStore(item);
}

public sealed class ShopStore : Store
{
    public ShopStore(UmbracoContent item) : base(item)
    {
        string supportEmail = GetValue("supportEmail");
        SupportEmail = string.IsNullOrWhiteSpace(supportEmail) ? null : supportEmail;

        ReturnDays = int.TryParse(GetValue("returnDays"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int days)
            ? days
            : 14;
    }

    /// <summary>Shown in the footer and on the receipt. Null when the editor left it empty.</summary>
    public string? SupportEmail { get; }

    /// <summary>Days the customer has to return an item. Default 14.</summary>
    public int ReturnDays { get; }
}
```

Register it in `Program.cs`, after the Umbraco builder:

```csharp
builder.Services.AddSingleton<IObjectFactory<IStore>, ShopStoreFactory>();
```

Read it from `Ekom.API.Store`, for example `stores.GetStore("Store") as ShopStore`, or from any catalog object: `product.Store as ShopStore`.

What to know:

- Return a subclass of `Ekom.Models.Store`. Ekom casts stores to `Store` when a store node is published and whenever a catalog node is published; any other `IStore` implementation fails there.
- Publishing a store node creates every product, variant, category, discount and provider of that store again, through their factories. On a large catalog that takes time. Put settings that editors change often somewhere else.
- `IStore` has no member for your settings, and the `Store` property of catalog objects is not included in JSON. Orders keep their own copy of the store's details (`StoreInfo`), not your store object.

## Related pages

- [Catalog and products](../guides/catalog-and-products.md): how to find and list products, and the catalog events that can change what is returned.
- [Orders and baskets](../guides/orders-and-baskets.md#refresh-prices-after-a-customer-signs-in): refresh basket prices after a customer signs in.
- [Stores](../guides/stores.md): how Ekom finds the store for a request, and how to refresh the caches.
- [Models and behavior](../reference/models-and-behavior.md): every member of `IProduct`, `ICategory`, `IVariant` and `IStore`.
- [Services and dependency injection](../reference/services-and-dependency-injection.md): how Ekom is registered and which services you can replace.
- [Replace Ekom services](replace-services.md): change filtering, search, stock rules and email without subclassing catalog objects.

[Back to Examples](README.md)

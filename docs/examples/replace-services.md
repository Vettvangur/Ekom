[Ekom docs](../README.md) / [Examples](README.md) / Replace Ekom services

# Replace Ekom services

Some of Ekom's behavior sits behind a service that you can replace with your own class through dependency injection. The examples on this page change which order lines need stock, hide products from guests, search the catalog without a search index, and send Ekom's alert emails through Umbraco.

On this page:

- [Which services you can replace](#which-services-you-can-replace)
- [Let only wholesale customers order beyond stock](#let-only-wholesale-customers-order-beyond-stock)
- [Hide members-only products from guests](#hide-members-only-products-from-guests)
- [Search the catalog without a search index](#search-the-catalog-without-a-search-index)
- [Send Ekom's alert emails through Umbraco](#send-ekoms-alert-emails-through-umbraco)

## Which services you can replace

| Service | Ekom's default | How Ekom registers it | Example |
| --- | --- | --- | --- |
| `ICheckoutStockPolicy` | A line needs stock unless its product has backorder turned on. | `TryAddSingleton` | [Wholesale backorder](#let-only-wholesale-customers-order-beyond-stock) |
| `IProductFilterService` | `ProductFilterService`, which returns the products unchanged. | `AddTransient` | [Members-only products](#hide-members-only-products-from-guests) |
| `ICatalogSearchService` | Examine on Umbraco 17 and 18; Umbraco's search provider on Umbraco 19. | `AddScoped` | [Search without an index](#search-the-catalog-without-a-search-index) |
| `IMailService` | Sends through its own SMTP client, with the `Umbraco:CMS:Global:Smtp` settings. | `AddTransient` | [Alert emails](#send-ekoms-alert-emails-through-umbraco) |

Two more interfaces are extension points that you add to rather than replace: `IGiftcardValidator` (none is registered; registering one turns on gift cards) and `IOrderManagerActionProvider` (add one registration per provider). See [Services you can replace](../reference/services-and-dependency-injection.md#services-you-can-replace).

### Register your class so that it wins

Ekom registers its services inside `EkomComposer`, which Umbraco runs inside the `.Build()` call on the Umbraco builder. ASP.NET Core injects the last registration of a service, so register yours in `Program.cs` after that call:

```csharp
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

// Ekom's composer ran inside Build() above. Registrations from here on come last and win.
builder.Services.AddSingleton<ICheckoutStockPolicy, WholesaleBackorderPolicy>();

WebApplication app = builder.Build();
```

A registration placed before the Umbraco builder loses to Ekom's, except for `ICheckoutStockPolicy`, which Ekom adds only when none is registered. In a class library, use a composer marked `[ComposeAfter(typeof(EkomComposer))]` instead; [Use your own search](../guides/catalog-and-products.md#use-your-own-search) shows one. Keep the lifetime in the table above.

### What you cannot replace

Ekom's own implementations are `internal`: `DefaultCheckoutStockPolicy`, the Examine and Umbraco search services, and `MailService`. You cannot inherit from them or wrap them by type; write a new implementation of the interface. `ProductFilterService` is the one public default you can inherit from.

The order, checkout, stock and cache services behind the API (`OrderService`, `CheckoutService`, `CheckoutReservationService`, the catalog caches and the repositories) are internal too, and so are the constructors of `Ekom.API.Catalog`, `Order`, `Stock` and the other API classes. Do not try to replace them. To change what they do, use [events](events.md) or the services above.

## Let only wholesale customers order beyond stock

Products with backorder turned on can be ordered even when they are out of stock. This policy keeps that for members of the `Wholesale` member group and requires stock for everyone else.

```csharp
using Ekom.Models;
using Ekom.Services;
using UmbracoMemberService = Umbraco.Cms.Core.Services.IMemberService;

namespace MyShop.Commerce;

public sealed class WholesaleBackorderPolicy(UmbracoMemberService memberService) : ICheckoutStockPolicy
{
    private const string WholesaleGroup = "Wholesale";

    public bool RequiresStock(IOrderInfo order, IOrderLine line)
    {
        // Products without backorder always need stock, as in Ekom's default policy.
        if (!line.Product.Backorder) return true;

        // Backorder products: only wholesale customers may order more than is in stock.
        return !IsWholesale(order);
    }

    private bool IsWholesale(IOrderInfo order)
    {
        // Ekom saves the signed-in member's username on the order, so this also works
        // when completion runs from a payment callback without the customer's request.
        string? username = order.CustomerInformation.Customer.UserName;
        if (string.IsNullOrWhiteSpace(username)) return false;

        return memberService.GetAllRoles(username).Contains(WholesaleGroup, StringComparer.OrdinalIgnoreCase);
    }
}
```

Register it in `Program.cs`:

```csharp
builder.Services.AddSingleton<ICheckoutStockPolicy, WholesaleBackorderPolicy>();
```

What to know:

- Ekom calls the policy for each line when the customer submits payment, and again when the paid order is completed. Completion can run from a payment provider's callback, without the customer's request or session. Base the decision on data saved with the order, as this example does with the username. `UmbracoMemberService` is an alias for Umbraco's `IMemberService`, because `Ekom.Services` has an `IMemberService` of its own.
- Do not use order custom data for this decision. The customer can set custom data through Ekom's order endpoints. See [Do not trust custom data](../guides/order-custom-data.md#do-not-trust-custom-data).
- A line for which the policy returns `false` is not checked, held or taken from stock at checkout.
- Ekom asks the policy every time stock is checked for a line: when the line is added to the basket, when its quantity changes, when checkout is prepared, and when the order is completed. Here a retail customer is refused as soon as they add more of a backorder product than is in stock. `IProduct.Available` is `true` for every backorder product, so show retail customers availability from stock in your views.
- The basket of a customer who has not signed in has no user name yet, so this policy treats them as retail. Decide what your own policy answers for a customer the order does not identify.
- `GetAllRoles` reads the database. Ekom calls the policy once per line at each of the two steps, so a large wholesale basket makes several lookups.
- The policy is a singleton. Do not inject scoped services such as `IMemberManager` into it.

## Hide members-only products from guests

The filter service runs on every product list Ekom builds. This one removes products whose `membersOnly` toggle is on when no member is signed in. Add a `membersOnly` toggle to the `ekmProduct` document type.

```csharp
using Ekom.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Security;

namespace MyShop.Commerce;

// ProductFilterService is Ekom's default implementation. Override the overload you need.
public sealed class MembersOnlyFilterService(IHttpContextAccessor httpContextAccessor) : ProductFilterService
{
    // Ekom's store-alias overloads and the synchronous overload all end up here.
    public override Task<IEnumerable<IProduct>> ApplyFiltersAsync(
        IEnumerable<IProduct> products,
        ProductQuery? query = null,
        ICategory? category = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (IsMemberSignedIn()) return Task.FromResult(products);

        var visible = new List<IProduct>();
        foreach (IProduct product in products)
        {
            if (!IsTrue(product.GetValue("membersOnly")))
            {
                visible.Add(product);
            }
        }

        return Task.FromResult<IEnumerable<IProduct>>(visible);
    }

    private bool IsMemberSignedIn()
    {
        // IMemberManager is scoped: take it from the current request, not from the constructor.
        IMemberManager? memberManager = httpContextAccessor.HttpContext?.RequestServices.GetService<IMemberManager>();
        return memberManager?.IsLoggedIn() == true;
    }

    // An Umbraco toggle is stored as "1" or "0".
    private static bool IsTrue(string value)
        => value == "1" || (bool.TryParse(value, out bool result) && result);
}
```

Register it in `Program.cs`:

```csharp
builder.Services.AddTransient<IProductFilterService, MembersOnlyFilterService>();
```

`ProductFilterService` is declared without a namespace, so it needs no `using`.

What to know:

- The filter runs on lists: category `Products` and `ProductsRecursive`, `GetAllProducts`, lookups by IDs, keys and SKUs, search results, and related products filled from a category. It does not run on single lookups such as `GetProduct()` on a product page, so a members-only product still opens by its URL. Check it in the product view as well, or in a `CatalogEvents.BeforeReturnProduct` handler.
- It does not run when `ProductQuery.RaiseEvents` is `false`.
- The filter runs after the query's own filters and search, and before sorting and paging, so `TotalProductCount` and the pages leave hidden products out. The metafield filters in `ProductResponse.Filters` are built before your filter runs, so their counts still include hidden products.
- Override the overload without a store alias, as here, when the store does not matter. Ekom calls the store-alias overload when it knows the store, and the base class passes that call on to this one. Override the store-alias overload when your rule depends on the store. See [Filter product lists](../reference/services-and-dependency-injection.md#filter-product-lists).
- Categories resolve the filter service from the application's root service provider. Give it only singleton dependencies, and take request services from `HttpContext.RequestServices`, as `IsMemberSignedIn` does.

## Search the catalog without a search index

This search service finds products in Ekom's cached catalog instead of Examine or another index: an exact SKU match first, then products whose title contains every word of the query. It fits a catalog of a few thousand products, and gives the same results on every server without an index to rebuild.

```csharp
using Ekom.API;
using Ekom.Models;
using Ekom.Services;

namespace MyShop.Commerce;

// Searches the cached catalog instead of a search index. Fits a catalog of a few thousand products.
public sealed class SkuAndTitleSearchService(Catalog catalog) : ICatalogSearchService
{
    // Catalog.ProductSearchAsync and ProductQuery.SearchQuery call this one.
    public async Task<(IEnumerable<int> Ids, long Total)> ProductQueryAsync(
        SearchRequest req,
        CancellationToken ct = default)
    {
        List<IProduct> hits = await FindAsync(req, ct).ConfigureAwait(false);

        var ids = new List<int>();
        foreach (IProduct product in Page(hits, req))
        {
            ids.Add(product.Id);
        }

        return (ids, hits.Count);
    }

    public async Task<(IEnumerable<SearchResultEntity> Results, long Total)> PublicQueryAsync(
        SearchRequest req,
        CancellationToken ct = default)
    {
        List<IProduct> hits = await FindAsync(req, ct).ConfigureAwait(false);

        var results = new List<SearchResultEntity>();
        foreach (IProduct product in Page(hits, req))
        {
            results.Add(new SearchResultEntity
            {
                Id = product.Id,
                Key = product.Key,
                Name = product.Title,
                SKU = product.SKU,
                Url = product.Url,
                DocType = "ekmProduct",
                ParentId = product.Id,
            });
        }

        return (results, hits.Count);
    }

    public Task<(IEnumerable<SearchResultEntity> Results, long Total)> InternalQueryAsync(
        SearchRequest req,
        CancellationToken ct = default)
        => PublicQueryAsync(req, ct);

    private async Task<List<IProduct>> FindAsync(SearchRequest req, CancellationToken ct)
    {
        string[] words = req.SearchQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0) return [];

        // RaiseEvents = false: no filter service or catalog events here. Ekom runs them on the products it returns.
        var everything = new ProductQuery { RaiseEvents = false };
        ProductResponse all = string.IsNullOrWhiteSpace(req.StoreAlias)
            ? await catalog.GetAllProductsAsync(everything, ct).ConfigureAwait(false)
            : await catalog.GetAllProductsAsync(req.StoreAlias, everything, ct).ConfigureAwait(false);

        var exactSku = new List<IProduct>();
        var titleMatches = new List<IProduct>();

        foreach (IProduct product in all.Products)
        {
            ct.ThrowIfCancellationRequested();

            if (string.Equals(product.SKU, req.SearchQuery.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                exactSku.Add(product);
            }
            else if (words.All(word => product.Title.Contains(word, StringComparison.OrdinalIgnoreCase)))
            {
                titleMatches.Add(product);
            }
        }

        // An exact SKU match comes first.
        exactSku.AddRange(titleMatches);
        return exactSku;
    }

    // Ekom's own search services treat SearchRequest.Page as the number of hits to skip. Do the same.
    private static IEnumerable<IProduct> Page(List<IProduct> hits, SearchRequest req)
        => hits.Skip(req.Page ?? 0).Take(req.PageSize ?? int.MaxValue);
}
```

Register it in `Program.cs`:

```csharp
builder.Services.AddScoped<ICatalogSearchService, SkuAndTitleSearchService>();
```

What to know:

- On Umbraco 17, 18 and 19, Ekom calls only `ProductQueryAsync`: from `Catalog.ProductSearchAsync` (and `POST /ekom/catalog/productsearch`), and from product lists whose `ProductQuery` has a `SearchQuery`. Implement `PublicQueryAsync` and `InternalQueryAsync` too, because they are part of the interface.
- Return product node IDs. Ekom loads the products by ID from the store's catalog, so IDs of variants and categories are left out. `Catalog.ProductSearchAsync` keeps the order of your IDs unless the request has an `OrderBy`. In a `ProductQuery` list, the IDs only decide which products stay; the list keeps its own order.
- Replacing the service works the same way on Umbraco 17, 18 and 19. Only Ekom's default differs: Examine on 17 and 18, Umbraco's search provider on 19 (see [Search](../guides/catalog-and-products.md#search)). With your own service, `SearchFields` and `ExamineIndex` on the request mean whatever your code makes of them; this example ignores them.
- Ekom creates a new dependency injection scope for each search and resolves the service there. Scoped dependencies are new for each search, not the request's instances.
- `SearchRequest.StoreAlias` is empty when the search comes from a `ProductQuery`. The example then searches the store of the current request. Outside a request, set `StoreAlias`.
- For a large catalog, use a search engine instead. The service shape is the same: query the engine and return product IDs.

## Send Ekom's alert emails through Umbraco

Ekom sends an email when it cannot complete a paid order. Its default `MailService` uses its own SMTP client and throws when `Umbraco:CMS:Global:Smtp:Host` is empty. This replacement sends through Umbraco's `IEmailSender` and never throws.

```csharp
using Ekom;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;
using IEkomMailService = Ekom.Services.IMailService;

namespace MyShop.Commerce;

// Sends Ekom's emails through Umbraco's email sender, and never lets a mail failure
// hide the checkout error that Ekom is reporting.
public sealed class UmbracoMailService(
    IEmailSender emailSender,
    Configuration ekomConfiguration,
    ILogger<UmbracoMailService> logger) : IEkomMailService
{
    public async Task SendAsync(string subject, string body, string? recipient = null, string? sender = null)
    {
        // Same default as Ekom: the addresses in Ekom:EmailNotifications, comma separated.
        string? to = recipient ?? ekomConfiguration.EmailNotifications;
        if (string.IsNullOrWhiteSpace(to))
        {
            logger.LogWarning("Ekom email {Subject} was not sent: Ekom:EmailNotifications is empty.", subject);
            return;
        }

        try
        {
            var message = new EmailMessage(sender, to.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), null, null, null, subject, body, true, null);
            await emailSender.SendAsync(message, "EkomNotification").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Ekom sends this mail from a catch block and then rethrows the checkout error.
            // Throwing here would replace that error with a mail error.
            logger.LogError(ex, "Ekom email {Subject} could not be sent.", subject);
        }
    }
}
```

Register it in `Program.cs`:

```csharp
builder.Services.AddTransient<IMailService, UmbracoMailService>();
```

What to know:

- On Umbraco 17, 18 and 19, Ekom sends one kind of email through `IMailService`: the alert when completing a paid checkout fails. It goes to the addresses in `Ekom:EmailNotifications`. See [What completion does](../guides/checkout.md#what-completion-does).
- Ekom sends the alert inside a `catch` block and then rethrows the original error. If `SendAsync` throws, its exception replaces the original one, and the log and the caller see a mail error instead of the checkout error. That is why this class catches everything.
- `EkomNotification` is a label that Umbraco's email sender receives with the message. It has no meaning to Ekom.
- Ekom itself does not send order confirmations or other customer emails. Send those from an [order event](events.md).

## Related pages

- [Services and dependency injection](../reference/services-and-dependency-injection.md): every service you can inject or replace, with lifetimes.
- [Stock](../guides/stock.md): how stock and backorder decide what can be bought.
- [Catalog and products](../guides/catalog-and-products.md): product lists, `ProductQuery`, search and catalog events.
- [Checkout](../guides/checkout.md): when stock is checked, held and taken, and what happens when completion fails.
- [Use your own product, variant and category classes](custom-models.md): change catalog objects through factories instead of services.
- [React to events](events.md): run your own code at fixed points without replacing a service.

[Back to Examples](README.md)

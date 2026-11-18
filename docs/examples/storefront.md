# Build storefront pages

[Ekom docs](../README.md) / [Examples](README.md) / Build storefront pages

These examples build the pages every shop needs: a category page, a product page, the basket, checkout and the receipt. All but the last are for a server-rendered Umbraco site: Razor views for the pages and your own controllers for the forms. The last one makes the same basket and checkout calls with `fetch` from a headless storefront.

The views are the templates of your Umbraco document types. The controllers go in your site project. Each form posts to a controller, the controller calls Ekom's `Order` API and redirects back, and the page then shows the totals Ekom recalculated.

On this page:

- [List a category's products with sorting and paging](#list-a-categorys-products-with-sorting-and-paging)
- [Search and filter a product list](#search-and-filter-a-product-list)
- [Show a menu of categories](#show-a-menu-of-categories)
- [Show a sale price](#show-a-sale-price)
- [Show a product with its variants and an add-to-basket form](#show-a-product-with-its-variants-and-an-add-to-basket-form)
- [Show and change the basket](#show-and-change-the-basket)
- [Apply and remove a coupon](#apply-and-remove-a-coupon)
- [Take the customer through checkout](#take-the-customer-through-checkout)
- [Show the receipt after payment](#show-the-receipt-after-payment)
- [Call the basket and checkout from a headless storefront](#call-the-basket-and-checkout-from-a-headless-storefront)

## List a category's products with sorting and paging

This category template lists the products of the category and all its subcategories, 24 to a page, with price, discount and availability. A select box changes the sort order, and the paging links keep the filters and the sort order from the address.

This helper builds the paging links:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace MyShop.Storefront;

public static class PageLinks
{
    // The current address with one query value replaced. Filters, search and sorting stay as they are.
    public static string With(HttpRequest request, string key, string value)
    {
        var query = new Dictionary<string, StringValues>(
            QueryHelpers.ParseQuery(request.QueryString.Value),
            StringComparer.OrdinalIgnoreCase)
        {
            [key] = value,
        };

        return QueryHelpers.AddQueryString(request.Path.Value ?? "/", query);
    }
}
```

The template:

```cshtml
@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@using Ekom.API
@using Ekom.Models
@using MyShop.Storefront
@inject Catalog Catalog
@{
    ICategory? category = await Catalog.GetCategoryAsync();
    if (category is null)
    {
        return;
    }

    // Reads page (or p), orderby, q, filter_{alias} and property_{alias} from the query string.
    ProductResponse result = await category.ProductsRecursiveAsync(
        new ProductQuery(Context.Request.Query) { PageSize = 24 });

    string orderBy = Context.Request.Query["orderby"].ToString();
    int page = result.Page ?? 1;
    int pageCount = result.PageCount ?? 1;
}

<h1>@category.Title</h1>

<form method="get">
    @* Keep the filters and search when the sort order changes. Paging starts again at page 1. *@
    @foreach (var pair in Context.Request.Query)
    {
        if (pair.Key is "orderby" or "page" or "p")
        {
            continue;
        }

        foreach (string? value in pair.Value)
        {
            <input type="hidden" name="@pair.Key" value="@value" />
        }
    }

    <label for="orderby">Sort by</label>
    <select id="orderby" name="orderby" onchange="this.form.submit()">
        <option value="DateDesc" selected="@(orderBy == "DateDesc")">Newest first</option>
        <option value="PriceAsc" selected="@(orderBy == "PriceAsc")">Price, low to high</option>
        <option value="PriceDesc" selected="@(orderBy == "PriceDesc")">Price, high to low</option>
        <option value="TitleAsc" selected="@(orderBy == "TitleAsc")">Name, A to Z</option>
    </select>
    <noscript><button type="submit">Sort</button></noscript>
</form>

<p>@result.ProductCount products</p>

<ul>
    @foreach (IProduct product in result.Products)
    {
        IPrice price = product.Price;
        Image? image = product.Images.FirstOrDefault();

        <li>
            <a href="@product.Url">
                @if (image is not null)
                {
                    <img src="@image.Url" alt="" loading="lazy" />
                }
                @product.Title
            </a>

            @if (price.HasDiscount)
            {
                <s>@price.BeforeDiscount.CurrencyString</s>
            }
            <strong>@price.AfterDiscount.CurrencyString</strong>

            @if (!product.Available)
            {
                <span>Out of stock</span>
            }
        </li>
    }
</ul>

@if (pageCount > 1)
{
    <nav aria-label="Pages">
        @if (page > 1)
        {
            <a href="@PageLinks.With(Context.Request, "page", (page - 1).ToString())">Previous</a>
        }

        @for (int i = 1; i <= pageCount; i++)
        {
            if (i == page)
            {
                <span aria-current="page">@i</span>
            }
            else
            {
                <a href="@PageLinks.With(Context.Request, "page", i.ToString())">@i</a>
            }
        }

        @if (page < pageCount)
        {
            <a href="@PageLinks.With(Context.Request, "page", (page + 1).ToString())">Next</a>
        }
    </nav>
}
```

What to know:

- `GetCategoryAsync()` without arguments returns the category Ekom routed the request to. On any other page it returns `null`.
- `new ProductQuery(Context.Request.Query)` reads `page` (or `p`), `orderby`, `q`, `filter_{alias}` and `property_{alias}`. It does not set `PageSize`. Ekom pages the result only when both `Page` and `PageSize` are set, and only then are `Page` and `PageCount` filled in.
- An `orderby` value that is not an `OrderBy` name is ignored. Without one, Ekom sorts by `Ekom:DefaultProductOrderBy`, which is `DateDesc` when not set. Put the option for your default first, so the select box shows it when the address has no `orderby`.
- `ProductCount` is the number of products after filtering, on all pages together.
- `product.Price` is the price in the customer's currency: the one in the `EkomCurrency-{storeAlias}` cookie, otherwise the request culture's, otherwise the store's first currency.
- `BeforeDiscount`, `AfterDiscount` and `DiscountAmount` include VAT when the store's prices include VAT, and leave it out when they do not. `WithVat` and `WithoutVat` always do what their names say. Do not show `BeforeDiscount` next to `WithVat`: in a store whose prices exclude VAT, one has VAT and the other does not.
- `Available` follows the stock rules: backorder, variants and the stock buffer. See [How Ekom decides that a product is available](../guides/stock.md#how-ekom-decides-that-a-product-is-available).
- `ProductsRecursiveAsync` includes the products of every subcategory. Use `ProductsAsync` for the products directly in the category.
- The catalog is read from memory, so listing it on every request does not query the database.

## Search and filter a product list

This partial view adds a search box, a checkbox list for each filterable metafield, and a price range to the category page above. It is a plain `GET` form, so every choice ends up in the address, and the paging and sorting links of the category page keep it.

Save it as `Views/Partials/_ProductFilters.cshtml`:

```cshtml
@model Ekom.Models.ProductResponse
@using System.Globalization
@using Ekom.Models
@{
    var query = Context.Request.Query;
    string culture = CultureInfo.CurrentUICulture.Name;

    // A metafield value is a dictionary from culture to text. Use the customer's language when it is there.
    string Text(Dictionary<string, string> values)
        => values.TryGetValue(culture, out string? text) ? text : values.Values.FirstOrDefault() ?? "";
}

<form method="get">
    @* Keep the sort order. The page number is left out, so a new search starts on page 1. *@
    <input type="hidden" name="orderby" value="@query["orderby"]" />

    <label for="q">Search</label>
    <input type="search" id="q" name="q" value="@query["q"]" />

    @foreach (MetafieldGrouped filter in Model.Filters)
    {
        string name = "filter_" + filter.Field.Alias;
        var chosen = query[name];

        <fieldset>
            <legend>@Text(filter.Field.Title)</legend>

            @foreach (Dictionary<string, string> value in filter.Values)
            {
                string text = Text(value);

                <label>
                    <input type="checkbox" name="@name" value="@text" checked="@chosen.Contains(text)" />
                    @text
                </label>
            }
        </fieldset>
    }

    <fieldset>
        <legend>Price</legend>
        <label for="priceFrom">From</label>
        <input type="number" id="priceFrom" name="property_priceFrom" min="0" step="1" value="@query["property_priceFrom"]" />
        <label for="priceTo">To</label>
        <input type="number" id="priceTo" name="property_priceTo" min="0" step="1" value="@query["property_priceTo"]" />
    </fieldset>

    <button type="submit">Show products</button>
    <a href="@Context.Request.Path">Clear</a>
</form>
```

In the category template, ask for the filters of all products and render the partial above the list:

```cshtml
ProductResponse result = await category.ProductsRecursiveAsync(
    new ProductQuery(Context.Request.Query) { PageSize = 24, AllFiltersVisible = true });
```

```cshtml
@await Html.PartialAsync("_ProductFilters", result)
```

What to know:

- `new ProductQuery(Context.Request.Query)` does the filtering. It reads `q` into `SearchQuery`, every `filter_{alias}` into `MetaFilters`, and every `property_{alias}` into `PropertyFilters`. The form only has to use those names.
- `result.Filters` lists the metafields marked as filterable that the products have, each with its distinct values. With `AllFiltersVisible = true` it is built from the products before filtering, so a checkbox does not disappear when another one is ticked. Without it, the list shrinks to the values of the products left.
- Several ticked values of one metafield match products that have any of them. A product must match every metafield that has a ticked value. When the metafield has `AllConditionsMustMatch` on, a product must have all the ticked values.
- A metafield value matches on its text in any language, and the comparison is exact, including upper and lower case. Send the text as Ekom gave it to you, as the checkbox does.
- `property_priceFrom` and `property_priceTo` are built in. They compare with the product's `OriginalPrice`, which is the price before discounts. Ekom parses them with the culture of the request, so send whole numbers. `property_category` is also built in and takes category IDs. Any other `property_{alias}` filters on the product property with that alias.
- An input left empty is ignored, so an empty search box or price field does not filter anything.
- `q` keeps only the products that the search service finds. On Umbraco 17 and 18 that is Examine, and on Umbraco 19 Umbraco's search provider. When no search service is registered, a search returns no products. See [Search](../guides/catalog-and-products.md#search).
- To search the whole store and not one category, put the same form on a search page and call `Catalog.GetAllProductsAsync(new ProductQuery(Context.Request.Query) { PageSize = 24 })`.
- The values come from the address and are written back into the form. Razor encodes them, so do not wrap them in `Html.Raw`.

## Show a menu of categories

This partial view renders the store's categories as nested lists. It opens only the branch the customer is in, and marks the category being shown.

Save it as `Views/Partials/_CategoryMenu.cshtml` and render it from your layout with `@await Html.PartialAsync("_CategoryMenu")`:

```cshtml
@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@using Ekom.API
@using Ekom.Models
@using Ekom.Utilities
@inject Catalog Catalog
@{
    IReadOnlyList<ICategory> rootCategories = await Catalog.GetRootCategoriesAsync();

    // The IDs of the current page and its ancestors, for example "-1,1050,1062,1088".
    string currentPath = Model.Path;

    void RenderCategories(IEnumerable<ICategory> categories)
    {
        if (!categories.Any())
        {
            return;
        }

        <ul>
            @foreach (ICategory category in categories)
            {
                // True for the category being shown and for every category above it.
                bool open = category.IsCurrent(currentPath);

                <li>
                    <a href="@category.Url" aria-current="@(category.Id == Model.Id ? "page" : null)">@category.Title</a>

                    @* Open only the branch the customer is in. *@
                    @if (open)
                    {
                        RenderCategories(category.SubCategories);
                    }
                </li>
            }
        </ul>
    }
}

<nav aria-label="Categories">
    @{ RenderCategories(rootCategories); }
</nav>
```

What to know:

- `GetRootCategoriesAsync()` returns the top-level categories of the store of the current request. Pass a store alias to read another store.
- `category.IsCurrent(path)` takes the `Path` of the Umbraco page being shown, which is the list of IDs of the page and its ancestors. It is `true` for the current category and for every category above it. It is also `true` on a product page for the categories above the product's node.
- `SubCategories` holds the categories directly below. To render the whole tree, call `RenderCategories(category.SubCategories)` without the `if`. `SubCategoriesRecursive` gives every category below as one flat list.
- The catalog is read from memory, so rendering the menu on every page does not query the database.
- The view inherits `UmbracoViewPage`, so `Model` is the current Umbraco page. On a page that is not Umbraco content, pass the path in yourself.

## Show a sale price

This partial view shows a price. When the price has a discount, it shows the old price struck through, the new price and the saving in percent.

Save it as `Views/Partials/_Price.cshtml` and render it with `@await Html.PartialAsync("_Price", product.Price)`:

```cshtml
@model Ekom.Models.IPrice
@{
    decimal before = Model.BeforeDiscountWithVat.Value;
    decimal now = Model.WithVat.Value;
    bool onSale = Model.HasDiscount && now < before;
}

@if (onSale)
{
    int percent = (int)Math.Round((before - now) / before * 100);

    <p>
        <s><span class="visually-hidden">Was</span> @Model.BeforeDiscountWithVat.CurrencyString</s>
        <strong><span class="visually-hidden">Now</span> @Model.WithVat.CurrencyString</strong>
        <span>Save @percent%</span>
    </p>
}
else
{
    <p><strong>@Model.WithVat.CurrencyString</strong></p>
}
```

What to know:

- `BeforeDiscountWithVat` and `WithVat` are the price before and after the discount, both with VAT in every store. To show prices without VAT, use `BeforeDiscountWithOutVat` and `WithoutVat`. Do not pair `BeforeDiscount` with `WithVat`: in a store whose prices exclude VAT, one has VAT and the other does not.
- `HasDiscount` is `true` when a discount is attached to the price. The view also checks that the price is lower, so a discount of zero does not show a sale.
- `product.Price` already has the product discount that applies to the product. Order discounts and coupons are not part of it; they appear on the basket lines. See [Discounts and coupons](../guides/discounts.md).
- For a product with variants, pass the price of the variant the customer chose, for example `product.PrimaryVariant?.Price ?? product.Price`.
- `CurrencyString` formats the amount in the price's currency. Do not format `Value` yourself.
- The `visually-hidden` class is yours to define. It hides "Was" and "Now" on screen and leaves them for screen readers, which do not announce struck-through text.

## Show a product with its variants and an add-to-basket form

The product template shows the variants as radio buttons, grouped by variant group, and posts the chosen variant and quantity to `BasketController`. Variants that cannot be bought are shown but cannot be chosen.

```cshtml
@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@using Ekom.API
@using Ekom.Models
@using MyShop.Storefront
@inject Catalog Catalog
@{
    IProduct? product = await Catalog.GetProductAsync();
    if (product is null)
    {
        return;
    }

    // Preselect the primary variant, or the first variant that can be bought.
    IVariant? preselected = product.PrimaryVariant is { Available: true } primary
        ? primary
        : product.AllVariants.FirstOrDefault(x => x.Available);
}

<h1>@product.Title</h1>

<p>
    @if (product.Price.HasDiscount)
    {
        <s>@product.Price.BeforeDiscount.CurrencyString</s>
    }
    <strong>@product.Price.AfterDiscount.CurrencyString</strong>
</p>

@if (TempData[BasketController.MessageKey] is string message)
{
    <p role="status">@message</p>
}

@if (!product.Available)
{
    <p>Out of stock</p>
}
else
{
    <form method="post" action="/shop/basket/add">
        @Html.AntiForgeryToken()
        <input type="hidden" name="StoreAlias" value="@product.Store.Alias" />
        <input type="hidden" name="ProductKey" value="@product.Key" />
        <input type="hidden" name="ReturnUrl" value="@Context.Request.Path" />

        @foreach (IVariantGroup group in product.VariantGroups)
        {
            <fieldset>
                <legend>@group.Title</legend>
                @foreach (IVariant variant in group.Variants)
                {
                    <label>
                        <input type="radio" name="VariantKey" value="@variant.Key" required
                               checked="@(variant.Key == preselected?.Key)"
                               disabled="@(!variant.Available)" />
                        @variant.Title, @variant.Price.AfterDiscount.CurrencyString
                        @if (!variant.Available)
                        {
                            <text>(sold out)</text>
                        }
                    </label>
                }
            </fieldset>
        }

        <label for="quantity">Quantity</label>
        <input id="quantity" type="number" name="Quantity" value="1" min="1" max="100" />

        <button type="submit">Add to basket</button>
    </form>
}

<div>@Html.Raw(product.Description)</div>
```

The form posts to this controller. The basket page in the next example uses its other two actions.

```csharp
using System.ComponentModel.DataAnnotations;

namespace MyShop.Storefront;

public sealed class AddToBasketForm
{
    [Required]
    public string StoreAlias { get; set; } = string.Empty;

    public Guid ProductKey { get; set; }

    // Empty for a product without variants.
    public Guid? VariantKey { get; set; }

    [Range(typeof(decimal), "1", "100")]
    public decimal Quantity { get; set; } = 1;

    public string? ReturnUrl { get; set; }
}

public sealed class BasketLineForm
{
    [Required]
    public string StoreAlias { get; set; } = string.Empty;

    public Guid LineKey { get; set; }

    // Only used to change the quantity. 0 removes the line.
    [Range(0, 100)]
    public int Quantity { get; set; }

    public string? ReturnUrl { get; set; }
}
```

```csharp
using Ekom.ActionFilters;
using Ekom.API;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace MyShop.Storefront;

[Route("shop/basket")]
[AutoValidateAntiforgeryToken]
public sealed class BasketController(Order orderApi) : Controller
{
    // The views read this TempData key to show the result of the last change.
    public const string MessageKey = "ShopMessage";

    [HttpPost("add")]
    [EkomRateLimit(EkomRateLimitPolicies.OrderAdd)]
    public async Task<IActionResult> AddAsync([FromForm] AddToBasketForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return Back(form.ReturnUrl, "Choose a quantity between 1 and 100.");
        }

        try
        {
            await orderApi.AddOrderLineAsync(
                form.ProductKey,
                form.Quantity,
                form.StoreAlias,
                new AddOrderSettings
                {
                    VariantKey = form.VariantKey,
                    OrderAction = OrderAction.AddOrUpdate,
                },
                ct);

            return Back(form.ReturnUrl, "The product is in your basket.");
        }
        catch (VariantRequiredException)
        {
            return Back(form.ReturnUrl, "Choose an option before you add the product.");
        }
        catch (Exception ex) when (ex is ProductNotFoundException or VariantNotFoundException)
        {
            return Back(form.ReturnUrl, "This product is no longer for sale.");
        }
        catch (NotEnoughStockException)
        {
            return Back(form.ReturnUrl, "We do not have that many in stock.");
        }
        catch (CheckoutConflictException ex)
        {
            // A checkout is running for this basket. The message is written for the customer.
            return Back(form.ReturnUrl, new CheckoutStateError(ex.Reason).Message);
        }
    }

    [HttpPost("quantity")]
    public async Task<IActionResult> ChangeQuantityAsync([FromForm] BasketLineForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return Back(form.ReturnUrl, "Choose a quantity between 0 and 100.");
        }

        try
        {
            // A quantity of 0 removes the line.
            await orderApi.UpdateOrderlineQuantityAsync(form.LineKey, form.Quantity, form.StoreAlias, ct: ct);
            return Back(form.ReturnUrl, null);
        }
        catch (NotEnoughStockException)
        {
            return Back(form.ReturnUrl, "We do not have that many in stock.");
        }
        catch (Exception ex) when (ex is OrderLineNotFoundException or OrderInfoNotFoundException)
        {
            return Back(form.ReturnUrl, "Your basket has changed. Check it and try again.");
        }
        catch (CheckoutConflictException ex)
        {
            return Back(form.ReturnUrl, new CheckoutStateError(ex.Reason).Message);
        }
    }

    [HttpPost("remove")]
    public async Task<IActionResult> RemoveAsync([FromForm] BasketLineForm form, CancellationToken ct)
    {
        try
        {
            // Lines linked to this line, such as gift wrapping, are removed with it.
            await orderApi.RemoveOrderLineAsync(form.LineKey, form.StoreAlias, ct: ct);
            return Back(form.ReturnUrl, null);
        }
        catch (Exception ex) when (ex is OrderLineNotFoundException or OrderInfoNotFoundException)
        {
            return Back(form.ReturnUrl, "Your basket has changed. Check it and try again.");
        }
        catch (CheckoutConflictException ex)
        {
            return Back(form.ReturnUrl, new CheckoutStateError(ex.Reason).Message);
        }
    }

    private IActionResult Back(string? returnUrl, string? message)
    {
        if (message is not null)
        {
            TempData[MessageKey] = message;
        }

        // Redirect only to an address on this site.
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : LocalRedirect("/");
    }
}
```

The controller uses attribute routes and needs no registration: Ekom's startup calls `MapControllers()`, which maps every controller with attribute routes, yours included.

What to know:

- `GetProductAsync()` without arguments returns the product Ekom routed the request to, or `null`.
- A product with variants needs a variant key. Without one, `AddOrderLineAsync` throws `VariantRequiredException`. A variant that does not belong to the product, or is not in the store, throws `VariantNotFoundException`.
- The first line a customer adds creates the basket and the basket cookie `ekmOrder-{StoreAlias}`. The cookie lasts `Ekom:BasketCookieLifetime` days, 1 by default.
- `OrderAction.AddOrUpdate` adds the quantity to a line that already has the same product and variant. Use `OrderAction.Set` to replace the quantity instead.
- Ekom checks stock when the line is added and again when the customer pays. Adding a product to the basket does not hold stock for the customer.
- `[EkomRateLimit(EkomRateLimitPolicies.OrderAdd)]` gives your action the same limit as Ekom's own `POST /ekom/order/add`: 30 requests a minute for each IP address. Over the limit, the browser gets an empty `429` response. Behind a proxy, turn on forwarded headers or every visitor shares one limit.
- Ekom's own `/ekom/order` routes do not check an antiforgery token. Your controller does, through `[AutoValidateAntiforgeryToken]`, so every form posts `@Html.AntiForgeryToken()`.
- The store alias comes from the page in a hidden field. A customer who changes it can only reach their own basket in another store.

## Show and change the basket

The basket template lists the lines with forms to change the quantity or remove a line, and shows the totals. The forms post to `BasketController` from the previous example.

```cshtml
@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@using Ekom.API
@using Ekom.Models
@using MyShop.Storefront
@inject Order OrderApi
@{
    // The customer's basket in the store of this page, found by the basket cookie.
    IOrderInfo? basket = await OrderApi.GetOrderAsync();
    string returnUrl = Context.Request.Path;
}

<h1>Basket</h1>

@if (TempData[BasketController.MessageKey] is string message)
{
    <p role="status">@message</p>
}

@if (basket is null || basket.TotalQuantity <= 0)
{
    <p>Your basket is empty.</p>
    return;
}

<table>
    <thead>
        <tr><th>Product</th><th>Price</th><th>Quantity</th><th>Total</th><th></th></tr>
    </thead>
    <tbody>
        @foreach (IOrderLine line in basket.OrderLines)
        {
            IPrice unitPrice = line.Variant?.Price ?? line.Product.Price;

            <tr>
                <td>
                    <a href="@line.Product.Url">@line.Product.Title</a>
                    @if (line.Variant is not null)
                    {
                        <br />@line.Variant.Title
                    }
                </td>
                <td>@unitPrice.AfterDiscount.CurrencyString</td>
                <td>
                    <form method="post" action="/shop/basket/quantity">
                        @Html.AntiForgeryToken()
                        <input type="hidden" name="StoreAlias" value="@basket.StoreInfo.Alias" />
                        <input type="hidden" name="LineKey" value="@line.Key" />
                        <input type="hidden" name="ReturnUrl" value="@returnUrl" />
                        <input type="number" name="Quantity" value="@decimal.ToInt32(line.Quantity)" min="0" max="100" aria-label="Quantity" />
                        <button type="submit">Update</button>
                    </form>
                </td>
                <td>
                    @if (line.Amount.HasDiscount)
                    {
                        <s>@line.Amount.BeforeDiscount.CurrencyString</s>
                    }
                    @line.Amount.AfterDiscount.CurrencyString
                </td>
                <td>
                    <form method="post" action="/shop/basket/remove">
                        @Html.AntiForgeryToken()
                        <input type="hidden" name="StoreAlias" value="@basket.StoreInfo.Alias" />
                        <input type="hidden" name="LineKey" value="@line.Key" />
                        <input type="hidden" name="ReturnUrl" value="@returnUrl" />
                        <button type="submit">Remove</button>
                    </form>
                </td>
            </tr>
        }
    </tbody>
</table>

<dl>
    <dt>Products</dt>
    <dd>@basket.SubTotal.WithVat.CurrencyString</dd>

    @if (basket.DiscountAmountWithVat.Value > 0)
    {
        <dt>Discounts</dt>
        <dd>-@basket.DiscountAmountWithVat.CurrencyString</dd>
    }

    @if (basket.ShippingProvider is not null)
    {
        <dt>Delivery: @basket.ShippingProvider.Title</dt>
        <dd>@basket.ShippingProvider.Price.WithVat.CurrencyString</dd>
    }

    @if (basket.PaymentProvider is not null && basket.PaymentProvider.Price.Value > 0)
    {
        <dt>Payment fee</dt>
        <dd>@basket.PaymentProvider.Price.WithVat.CurrencyString</dd>
    }

    <dt>Total</dt>
    <dd>@basket.GrandTotal.CurrencyString</dd>

    @if (basket.Giftcards.Count > 0)
    {
        <dt>To pay after gift cards</dt>
        <dd>@basket.ChargedAmount.CurrencyString</dd>
    }

    <dt>Of which VAT</dt>
    <dd>@basket.ChargedVat.CurrencyString</dd>
</dl>

<a href="/checkout/details/">Go to checkout</a>
```

What to know:

- `GetOrderAsync()` without a store alias returns the basket of the store this page belongs to. It returns `null` when the customer has no basket, or when the order has a final status. A basket whose status is `WaitingForPayment` is still returned. If the customer changes it then, Ekom ends that payment attempt and releases its stock holds.
- A quantity of 0 removes the line. Removing a line also removes the lines linked to it. See [Linked order lines](../guides/linked-order-lines.md).
- The quantity form takes whole numbers. If you sell by weight or length, use a decimal field and check how your site's culture writes decimals before you bind it.
- `OrderLineNotFoundException` means the line is gone, for example because the customer removed it in another tab.
- The totals add up in every store, because each one is with VAT: `SubTotal.WithVat` is the lines before discounts, `DiscountAmountWithVat` is every product and order discount, and `GrandTotal` adds shipping and the payment fee. To show the summary without VAT, use `SubTotal.WithoutVat`, `DiscountAmountWithOutVat` and `GrandTotalWithOutVat`. `DiscountAmount`, without a suffix, is without VAT in a store whose prices exclude VAT, so do not put it next to totals that include VAT.
- `GrandTotal` is the order value. `ChargedAmount` is what is left to pay after gift cards, and is what the customer is charged.
- `ChargedVat` is the VAT of the whole order, with shipping and the payment fee. `Vat` is the VAT of the lines, plus the VAT of shipping when the store has `ApplyVatOnShipping` on; it does not include VAT on a payment fee.
- Each change saves the whole basket. Two changes at the same moment, for example from two tabs, can overwrite each other.

## Apply and remove a coupon

This partial view shows a field for a discount code, or the code on the basket with a button to remove it. Save it as `Views/Partials/_BasketCoupon.cshtml` and add it to the basket template above the totals with `<partial name="_BasketCoupon" model="basket" />`.

```cshtml
@using Ekom.Models
@model IOrderInfo

@if (string.IsNullOrEmpty(Model.Coupon))
{
    <form method="post" action="/shop/coupon/apply">
        @Html.AntiForgeryToken()
        <input type="hidden" name="StoreAlias" value="@Model.StoreInfo.Alias" />
        <input type="hidden" name="ReturnUrl" value="@Context.Request.Path" />
        <label for="coupon">Discount code</label>
        <input id="coupon" name="Coupon" autocomplete="off" maxlength="100" />
        <button type="submit">Apply</button>
    </form>
}
else
{
    <form method="post" action="/shop/coupon/remove">
        @Html.AntiForgeryToken()
        <input type="hidden" name="StoreAlias" value="@Model.StoreInfo.Alias" />
        <input type="hidden" name="ReturnUrl" value="@Context.Request.Path" />
        <p>
            Discount code @Model.Coupon.ToUpperInvariant()
            @if (Model.Discount is not null)
            {
                <text>(@Model.Discount.Title)</text>
            }
            <button type="submit">Remove</button>
        </p>
    </form>
}
```

The controller turns each result into a message for the customer:

```csharp
using Ekom.ActionFilters;
using Ekom.API;
using Ekom.Exceptions;
using Ekom.Models;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace MyShop.Storefront;

[Route("shop/coupon")]
[AutoValidateAntiforgeryToken]
public sealed class CouponController(Order orderApi) : Controller
{
    [HttpPost("apply")]
    [EkomRateLimit(EkomRateLimitPolicies.OrderCoupon)] // The same limit as Ekom's own coupon endpoint.
    public async Task<IActionResult> ApplyAsync([FromForm] CouponForm form, CancellationToken ct)
    {
        string code = form.Coupon?.Trim() ?? string.Empty;
        if (code.Length == 0)
        {
            return Back(form.ReturnUrl, "Enter a discount code.");
        }

        try
        {
            bool applied = await orderApi.ApplyCouponToOrderAsync(code, form.StoreAlias, ct);

            return Back(form.ReturnUrl, applied
                ? $"The code {code} is on your basket."
                : "This code does not lower the price of your basket. Your basket may already have a better discount.");
        }
        catch (DiscountNotFoundException)
        {
            return Back(form.ReturnUrl, "We do not recognise this code.");
        }
        catch (DiscountHasNoUsageException)
        {
            return Back(form.ReturnUrl, "This code has been used up.");
        }
        catch (DiscountUnableToFindCouponException)
        {
            return Back(form.ReturnUrl, "This code cannot be used in this shop.");
        }
        catch (DiscountDuplicateException)
        {
            return Back(form.ReturnUrl, "This code is already on your basket.");
        }
        catch (CouponApplicationRejectedException ex)
        {
            // The message comes from your own BeforeApplyCouponDiscountAsync handler.
            return Back(form.ReturnUrl, ex.Message);
        }
        catch (CheckoutConflictException ex)
        {
            return Back(form.ReturnUrl, new CheckoutStateError(ex.Reason).Message);
        }
    }

    [HttpPost("remove")]
    public async Task<IActionResult> RemoveAsync([FromForm] CouponForm form, CancellationToken ct)
    {
        try
        {
            // Removes the order discount only when it came from a coupon.
            await orderApi.RemoveCouponFromOrderAsync(form.StoreAlias, settings: null, ct);
            return Back(form.ReturnUrl, "The discount code was removed.");
        }
        catch (CheckoutConflictException ex)
        {
            return Back(form.ReturnUrl, new CheckoutStateError(ex.Reason).Message);
        }
    }

    private IActionResult Back(string? returnUrl, string message)
    {
        TempData[BasketController.MessageKey] = message;
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : LocalRedirect("/");
    }
}

public sealed class CouponForm
{
    [Required]
    public string StoreAlias { get; set; } = string.Empty;

    public string? Coupon { get; set; }

    public string? ReturnUrl { get; set; }
}
```

| Result | What happened |
| --- | --- |
| `true` | The coupon's order discount is on the basket. |
| `false` | There is no basket, the discount does not fit the basket, or the basket already has an order discount that gives the same or a lower total. |
| `DiscountNotFoundException` | No coupon has this code. |
| `DiscountHasNoUsageException` | The code has no uses left. |
| `DiscountUnableToFindCouponException` | The code's discount is not available in this store, for example because it is not published or is turned off there. |
| `DiscountDuplicateException` | The basket already has this discount. |
| `CouponApplicationRejectedException` | A `BeforeApplyCouponDiscountAsync` handler of yours rejected the code, with its own message. |

What to know:

- Ekom converts the code to lower case before it looks it up, and stores it on the basket in lower case. The partial view shows it in upper case.
- Applying a code does not use it up. Ekom takes one use off when the order completes, and checks the remaining uses again when the customer pays. If none are left then, payment returns the `couponUnavailable` error shown in the next example.
- Uses are counted per code, not per customer. To allow one use per customer, reject the code in a `BeforeApplyCouponDiscountAsync` handler. See [Reject a coupon with your own rule](../guides/discounts/developers.md#reject-a-coupon-with-your-own-rule).
- `RemoveCouponFromOrderAsync` removes the order discount only when it came from a coupon. A discount Ekom applied by itself stays.
- The coupon action uses the same rate limit as Ekom's coupon endpoint: 13 attempts at once, then 10 more each minute for each IP address. It slows down anyone who guesses codes.
- Show messages from your rejection handler as text. Razor encodes them for you.

## Take the customer through checkout

Checkout is three pages: the customer's details, delivery, and payment. Each page posts to `ShopCheckoutController`. The payment action saves the payment choice, starts payment with `Order.PayAsync`, and handles each response checkout can give.

The forms the pages post:

```csharp
using System.ComponentModel.DataAnnotations;

namespace MyShop.Storefront;

public sealed class CustomerDetailsForm
{
    [Required]
    public string StoreAlias { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    [Required]
    public string Address { get; set; } = string.Empty;

    [Required]
    public string City { get; set; } = string.Empty;

    [Required]
    public string ZipCode { get; set; } = string.Empty;

    // A two-letter code such as IS. Ekom filters providers by it.
    [Required, StringLength(2, MinimumLength = 2)]
    public string Country { get; set; } = string.Empty;

    public bool ShipToOtherAddress { get; set; }

    public string? ShippingName { get; set; }

    public string? ShippingAddress { get; set; }

    public string? ShippingCity { get; set; }

    public string? ShippingZipCode { get; set; }

    public string? ShippingCountry { get; set; }

    // The page of the next step, for example /checkout/delivery.
    public string? NextUrl { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class ShippingChoiceForm
{
    [Required]
    public string StoreAlias { get; set; } = string.Empty;

    public Guid ShippingProvider { get; set; }

    public string? NextUrl { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class PaymentForm
{
    [Required]
    public string StoreAlias { get; set; } = string.Empty;

    public Guid PaymentProvider { get; set; }

    // Pages to send the customer to when a step must be done again.
    public string? DetailsUrl { get; set; }

    public string? DeliveryUrl { get; set; }

    public string? BasketUrl { get; set; }

    public string? ReturnUrl { get; set; }
}
```

The pages and the controller use the same helper to list the providers, so a customer can only choose a provider the page offered:

```csharp
using Ekom.API;
using Ekom.Models;

namespace MyShop.Storefront;

// The checkout pages and ShopCheckoutController use the same lists,
// so a customer can only choose a provider that the page offered.
public static class CheckoutOptions
{
    public static Task<IReadOnlyList<IShippingProvider>> GetShippingProvidersAsync(
        Providers providers,
        IOrderInfo order,
        CancellationToken ct = default)
    {
        string? country = order.CustomerInformation.Shipping.Country;
        if (string.IsNullOrWhiteSpace(country))
        {
            country = order.CustomerInformation.Customer.Country;
        }

        return providers.GetShippingProvidersAsync(order.StoreInfo.Alias, country, AmountForProviderRules(order), ct);
    }

    public static Task<IReadOnlyList<IPaymentProvider>> GetPaymentProvidersAsync(
        Providers providers,
        IOrderInfo order,
        CancellationToken ct = default)
        => providers.GetPaymentProvidersAsync(
            order.StoreInfo.Alias,
            order.CustomerInformation.Customer.Country,
            AmountForProviderRules(order),
            ct);

    // The amount Ekom checks provider ranges against at payment:
    // the amount to pay without shipping and payment fees, never below zero.
    public static decimal AmountForProviderRules(IOrderInfo order)
    {
        decimal amount = order.ChargedAmount.Value;
        amount -= order.ShippingProvider?.Price.Value ?? 0;
        amount -= order.PaymentProvider?.Price.Value ?? 0;
        return Math.Max(0, amount);
    }
}
```

The controller:

```csharp
using Ekom.ActionFilters;
using Ekom.API;
using Ekom.Exceptions;
using Ekom.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MyShop.Storefront;

// Do not call this class CheckoutController: Ekom already has a controller with that name.
[Route("shop/checkout")]
[AutoValidateAntiforgeryToken]
public sealed class ShopCheckoutController(
    Order orderApi,
    Providers providers,
    ILogger<ShopCheckoutController> logger) : Controller
{
    public const string MessageKey = "CheckoutMessage";

    [HttpPost("details")]
    [EkomRateLimit(EkomRateLimitPolicies.OrderUpdate)]
    public async Task<IActionResult> SaveDetailsAsync([FromForm] CustomerDetailsForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return Back(form.ReturnUrl, "Fill in every field marked as required.");
        }

        IOrderInfo? basket = await orderApi.GetOrderAsync(form.StoreAlias, ct);
        if (basket is null || basket.TotalQuantity <= 0)
        {
            return Back("/", "Your basket is empty.");
        }

        var fields = new Dictionary<string, string>
        {
            ["storeAlias"] = form.StoreAlias,
            ["customerName"] = form.Name,
            ["customerEmail"] = form.Email,
            ["customerPhone"] = form.Phone ?? string.Empty,
            ["customerAddress"] = form.Address,
            ["customerCity"] = form.City,
            ["customerZipCode"] = form.ZipCode,
            ["customerCountry"] = form.Country.ToUpperInvariant(),
            // Empty shipping fields mean "ship to the customer's address".
            ["shippingName"] = form.ShipToOtherAddress ? form.ShippingName ?? string.Empty : string.Empty,
            ["shippingAddress"] = form.ShipToOtherAddress ? form.ShippingAddress ?? string.Empty : string.Empty,
            ["shippingCity"] = form.ShipToOtherAddress ? form.ShippingCity ?? string.Empty : string.Empty,
            ["shippingZipCode"] = form.ShipToOtherAddress ? form.ShippingZipCode ?? string.Empty : string.Empty,
            ["shippingCountry"] = form.ShipToOtherAddress ? form.ShippingCountry?.ToUpperInvariant() ?? string.Empty : string.Empty,
        };

        try
        {
            await orderApi.UpdateCustomerInformationAsync(fields, ct: ct);
        }
        catch (FormatException)
        {
            // Ekom throws a FormatException when the email address is not valid.
            return Back(form.ReturnUrl, "Check your email address.");
        }
        catch (ArgumentException ex)
        {
            // Field limits, for example a name longer than 200 characters.
            return Back(form.ReturnUrl, ex.Message);
        }

        return Back(form.NextUrl, null);
    }

    [HttpPost("delivery")]
    [EkomRateLimit(EkomRateLimitPolicies.OrderUpdate)]
    public async Task<IActionResult> ChooseShippingAsync([FromForm] ShippingChoiceForm form, CancellationToken ct)
    {
        IOrderInfo? basket = await orderApi.GetOrderAsync(form.StoreAlias, ct);
        if (basket is null || basket.TotalQuantity <= 0)
        {
            return Back("/", "Your basket is empty.");
        }

        // Accept only a provider the delivery page offered for this country and amount.
        IReadOnlyList<IShippingProvider> offered = await CheckoutOptions.GetShippingProvidersAsync(providers, basket, ct);
        if (!offered.Any(x => x.Key == form.ShippingProvider))
        {
            return Back(form.ReturnUrl, "Choose one of the delivery options.");
        }

        try
        {
            await orderApi.UpdateShippingInformationAsync(
                form.ShippingProvider,
                form.StoreAlias,
                new Dictionary<string, string>(),
                ct: ct);
        }
        catch (CheckoutConflictException ex)
        {
            return Back(form.ReturnUrl, new CheckoutStateError(ex.Reason).Message);
        }

        return Back(form.NextUrl, null);
    }

    [HttpPost("pay")]
    [EkomRateLimit(EkomRateLimitPolicies.CheckoutPay)]
    public async Task<IActionResult> PayAsync([FromForm] PaymentForm form, CancellationToken ct)
    {
        IOrderInfo? basket = await orderApi.GetOrderAsync(form.StoreAlias, ct);
        if (basket is null || basket.TotalQuantity <= 0)
        {
            return Back("/", "Your basket is empty.");
        }

        IReadOnlyList<IPaymentProvider> offered = await CheckoutOptions.GetPaymentProvidersAsync(providers, basket, ct);
        if (!offered.Any(x => x.Key == form.PaymentProvider))
        {
            return Back(form.ReturnUrl, "Choose one of the payment options.");
        }

        CheckoutResponse response;
        try
        {
            // Order.PayAsync pays with the provider saved on the basket, so save the choice first.
            basket = await orderApi.UpdatePaymentInformationAsync(
                form.PaymentProvider,
                form.StoreAlias,
                new Dictionary<string, string>(),
                ct: ct);

            response = await orderApi.PayAsync(
                new PaymentRequest
                {
                    StoreAlias = form.StoreAlias,
                    Culture = basket.Culture,
                },
                form.StoreAlias,
                basket.UniqueId,
                ct);
        }
        catch (CheckoutConflictException ex)
        {
            return Back(form.ReturnUrl, new CheckoutStateError(ex.Reason).Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // For example a payment provider package that is not installed. The details are only in the log.
            logger.LogError(ex, "Payment could not start for order {OrderId} in store {StoreAlias}", basket.UniqueId, form.StoreAlias);
            return Back(form.ReturnUrl, "We could not start the payment. Try again, or choose another payment option.");
        }

        switch (response.HttpStatusCode)
        {
            case 230:
                // Online payment: the provider's page, usually a form that posts the customer on to the gateway.
                string html = response.ResponseBody as string ?? string.Empty;
                return html.Length > 0 ? Content(html, "text/html") : new EmptyResult();

            case 300:
                // Offline payment: the order is already completed. This is the provider's success URL with ?orderId=.
                return Redirect((string)response.ResponseBody);

            case 400 when response.ResponseBody is ShippingValidationError shipping:
                return Back(form.DeliveryUrl, shipping.Message);

            case 400:
                return Back(form.DetailsUrl, "Fill in your name and email address.");

            case 409 when response.ResponseBody is CheckoutStateError state:
                // Never resubmit the payment automatically after a 409.
                return Back(form.ReturnUrl, state.Message);

            case 530 when response.ResponseBody is StockError stock:
                IOrderLine? line = basket.OrderLines.FirstOrDefault(x => x.Key == stock.OrderLineKey);
                return Back(form.BasketUrl, line is null
                    ? "Some products in your basket are no longer in stock."
                    : $"We do not have enough of {line.Product.Title} in stock. Change the quantity and try again.");

            default:
                logger.LogWarning("Checkout returned {StatusCode} for order {OrderId}", response.HttpStatusCode, basket.UniqueId);
                return Back(form.ReturnUrl, "We could not start the payment. Try again.");
        }
    }

    private IActionResult Back(string? url, string? message)
    {
        if (message is not null)
        {
            TempData[MessageKey] = message;
        }

        return Url.IsLocalUrl(url) ? LocalRedirect(url) : LocalRedirect("/");
    }
}
```

The details page, `/checkout/details/`:

```cshtml
@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@using Ekom.API
@using Ekom.Models
@using MyShop.Storefront
@inject Order OrderApi
@{
    IOrderInfo? basket = await OrderApi.GetOrderAsync();
    if (basket is null || basket.TotalQuantity <= 0)
    {
        Context.Response.Redirect("/basket/");
        return;
    }

    Customer customer = basket.CustomerInformation.Customer;
    CustomerShippingInfo shipping = basket.CustomerInformation.Shipping;
}

<h1>Your details</h1>

@if (TempData[ShopCheckoutController.MessageKey] is string message)
{
    <p role="alert">@message</p>
}

<form method="post" action="/shop/checkout/details">
    @Html.AntiForgeryToken()
    <input type="hidden" name="StoreAlias" value="@basket.StoreInfo.Alias" />
    <input type="hidden" name="ReturnUrl" value="/checkout/details/" />
    <input type="hidden" name="NextUrl" value="/checkout/delivery/" />

    <label>Name <input name="Name" value="@customer.Name" required maxlength="200" autocomplete="name" /></label>
    <label>Email <input type="email" name="Email" value="@customer.Email" required maxlength="200" autocomplete="email" /></label>
    <label>Phone <input name="Phone" value="@customer.Phone" autocomplete="tel" /></label>
    <label>Address <input name="Address" value="@customer.Address" required autocomplete="street-address" /></label>
    <label>Postal code <input name="ZipCode" value="@customer.ZipCode" required autocomplete="postal-code" /></label>
    <label>City <input name="City" value="@customer.City" required autocomplete="address-level2" /></label>
    <label>
        Country
        <select name="Country" required>
            <option value="IS" selected="@(customer.Country == "IS")">Iceland</option>
            <option value="DK" selected="@(customer.Country == "DK")">Denmark</option>
        </select>
    </label>

    <fieldset>
        <legend>
            <label>
                <input type="checkbox" name="ShipToOtherAddress" value="true"
                       checked="@(!string.IsNullOrEmpty(shipping.Address))" />
                Deliver to another address
            </label>
        </legend>
        <label>Name <input name="ShippingName" value="@shipping.Name" /></label>
        <label>Address <input name="ShippingAddress" value="@shipping.Address" /></label>
        <label>Postal code <input name="ShippingZipCode" value="@shipping.ZipCode" /></label>
        <label>City <input name="ShippingCity" value="@shipping.City" /></label>
        <label>
            Country
            <select name="ShippingCountry">
                <option value="IS" selected="@(shipping.Country == "IS")">Iceland</option>
                <option value="DK" selected="@(shipping.Country == "DK")">Denmark</option>
            </select>
        </label>
    </fieldset>

    <button type="submit">Continue to delivery</button>
</form>
```

The delivery page, `/checkout/delivery/`:

```cshtml
@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@using Ekom.API
@using Ekom.Models
@using MyShop.Storefront
@inject Order OrderApi
@inject Providers Providers
@{
    IOrderInfo? basket = await OrderApi.GetOrderAsync();
    if (basket is null || basket.TotalQuantity <= 0)
    {
        Context.Response.Redirect("/basket/");
        return;
    }

    IReadOnlyList<IShippingProvider> options = await CheckoutOptions.GetShippingProvidersAsync(Providers, basket);
}

<h1>Delivery</h1>

@if (TempData[ShopCheckoutController.MessageKey] is string message)
{
    <p role="alert">@message</p>
}

@if (options.Count == 0)
{
    <p>We cannot deliver this order to your address. <a href="/checkout/details/">Change the address</a>.</p>
}
else
{
    <form method="post" action="/shop/checkout/delivery">
        @Html.AntiForgeryToken()
        <input type="hidden" name="StoreAlias" value="@basket.StoreInfo.Alias" />
        <input type="hidden" name="ReturnUrl" value="/checkout/delivery/" />
        <input type="hidden" name="NextUrl" value="/checkout/payment/" />

        @foreach (IShippingProvider provider in options)
        {
            <label>
                <input type="radio" name="ShippingProvider" value="@provider.Key" required
                       checked="@(basket.ShippingProvider?.Key == provider.Key)" />
                @provider.Title, @provider.Price.WithVat.CurrencyString
            </label>
        }

        <button type="submit">Continue to payment</button>
    </form>
}
```

The payment page, `/checkout/payment/`:

```cshtml
@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@using Ekom.API
@using Ekom.Models
@using MyShop.Storefront
@inject Order OrderApi
@inject Providers Providers
@{
    IOrderInfo? basket = await OrderApi.GetOrderAsync();
    if (basket is null || basket.TotalQuantity <= 0)
    {
        Context.Response.Redirect("/basket/");
        return;
    }

    IReadOnlyList<IPaymentProvider> options = await CheckoutOptions.GetPaymentProvidersAsync(Providers, basket);
}

<h1>Payment</h1>

@if (TempData[ShopCheckoutController.MessageKey] is string message)
{
    <p role="alert">@message</p>
}

<p>
    Delivery: @(basket.ShippingProvider?.Title ?? "none")<br />
    To pay: @basket.ChargedAmount.CurrencyString
</p>

<form method="post" action="/shop/checkout/pay">
    @Html.AntiForgeryToken()
    <input type="hidden" name="StoreAlias" value="@basket.StoreInfo.Alias" />
    <input type="hidden" name="ReturnUrl" value="/checkout/payment/" />
    <input type="hidden" name="DetailsUrl" value="/checkout/details/" />
    <input type="hidden" name="DeliveryUrl" value="/checkout/delivery/" />
    <input type="hidden" name="BasketUrl" value="/basket/" />

    @foreach (IPaymentProvider provider in options)
    {
        <label>
            <input type="radio" name="PaymentProvider" value="@provider.Key" required
                   checked="@(basket.PaymentProvider?.Key == provider.Key)" />
            @provider.Title
        </label>
    }

    <button type="submit">Pay</button>
</form>
```

The responses `PayAsync` can return:

| `HttpStatusCode` | Meaning | What the controller does |
| --- | --- | --- |
| `230` | Online payment. `ResponseBody` is the payment provider's HTML. | Returns the HTML, which sends the customer on to the gateway. |
| `300` | Offline payment, such as an invoice. The order is completed. `ResponseBody` is the provider's success URL with `?orderId=`. | Redirects there. |
| `400` with `ShippingValidationError` | The chosen delivery option no longer fits the order. | Back to the delivery page with Ekom's message. |
| `400` | The order has no customer name or email. | Back to the details page. |
| `409` with `CheckoutStateError` | Checkout cannot start in the order's current state. | Back to the payment page with Ekom's message. |
| `530` with `StockError` | A line has too little stock. `OrderLineKey` says which. | Back to the basket, naming the product. |

What to know:

- Name the controller anything but `CheckoutController`. Ekom has a controller with that name for `/ekom/mvcCheckout`.
- `Order.PayAsync` pays with the payment provider saved on the basket. It does not read `PaymentRequest.PaymentProvider`, so the action saves the choice first. `UpdatePaymentInformationAsync` does not throw for a key that is not a provider of the store; it leaves the basket as it was. Checking the key against the offered list catches that.
- `Order.PayAsync` needs a web request. In a background job it throws `ArgumentNullException`.
- `UpdateCustomerInformationAsync` throws a `FormatException` for an email address that is not valid, and an `ArgumentException` for a value over its limit, such as a name longer than 200 characters. Ekom's own exception types for these are internal, so catch the base types.
- Keys that start with `customer` are saved on the customer and keys that start with `shipping` on the shipping address. The controller sends empty shipping fields when the customer ships to their own address, which clears an address entered earlier.
- With a two-letter `countryCode`, Ekom leaves out providers whose zone has no countries. The amount for the provider ranges is the amount to pay without shipping and payment fees, as Ekom checks it at payment.
- After a `409`, do not submit the payment again by code. `canRetry` is always `false`. When the code is `paymentReviewRequired`, the customer has to contact the store.
- `230` with an empty body happens with a payment provider that sets its own redirect, such as Siminn Pay. The controller then returns an empty result so the redirect goes through.
- Any other failure, for example a payment provider package that is not installed, throws. The controller logs it and shows a general message. For an offline provider, Ekom sets the order status to `PaymentFailed` first.
- Ekom also has a built-in form endpoint for Razor views, `Html.BeginEkomCheckoutForm(CheckoutFormType.Pay)`, which posts to `/ekom/mvcCheckout/pay` and reports errors in the query string of the return URL. See [Submit payment](../guides/checkout.md#submit-payment).

## Show the receipt after payment

Set the payment provider's `successUrl` to your receipt page. Ekom adds `?orderId=` to it. The page shows the order only when the customer's basket cookie points to that order, so an order ID alone does not show anyone's receipt.

```csharp
using Ekom.API;
using Ekom.Models;
using Ekom.Utilities;

namespace MyShop.Storefront;

public enum ReceiptState
{
    NotFound,
    WaitingForPayment,
    Completed,
}

public sealed record Receipt(ReceiptState State, IOrderInfo? Order);

public static class ReceiptLookup
{
    // Finds the order in ?orderId= only when this customer's basket cookie points to it,
    // so knowing an order ID is not enough to see someone else's receipt.
    public static async Task<Receipt> FindAsync(
        Order orderApi,
        string storeAlias,
        Guid orderId,
        CancellationToken ct = default)
    {
        IOrderInfo? completed = await orderApi.GetCompletedOrderAsync(storeAlias, ct);
        if (completed is not null && completed.UniqueId == orderId)
        {
            return new Receipt(ReceiptState.Completed, completed);
        }

        // An online payment can return the customer before the gateway's callback has completed the order.
        IOrderInfo? basket = await orderApi.GetOrderAsync(storeAlias, ct);
        if (basket is not null
            && basket.UniqueId == orderId
            && basket.OrderStatus == OrderStatus.WaitingForPayment)
        {
            return new Receipt(ReceiptState.WaitingForPayment, basket);
        }

        return new Receipt(ReceiptState.NotFound, null);
    }
}
```

The receipt page:

```cshtml
@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage
@using Ekom.API
@using Ekom.Models
@using MyShop.Storefront
@inject Order OrderApi
@inject global::Ekom.API.Store Stores
@{
    // The payment provider's success URL points here and adds ?orderId=.
    IStore? store = Stores.GetStore();
    Receipt receipt = store is not null && Guid.TryParse(Context.Request.Query["orderId"].ToString(), out Guid orderId)
        ? await ReceiptLookup.FindAsync(OrderApi, store.Alias, orderId)
        : new Receipt(ReceiptState.NotFound, null);
}

@if (receipt is { State: ReceiptState.Completed, Order: not null })
{
    IOrderInfo order = receipt.Order;

    <h1>Thank you for your order</h1>
    <p>Your order number is @order.OrderNumber.</p>

    <table>
        @foreach (IOrderLine line in order.OrderLines)
        {
            <tr>
                <td>@line.Product.Title @line.Variant?.Title</td>
                <td>@decimal.ToInt32(line.Quantity)</td>
                <td>@line.Amount.AfterDiscount.CurrencyString</td>
            </tr>
        }
    </table>

    <p>
        Delivery: @(order.ShippingProvider?.Title ?? "none")<br />
        Payment: @order.PaymentProvider?.Title<br />
        Total: @order.GrandTotal.CurrencyString<br />
        Paid: @order.ChargedAmount.CurrencyString
    </p>
}
else if (receipt.State == ReceiptState.WaitingForPayment)
{
    <h1>We are confirming your payment</h1>
    <p>This page reloads by itself. Do not pay again.</p>
    <script>setTimeout(() => location.reload(), 5000);</script>
}
else
{
    <h1>We could not find this order</h1>
    <p>If you have paid, contact us with the email address you used.</p>
}
```

What to know:

- Arriving on the success page does not complete an order. An offline payment is completed before the redirect. An online payment is completed when the payment provider's callback records the payment, which can be after the customer arrives. Until then, the order status is `WaitingForPayment` and the page waits.
- `GetCompletedOrderAsync` returns the order the basket cookie points to, and only when its status is final. In a store with `UserBasket` turned on, a signed-in member gets their newest completed order in that store, until they start a new basket. `GetOrderAsync(storeAlias)` returns it while it is not final.
- Do not show `GetOrderAsync(orderId)` to whoever has the order ID. It returns any order and checks nothing.
- When the customer cancels at the gateway, Ekom sends them to the provider's `cancelUrl` instead, with the basket as it was. See [Cancel, edit and retry a payment](../guides/checkout.md#cancel-edit-and-retry-a-payment).

## Call the basket and checkout from a headless storefront

This module makes the same calls over Ekom's HTTP API, for a front end that does not render Umbraco views. Each function returns the basket or a message for the customer.

```js
// shop-api.js: the basket and checkout calls of a headless storefront.
// The site that runs Ekom. Serve the storefront from the same site, or proxy /ekom through it.
const EKOM = window.location.origin;
const STORE_ALIAS = 'Store';
const CULTURE = 'en-US';

// Every call sends cookies, so Ekom finds the customer's basket, and puts the
// store alias in the query string, so Ekom uses the right store on every route.
// POST bodies also carry storeAlias, because most order routes read it from there.
async function ekom(path, { method = 'GET', body } = {}) {
  const url = new URL(path, EKOM);
  url.searchParams.set('storeAlias', STORE_ALIAS);

  return fetch(url, {
    method,
    credentials: 'include',
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify({ storeAlias: STORE_ALIAS, ...body }),
  });
}

// Error bodies are JSON, plain text or empty, depending on the error.
async function readBody(response) {
  const text = await response.text();
  if (!text) return null;
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}

export async function getBasket() {
  const response = await ekom(`/ekom/order/storeAlias/${STORE_ALIAS}`);
  if (response.status === 404) return null; // No basket yet, or the order is completed.
  if (!response.ok) throw new Error(`Reading the basket failed: ${response.status}`);
  return response.json();
}

export async function addToBasket(productKey, variantKey, quantity = 1) {
  const response = await ekom('/ekom/order/add', {
    method: 'POST',
    body: { productId: productKey, variantId: variantKey, quantity, action: 'AddOrUpdate' },
  });
  if (response.ok) return { basket: await response.json() };

  const body = await readBody(response);
  switch (response.status) {
    case 400: return { error: body?.message ?? 'Choose an option before you add the product.' };
    case 404: return { error: 'This product is no longer for sale.' };
    // An empty body means too little stock; a body with a message means a checkout holds the basket.
    case 409: return { error: body?.message ?? 'We do not have that many in stock.' };
    case 429: return { error: 'Too many requests. Wait a minute and try again.' };
    default: return { error: 'Something went wrong. Try again.' };
  }
}

// quantity must be a whole number. 0 removes the line.
export async function setQuantity(lineKey, quantity) {
  const response = await ekom('/ekom/order/Updateorderlinequantity', {
    method: 'POST',
    body: { lineId: lineKey, quantity },
  });
  if (response.ok) return { basket: await response.json() };

  const body = await readBody(response);
  switch (response.status) {
    case 404: return { error: 'Your basket has changed. Reload it and try again.' };
    case 409: return { error: body?.message ?? 'We do not have that many in stock.' };
    default: return { error: 'Something went wrong. Try again.' };
  }
}

export async function removeLine(lineKey) {
  const response = await ekom('/ekom/order/removeorderline', {
    method: 'POST',
    body: { lineId: lineKey },
  });
  if (response.ok) return { basket: await response.json() };
  return { error: 'Your basket has changed. Reload it and try again.' };
}

export async function applyCoupon(code) {
  const response = await ekom('/ekom/order/coupon/apply', { method: 'POST', body: { coupon: code } });
  const body = await readBody(response);

  // 200 has an empty body. Read the basket again to show the new totals.
  switch (response.status) {
    case 200: return { applied: true };
    case 450: return { error: 'This code does not lower the price of your basket.' };
    case 400: return { error: body?.code === 'couponApplicationRejected' ? body.message : 'Enter a discount code.' };
    case 404: return { error: 'We do not recognise this code, or it cannot be used in this shop.' };
    case 406: return { error: 'This code has been used up.' };
    case 409: return { error: body?.message ?? 'Your basket cannot be changed right now.' };
    case 429: return { error: 'Too many attempts. Wait a minute and try again.' };
    default: return { error: 'The code could not be applied.' };
  }
}

export async function removeCoupon() {
  const response = await ekom('/ekom/order/coupon/remove', { method: 'POST' });
  return response.ok;
}

// fields: customerName, customerEmail, customerAddress, customerCity, customerZipCode,
// customerCountry (two letters), customerPhone, and shipping... fields for another address.
export async function saveCustomer(fields) {
  const response = await ekom('/ekom/order/updatecustomer', { method: 'POST', body: fields });
  if (response.ok) return { basket: await response.json() };

  const body = await readBody(response);
  return { error: body?.message ?? 'Check your details and try again.' };
}

// The amount Ekom checks provider ranges against: what is left to pay, without shipping and payment fees.
function amountForProviderRules(basket) {
  const fees = (basket.shippingProvider?.price?.value ?? 0) + (basket.paymentProvider?.price?.value ?? 0);
  return Math.max(0, basket.chargedAmount.value - fees);
}

// Ekom works out the basket amount for shipping providers itself.
export async function getShippingOptions(countryCode) {
  const response = await ekom(`/ekom/provider/shippingproviders/${STORE_ALIAS}?countryCode=${encodeURIComponent(countryCode)}`);
  if (!response.ok) throw new Error(`Reading delivery options failed: ${response.status}`);
  return response.json();
}

export async function chooseShipping(shippingProviderKey) {
  const response = await ekom('/ekom/order/updateshippingprovider', {
    method: 'POST',
    body: { shippingProvider: shippingProviderKey },
  });
  if (response.ok) return { basket: await response.json() };
  return { error: 'Choose one of the delivery options.' };
}

export async function getPaymentOptions(basket, countryCode) {
  const amount = amountForProviderRules(basket);
  const response = await ekom(
    `/ekom/provider/paymentsproviders/${STORE_ALIAS}?countryCode=${encodeURIComponent(countryCode)}&orderAmount=${amount}`);
  if (!response.ok) throw new Error(`Reading payment options failed: ${response.status}`);
  return response.json();
}

export async function pay(paymentProviderKey) {
  let response;
  try {
    response = await ekom(`/ekom/checkout/pay?culture=${CULTURE}`, {
      method: 'POST',
      body: { paymentProvider: paymentProviderKey, culture: CULTURE },
    });
  } catch {
    // Also what you get when an offline payment redirects to a success URL on another origin.
    return { error: 'We could not reach the shop. Check your email for an order confirmation before you try again.' };
  }

  // Offline payment: Ekom completed the order and redirected to the provider's success URL,
  // which carries ?orderId=. fetch followed the redirect, so send the browser there too.
  if (response.redirected) {
    window.location.assign(response.url);
    return { done: true };
  }

  const body = await readBody(response);
  switch (response.status) {
    case 230:
      // Online payment: the body is a JSON string with the provider's HTML,
      // usually a form that sends the customer on to the payment gateway.
      document.open();
      document.write(body);
      document.close();
      return { done: true };
    case 400:
      if (body?.code === 'invalidShippingProvider') return { error: body.message, step: 'delivery' };
      return { error: 'Fill in your name and email address.', step: 'details' };
    case 404:
      return { error: 'Your basket is empty, or the payment option no longer exists.', step: 'basket' };
    case 409:
      // canRetry is always false. Never submit the payment again by yourself.
      return { error: body?.message ?? 'Checkout is not possible right now.', step: 'basket' };
    case 530:
      // This body uses PascalCase: OrderLineKey and IsVariant.
      return { error: 'A product in your basket is no longer in stock in that quantity.', lineKey: body?.OrderLineKey, step: 'basket' };
    case 429:
      return { error: 'Too many attempts. Wait a minute and try again.' };
    default:
      return { error: 'We could not start the payment. Try again.' };
  }
}
```

What the client must send:

| What | Why |
| --- | --- |
| Cookies on every request (`credentials: 'include'`) | Ekom finds the basket by the cookie `ekmOrder-{StoreAlias}`, or `ekmOrder` when the store shares its basket. With `UserBasket` on, it uses the signed-in member's basket, so the member's login cookie must go too. |
| `storeAlias` in the query string | For routes under `/ekom`, Ekom finds the store from a `storeAlias` query value, a URL-encoded form field or a header with that name, or the `StoreInfo` cookie it sets. It does not read a JSON body for this. Routes such as `GET /ekom/order` and the provider lookups use that store. |
| `storeAlias` in the body or route | The basket, coupon, customer and provider routes read the store alias from their own body or route. Without it some return `400`. |

What to know:

- The basket cookie is `SameSite=Lax`. Browsers do not send it with a `fetch` from another site, so serve the storefront from the same site as Umbraco, or proxy `/ekom` through the storefront's host. For another origin on the same site, set up CORS with credentials in the host application. Ekom does not set up CORS.
- The cookie is not `HttpOnly`, is `Secure` only on HTTPS, and lasts `Ekom:BasketCookieLifetime` days, 1 by default.
- The first `POST /ekom/order/add` creates the basket and sets the cookie. Before that, `GET /ekom/order/storeAlias/Store` returns `404`.
- The coupon routes return an empty body. Read the basket again to show the new totals. The other changes return the updated basket.
- A `409` from a basket change has no body when stock is short, and a `CheckoutStateError` body when a checkout holds the basket.
- `Updateorderlinequantity` takes a whole number. A decimal quantity is rejected.
- For an offline payment, Ekom answers with a redirect to the success URL, and `fetch` follows it. When that URL is on another origin without CORS, `fetch` fails even though the order is completed. Keep the success page on the same origin.
- The `530` body uses `OrderLineKey` and `IsVariant`, in PascalCase. The other responses use camelCase.
- A headless receipt page can read the order with `GET /ekom/order/{orderId}`, but that route returns any order to anyone who has the ID. Protect it in your host or proxy. See [Read a completed order safely](../guides/orders-and-baskets.md#read-a-completed-order-safely).
- The routes have rate limits per IP address and answer `429` over them. See [Errors, limits and security](../guides/headless.md#errors-limits-and-security).

## Related pages

- [Catalog and products](../guides/catalog-and-products.md): product queries, variants and what the catalog returns.
- [Orders and baskets](../guides/orders-and-baskets.md): how the basket is found, changed and priced.
- [Checkout](../guides/checkout.md): what happens when the customer pays, and every checkout error.
- [Payment and shipping providers](../guides/providers.md): how providers are filtered by country and amount.
- [Discount APIs and behavior](../guides/discounts/developers.md): coupon rules and the rejection hook.
- [Headless storefronts](../guides/headless.md) and the [HTTP API reference](../reference/http-api.md): every route the JavaScript example calls.

[Back to Examples](README.md)

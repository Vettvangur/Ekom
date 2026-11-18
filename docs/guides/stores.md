# Stores

[Ekom docs](../README.md) / [Guides](README.md) / Stores

A store is one shop inside Ekom. Each store has its own catalog data, prices, discounts, payment and shipping providers, order numbers, basket, cultures and currencies. This page shows how to get the right store in your code, how Ekom picks the store for a request, and which settings decide what stores share.

Each store is an `ekmStore` node in Umbraco. The store alias is the name of that node, for example `Store` or `IS`. Most Ekom methods take a store alias.

In storefront code that runs inside a web request, Ekom works out the store for you. In background jobs, message handlers, imports and any other code without a storefront request, pass the store alias yourself.

On this page:

- [Get a store in your code](#get-a-store-in-your-code)
- [How Ekom finds the store for a request](#how-ekom-finds-the-store-for-a-request)
- [Change the store for the current request](#change-the-store-for-the-current-request)
- [Cultures](#cultures)
- [Currencies](#currencies)
- [Countries](#countries)
- [Choose what stores share](#choose-what-stores-share)
- [Refresh the caches](#refresh-the-caches)

## Get a store in your code

Inject `Ekom.API.Store`, or use `Store.Instance` where injection is not possible. Use the method that fits where your code runs.

```csharp
using Ekom.API;
using Ekom.Models;

public sealed class StoreContext(Store stores)
{
    // The store of the current request, or the first store.
    public IStore? Current() => stores.GetStore();

    // One store by alias. Throws StoreNotFoundException if the alias is unknown.
    public IStore ByAlias(string alias) =>
        stores.GetStore(alias)
        ?? throw new InvalidOperationException($"Store '{alias}' was not found.");

    // All stores, ordered by their sort order in Umbraco.
    public IEnumerable<IStore> All() => stores.GetAllStores();
}
```

| Method | Returns | When nothing matches |
| --- | --- | --- |
| `GetStore()` | The store Ekom resolved for the current request. Without one, the first store by `SortOrder`. | `null` if there are no stores. |
| `GetStore(alias)` | The store with that alias. Upper and lower case are ignored. | Throws `StoreNotFoundException`. It also throws when there are no stores at all. |
| `GetAllStores()` | Every store, ordered by `SortOrder`. | An empty list. |

`GetStore()` falls back to the first store when the request has no store. On a site with one store that is what you want. On a site with several stores it can hide a missing store in a request, so use `GetStore(alias)` whenever your code already knows which store it works on.

HTTP clients can read the list of stores from `GET /ekom/api/stores`. See the [HTTP API reference](../reference/http-api.md#general-data).

## How Ekom finds the store for a request

For normal storefront pages, Ekom middleware picks the store from the host name and the first segment of the request path. It matches them against the domains (culture and hostnames) set up in Umbraco. A domain belongs to a store when it is set on the store's root content node (`storeRootNode` on the `ekmStore` node).

A domain can be:

- a host, such as `shop.example.com`
- a host and a first path segment, such as `shop.example.com/en`
- a first path segment only, such as `/en`

To find a store outside the normal request flow, call `GetStoreByDomain`:

```csharp
IStore? store = stores.GetStoreByDomain("shop.example.com/en", "en-US");
```

Ekom looks for a matching domain in this order: host and first segment, then host only, then the first segment on its own. For the first domain that matches, it picks a store with the same root node and the culture you passed. If no store has that culture, it takes any store with the same root node. If you pass an empty culture, Ekom uses the culture of the matched domain.

If no domain matches, `GetStoreByDomain` returns the first store. It throws an exception only when there are no stores at all. When your routing must never fall back to another store, check the result.

`stores.GetDomains()` returns every domain Ekom has cached, as `UmbracoDomain` values. `IStore.Domains` holds the domains whose root node is the store's root node (`StoreRootNodeId`).

Ekom's own API routes (under `/ekom/`) find the store differently from storefront pages. Pass `storeAlias` in the route, query, form or request body where the endpoint documents it. There is no single store header that every endpoint reads. See [HTTP API security and request context](../reference/http-api.md#security-and-request-context).

## Change the store for the current request

`SetStore(alias)` sets the store for the rest of the current HTTP request and returns that store:

```csharp
IStore? selected = stores.SetStore("Store");
```

- Ekom calls that use the current store later in the same request now use this store.
- An empty alias returns `null` and changes nothing.
- An unknown alias throws `StoreNotFoundException`.
- Without an `HttpContext`, the method returns the store but has no request to change.

`SetStore` does not change the domains in Umbraco, does not save a choice for the customer, and does not change the request culture.

Do not call `SetStore` in background work to create a "current" store. Pass the alias to the catalog, order, provider, stock and discount methods instead.

## Cultures

`IStore.Cultures` lists every culture of the store. `IStore.Culture` is the culture to use for the current request. Ekom chooses it like this:

1. It reads the request culture from ASP.NET Core (`IRequestCultureFeature`).
2. If that culture is one of the store's `Cultures`, Ekom uses it.
3. Otherwise Ekom uses the first culture in `Cultures`.
4. If `Cultures` is empty, Ekom uses `en-US`.

```csharp
IStore store = stores.GetStore("Store")!;

string currentCulture = store.Culture.Name;
List<CultureInfoDto> supportedCultures = store.Cultures;
string pathPrefix = store.UrlPrefix(currentCulture);
```

Cultures are `CultureInfoDto` values. They hold the culture name and ISO language data, so Ekom does not serialize `System.Globalization.CultureInfo` directly.

`UrlPrefix(culture)` returns the store's URL prefix for that culture. If the prefix is set, Ekom makes sure it starts with `/` and does not end with `/`. If it is not set, you get an empty value.

The store and the request culture are separate. Choosing a store does not set the ASP.NET Core request culture. Set up localization and Umbraco domains in your site so that the request culture Ekom sees is the one you want.

## Currencies

`IStore.Currencies` lists the store's currencies as `CurrencyModel` values. `IStore.Currency` and `GetCurrentCurrency()` return the currency to use now. Ekom chooses it in this order:

1. The `EkomCurrency-{StoreAlias}` cookie, if its value matches the `CurrencyValue` of one of the store's currencies.
2. The currency whose `CurrencyValue` matches the name of the current request culture.
3. The first currency of the store.

`CurrencyValue` is a culture name such as `en-US` or `is-IS`, not an ISO currency code such as `USD`. Ekom uses it to choose the currency. When .NET knows that culture, Ekom takes the ISO currency code (`ISOCurrencySymbol`), the currency symbol, the number of decimals and the format from it.

To let a customer change currency over HTTP, post the `CurrencyValue`:

```http
POST /ekom/order/currency?currency=en-US
```

This endpoint works on the store of the current request. It does two things:

- If the customer has a basket, it changes the basket's currency and recalculates it.
- It writes the `EkomCurrency-{StoreAlias}` cookie. The cookie lasts 360 days.

It does not create a basket when there is none. Use a `CurrencyValue` that the store has. If you post another value, the cookie is still written, but Ekom ignores it and falls back to the next rule in the list above.

In C#, `Order.UpdateCurrencyAsync(currency, orderId, storeAlias, ct)` changes the currency of a saved order. It does not write the cookie. If you build your own currency switcher, keep the cookie and the order currency in step yourself.

Prices of products are covered in [Catalog and products](catalog-and-products.md). The price types that Ekom returns are described in [Models and behavior](../reference/models-and-behavior.md#prices).

## Countries

Countries do not come from the store's culture or currency. Ekom keeps a separate country list. Its main use is to filter payment and shipping providers by the zones they are set up for.

```csharp
IEnumerable<Country> countries = providers.GetAllCountries();
```

`providers` is `Ekom.API.Providers`. HTTP clients can call `GET /ekom/api/countries`. Each `Country` has a `Name` and a `Code`.

The provider methods filter by country only when you pass a two-letter code, such as `IS` or `DE`. An empty value, or a value of another length, does not filter by country. See [Payment and shipping providers](providers.md#how-ekom-filters-providers-by-country-and-amount).

## Choose what stores share

Several settings decide whether stores keep data apart or share it. Each one works on its own.

| Setting | Where | Default | Effect |
| --- | --- | --- | --- |
| `userBasket` | Store node | `Ekom:UserBasket`, which is `false` | `true`: a signed-in member has one basket, saved on the member (in `orderId`) instead of in a cookie. |
| `shareBasketBetweenStores` | Store node | `Ekom:ShareBasket`, which is `false` | `true`: the basket cookie is called `ekmOrder` for every store, instead of `ekmOrder-{StoreAlias}`. |
| `applyVatOnShipping` | Store node | `Ekom:ApplyVatOnShipping`, which is `false` | Whether VAT is added to shipping. |
| `Ekom:PerStoreStock` | appsettings | `false` | `true`: each store has its own stock count. `false`: one count per product for all stores. See [Stock](stock.md). |
| `Ekom:GlobalCatalog` | appsettings | `false` | `true`: looking up a product or category by key, ID or SKU can return one from another store. Looking up by route never leaves the store. See [Catalog and products](catalog-and-products.md#find-a-product-or-category). |

A value set on the store node wins. When the store node does not have the property, Ekom uses the appsettings value.

Ekom keeps products, categories, discounts, payment providers and shipping providers in a separate cache for each store, even when the content behind them is shared. Carry the store alias through every step of a multi-store flow. A product or provider that Ekom found for one store is not automatically valid in another.

Sharing a basket only changes the name of the cookie that finds it. Ekom does not merge orders, does not convert currencies, and does not check that every store can sell every line already in the basket. Turn it on only for stores that use the same currencies and can sell the same products. See [Orders and baskets](orders-and-baskets.md#how-ekom-finds-the-current-basket) for how Ekom finds a basket.

All application settings are listed in the [Configuration reference](../reference/configuration-reference.md).

## Refresh the caches

Ekom reads stores and domains from memory. The catalog, discounts and providers of each store are in memory too. Umbraco tells Ekom when content changes, so you do not normally need to refresh anything.

To rebuild every Ekom cache, call:

```csharp
stores.RefreshCache();
```

`RefreshCache()` rebuilds all of Ekom's caches, for every store. It is a heavy operation. Do not call it on normal reads or on every request. Use it after bulk or code changes to content when Umbraco's change notifications did not reach Ekom.

Backoffice users can trigger the same rebuild with `POST /ekom/backoffice/Cache`. That route needs a signed-in Umbraco backoffice user, and Ekom logs who started the rebuild. It is not meant for storefront code.

## Related pages

- [Catalog and products](catalog-and-products.md): read products and categories for a store.
- [Orders and baskets](orders-and-baskets.md): how the basket for a store is found and changed.
- [Payment and shipping providers](providers.md): filter providers by store and country.
- [Headless storefronts](headless.md): pass the store alias from a client that talks to Ekom over HTTP.
- [.NET API reference](../reference/dotnet-api.md#store): every method on `Ekom.API.Store`.
- [Configuration reference](../reference/configuration-reference.md): all `Ekom` settings.

[Back to Guides](README.md)

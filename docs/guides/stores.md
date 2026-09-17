# Stores

A store is the boundary Ekom uses for catalog data, prices, discounts, providers, order numbering, basket behavior, cultures, and currencies. In request-bound storefront code Ekom can resolve the active store for you. In jobs, message handlers, imports, and other code without a storefront request, pass a store alias explicitly.

## Read stores

Inject `Ekom.API.Store` and use the method that matches the context:

```csharp
using Ekom.API;
using Ekom.Models;

public sealed class StoreContext(Store stores)
{
    public IStore? Current() => stores.GetStore();

    public IStore ByAlias(string alias) =>
        stores.GetStore(alias)
        ?? throw new InvalidOperationException($"Store '{alias}' was not found.");

    public IEnumerable<IStore> All() => stores.GetAllStores();
}
```

`GetStore()` first uses the store attached to Ekom's current `ContentRequest`. If the request has no resolved store, it returns the first configured store by `SortOrder`; if there are no stores, it returns `null`. This fallback is convenient for a single-store site but can hide missing context in a multi-store application.

`GetStore(alias)` performs a case-insensitive alias lookup. An empty or unknown alias throws `StoreNotFoundException` when stores exist, rather than silently selecting another store. Prefer this overload whenever the caller already knows the target store.

`GetAllStores()` returns cached stores ordered by `SortOrder`. HTTP clients can read the same collection from `GET /ekom/api/stores`; see the [HTTP API reference](../reference/http-api.md#general-data).

## Domain and request resolution

For ordinary storefront routes, Ekom middleware resolves a store from the request host and first path segment. Domain records are supplied by Umbraco and associated with a store through the store's root content node. Domain matching supports host mappings such as `shop.example.com`, host-and-first-segment mappings, and relative first-segment mappings.

Use the domain API when resolving a store outside the normal middleware path:

```csharp
IStore? store = stores.GetStoreByDomain(
    "shop.example.com/en",
    "en-US");
```

The resolver first finds the cached Umbraco domain, then prefers a store with the same root node and requested culture. If that culture-specific match is absent, it uses a store with the same root node. If no domain mapping resolves, the current implementation falls back to the first configured store; it throws only when the store cache is empty. Validate the result when routing must never fall back.

`stores.GetDomains()` returns all cached `UmbracoDomain` mappings. Each `IStore.Domains` collection contains mappings whose root content ID matches that store's `StoreRootNodeId`.

Ekom API routes resolve store context differently from normal storefront pages. Supply `storeAlias` in the route, query, form, or request model where that operation documents it. There is no universal store header contract across the public controllers. See [HTTP API security and request context](../reference/http-api.md#security-and-request-context).

## Change the current request store

`SetStore(alias)` replaces the active store in Ekom's request context and returns the selected store:

```csharp
IStore? selected = stores.SetStore("Store");
```

This affects later Ekom operations in the same HTTP request. It does not change Umbraco domain configuration, persist a customer preference, or rewrite the request culture. With no `HttpContext`, the method can resolve and return the store but has no request context to update.

For background work, do not call `SetStore` to create ambient state. Pass the alias to catalog, order, provider, stock, and discount methods instead.

## Cultures

`IStore.Cultures` is the complete culture list for the store. `IStore.Culture` is the effective culture for the current request:

1. Ekom reads ASP.NET Core's `IRequestCultureFeature`.
2. If that culture name exists in `Cultures`, Ekom returns it.
3. Otherwise Ekom returns the first configured culture.
4. If the resulting culture list is empty, the model has a final `en-US` culture DTO fallback.

```csharp
IStore store = stores.GetStore("Store")!;

string currentCulture = store.Culture.Name;
IReadOnlyList<CultureInfoDto> supportedCultures = store.Cultures;
string pathPrefix = store.UrlPrefix(currentCulture);
```

Culture DTOs expose names and ISO language metadata without serializing `System.Globalization.CultureInfo` directly. `UrlPrefix(culture)` normalizes a configured non-empty prefix to start with `/` and not end with `/`.

Store selection and request culture are related but separate. Selecting a store does not itself set ASP.NET Core request localization. Configure localization and Umbraco domains in the host application so the request culture presented to Ekom is the intended one.

## Currencies

`IStore.Currencies` lists the store's configured `CurrencyModel` values. `IStore.Currency` and `GetCurrentCurrency()` resolve the active value in this order:

1. A valid `EkomCurrency-{StoreAlias}` cookie whose value matches `CurrencyModel.CurrencyValue`.
2. A configured currency whose `CurrencyValue` matches the current request culture name.
3. The first configured currency.

`CurrencyValue` is the configured culture/region value used for selection. `ISOCurrencySymbol`, `CurrencySymbol`, decimal digits, and formatting are derived from it when .NET can resolve that region.

The HTTP currency operation updates an existing basket, when present, and writes the store-specific currency cookie:

```http
POST /ekom/order/currency?currency=en-US
```

Use the exact configured `CurrencyValue`. The endpoint does not create a basket when none exists. In C#, `Order.UpdateCurrencyAsync(currency, orderId, storeAlias)` updates the persisted order but does not write the browser cookie, so application code that exposes its own currency selector must keep browser selection and order currency coordinated.

Changing currency recalculates the order against the selected store currency. Product price behavior is covered in [Catalog and products](catalog-and-products.md), and the returned price contracts are described in [Models and behavior](../reference/models-and-behavior.md#prices).

## Countries and provider availability

Countries are not inferred from the current store culture or currency. They are a separate Ekom country list used primarily when filtering shipping and payment providers by configured zones.

```csharp
IEnumerable<Country> countries = providers.GetAllCountries();
```

HTTP clients can call `GET /ekom/api/countries`. Country records contain `Name` and a country `Code`. Provider collection methods apply country filtering only for a two-letter code; an empty or differently sized value does not filter by country. See [Payment and shipping providers](providers.md#availability-constraints).

## Multi-store behavior

Several settings decide whether data is isolated or shared. Treat them independently:

| Setting | Effect |
| --- | --- |
| Store `UserBasket` | Uses the authenticated member's saved `orderId` instead of an order cookie when a signed-in member is available. Falls back to `Ekom:UserBasket`. |
| Store `ShareBasketBetweenStores` | Uses the shared `ekmOrder` cookie name instead of `ekmOrder-{StoreAlias}`. Falls back to `Ekom:ShareBasket`. Shared stores must use compatible currencies and catalog/order assumptions. |
| Store `ApplyVatOnShipping` | Controls shipping VAT behavior and falls back to `Ekom:ApplyVatOnShipping`. |
| `Ekom:PerStoreStock` | Separates primary stock identities by store alias. When false, primary stock is global. |
| `Ekom:GlobalCatalog` | Allows eligible catalog key/ID lookups to fall back to another store. Route lookups remain in the selected store. |

Products, categories, discounts, payment providers, and shipping providers are represented by per-store caches even when their source content overlaps. Always carry the selected alias through a multi-store flow; do not assume that a product or provider resolved for one store is valid in another.

Basket sharing changes only how the basket identifier is named and found. It does not merge orders, convert currencies, or validate that every store can sell every existing line. Enable it only where the participating stores are deliberately compatible. See [Orders and baskets](orders-and-baskets.md#basket-identity-and-resolution) for the basket rules.

The complete application-setting list is in [Configuration reference](../reference/configuration-reference.md).

## Refresh caches

Store and domain reads come from Ekom caches, as do the per-store catalog, discount, and provider models. A full administrative refresh is available through:

```csharp
stores.RefreshCache();
```

`RefreshCache()` runs Ekom's cache initializer in forced-refresh mode. It is a broad administrative operation, not something to call on normal reads or every request. Use it after bulk or programmatic content changes when the normal Umbraco cache notifications are not sufficient, and expect all dependent per-store caches to be rebuilt.

Ekom also exposes `POST /ekom/backoffice/Cache` for its authenticated Umbraco backoffice integration. That route requires a backoffice session and is not a storefront cache endpoint. This guide intentionally does not prescribe a backoffice editing workflow; store and domain authoring depends on the installed Umbraco/Ekom version and the site's content model.

## Related reference

- [.NET API](../reference/dotnet-api.md#store)
- [HTTP API](../reference/http-api.md)
- [Models and behavior](../reference/models-and-behavior.md#istore)
- [Configuration reference](../reference/configuration-reference.md)
- [Headless storefronts](headless.md)

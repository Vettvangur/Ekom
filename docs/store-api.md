# Store API

`Ekom.API.Store` is the main C# entry point for reading the current store context, resolving stores directly, and switching the active store for the current request.

## Example

```csharp
using Ekom.API;
using Ekom.Models;

public sealed class StoreApplicationService
{
    private readonly Store _store;

    public StoreApplicationService(Store store)
    {
        _store = store;
    }

    public IStore? GetCurrentStore()
    {
        return _store.GetStore();
    }
}
```

## When to use `API.Store`

Use `API.Store` when you want to:

- get the current store for the active request
- resolve a specific store by alias
- resolve a store from domain and culture information
- list all configured stores
- switch the active store for the current request
- refresh Ekom store-related cache data

## Injecting `Store`

The usual way to work with `API.Store` is to inject it.

```csharp
using Ekom.API;

public sealed class StoreApplicationService
{
    private readonly Store _store;

    public StoreApplicationService(Store store)
    {
        _store = store;
    }
}
```

You can also access the static instance:

```csharp
var storeApi = Store.Instance;
```

In most application code, constructor injection is the better option.

## Methods

### `GetStore()`

```csharp
IStore? store = _store.GetStore();
```

Returns the current store for the active request context.

If no request store has been set, Ekom falls back to the first available store.

### `GetStore(string? storeAlias)`

```csharp
IStore? store = _store.GetStore("Store");
```

Returns a store by alias.

Use this when you already know which store you want to work with.

If the alias does not match a configured store, Ekom throws a store not found exception.

### `GetStoreByDomain(string domain, string culture)`

```csharp
IStore? store = _store.GetStoreByDomain("example.com", "en-US");
```

Resolves a store from domain and culture information.

Use this when store resolution should be based on incoming host/domain data instead of the current request store.

If no matching domain is found, Ekom falls back to the first available store.

### `GetAllStores()`

```csharp
IEnumerable<IStore> stores = _store.GetAllStores();
```

Returns all configured stores.

Stores are returned ordered by their configured sort order.

### `GetDomains()`

```csharp
IEnumerable<UmbracoDomain> domains = _store.GetDomains();
```

Returns the Umbraco domains known to Ekom.

Use this when you need to inspect or work with the configured store/domain mapping.

### `SetStore(string storeAlias)`

```csharp
IStore? store = _store.SetStore("Store");
```

Sets the active store for the current request.

Use this when you need to explicitly switch the store context before calling other Ekom APIs.

If the store alias is empty, the method returns `null`.

### `RefreshCache()`

```csharp
_store.RefreshCache();
```

Refreshes the Ekom caches related to stores and dependent data.

This also refreshes stock cache data and coupon cache data.

Use this in operational or maintenance scenarios, not as part of normal request flow.

## Notes

- `GetStore()` works with the current request context when one is available.
- `SetStore(...)` changes the active request store, which can affect later Ekom API calls in the same request.
- `RefreshCache()` is heavier than a normal lookup and should be used carefully.

## Related pages

- [Stores Overview](stores-overview.md)
- [Configuration](configuration.md)
- [Catalog API](catalog-api.md)
- [Order API](api-order-reference.md)

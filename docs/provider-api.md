# Provider API

`Ekom.API.Providers` is the main C# entry point for reading payment providers, shipping providers, countries, and zones.

## Example

```csharp
using Ekom.API;
using Ekom.Models;

public sealed class ProviderApplicationService
{
    private readonly Providers _providers;

    public ProviderApplicationService(Providers providers)
    {
        _providers = providers;
    }

    public async Task<IReadOnlyList<IPaymentProvider>> GetPaymentProvidersAsync(CancellationToken ct)
    {
        return await _providers.GetPaymentProvidersAsync(
            store: "Store",
            countryCode: "IS",
            orderAmount: 10000,
            ct: ct);
    }
}
```

## When to use `API.Providers`

Use `API.Providers` when you want to:

- list available payment providers
- list available shipping providers
- filter providers by store, country, or order amount
- resolve a single provider by key
- read configured countries or zones

Use `API.Order` when you want to assign a selected provider to an order.

## Injecting `Providers`

The usual way to work with `API.Providers` is to inject it.

```csharp
using Ekom.API;

public sealed class ProviderApplicationService
{
    private readonly Providers _providers;

    public ProviderApplicationService(Providers providers)
    {
        _providers = providers;
    }
}
```

You can also access the static instance:

```csharp
var providersApi = Providers.Instance;
```

In most application code, constructor injection is the better option.

## Provider collections

### `GetShippingProvidersAsync(string? store = null, string? countryCode = null, decimal orderAmount = 0, CancellationToken ct = default)`

```csharp
IReadOnlyList<IShippingProvider> providers = await _providers.GetShippingProvidersAsync(
    store: "Store",
    countryCode: "IS",
    orderAmount: 10000,
    ct: ct);
```

Returns shipping providers for the resolved store.

Optional filters:

- `store`: store alias. If empty, Ekom uses the current request store.
- `countryCode`: two-letter country code. When provided, providers are filtered by zones that contain the country.
- `orderAmount`: when greater than `0`, providers are filtered by their configured amount range.

Provider events can modify the returned shipping provider list before it is returned.

### `GetPaymentProvidersAsync(string? store = null, string? countryCode = null, decimal orderAmount = 0, CancellationToken ct = default)`

```csharp
IReadOnlyList<IPaymentProvider> providers = await _providers.GetPaymentProvidersAsync(
    store: "Store",
    countryCode: "IS",
    orderAmount: 10000,
    ct: ct);
```

Returns payment providers for the resolved store.

Optional filters:

- `store`: store alias. If empty, Ekom uses the current request store.
- `countryCode`: two-letter country code. When provided, providers are filtered by zones that contain the country.
- `orderAmount`: when greater than `0`, providers are filtered by their configured amount range.

Provider events can modify the returned payment provider list before it is returned.

### `GetPaymentProvidersAsync(string? store = null, string? countryCode = null, decimal orderAmount = 0)`

```csharp
await foreach (IPaymentProvider provider in _providers.GetPaymentProvidersAsync("Store"))
{
    // use provider
}
```

Returns payment providers as an async enumerable.

Prefer the `Task<IReadOnlyList<IPaymentProvider>>` overload when you want cancellation support.

## Single provider lookup

### `GetShippingProviderAsync(Guid key, IStore? store = null, CancellationToken ct = default)`

```csharp
IShippingProvider? provider = await _providers.GetShippingProviderAsync(
    shippingProviderKey,
    store,
    ct);
```

Returns a shipping provider by key for a specific store instance.

If `store` is `null`, Ekom uses the current request store.

### `GetShippingProviderAsync(Guid key, string? storeAlias = null, CancellationToken ct = default)`

```csharp
IShippingProvider? provider = await _providers.GetShippingProviderAsync(
    shippingProviderKey,
    "Store",
    ct);
```

Returns a shipping provider by key for a store alias.

If `storeAlias` is empty, Ekom uses the current request store.

### `GetPaymentProviderAsync(Guid key, IStore? store = null, CancellationToken ct = default)`

```csharp
IPaymentProvider? provider = await _providers.GetPaymentProviderAsync(
    paymentProviderKey,
    store,
    ct);
```

Returns a payment provider by key for a specific store instance.

If `store` is `null`, Ekom uses the current request store.

### `GetPaymentProviderAsync(Guid key, string? storeAlias = null, CancellationToken ct = default)`

```csharp
IPaymentProvider? provider = await _providers.GetPaymentProviderAsync(
    paymentProviderKey,
    "Store",
    ct);
```

Returns a payment provider by key for a store alias.

If `storeAlias` is empty, Ekom uses the current request store.

## Countries and zones

### `GetAllCountries()`

```csharp
IEnumerable<Country> countries = _providers.GetAllCountries();
```

Returns all countries known to Ekom.

This method is sync-only.

### `GetAllZones()`

```csharp
IEnumerable<IZone> zones = _providers.GetAllZones();
```

Returns all configured zones known to Ekom.

This method is sync-only.

## Notes

- This page documents async methods where async methods exist.
- Sync-only methods are included where there is no async alternative.
- Provider collection methods are filtered by store, country, and order amount before provider events run.
- `countryCode` filtering only applies when a two-letter country code is provided.
- `orderAmount` filtering only applies when the amount is greater than `0`.
- A missing store context can result in an exception when a store alias cannot be resolved.

## Related pages

- [Provider Events](provider-events.md)
- [Payment Provider Selection](payment-provider-selection.md)
- [Shipping Provider Selection](shipping-provider-selection.md)
- [Payment Providers Overview](payment-providers-overview.md)
- [Shipping Providers Overview](shipping-providers-overview.md)
- [Order API](api-order-reference.md)

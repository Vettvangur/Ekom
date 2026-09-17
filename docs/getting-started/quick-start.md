# Quick Start

This walkthrough establishes a small, current Ekom API flow: install Ekom, create a store, resolve a product, add it to the current cart, and read the cart. It is not a production checkout or payment implementation.

## 1. Install and start Ekom

Install the package pair for the site's Umbraco version as described in [Installation](installation.md). Start the site, sign in to Umbraco, and create a store before adding products, providers, or checkout behavior.

Ekom's composer registers the public APIs with dependency injection, so application services can request them directly.

```csharp
using Ekom.API;

public sealed class BasketService
{
    private readonly Catalog _catalog;
    private readonly Order _order;

    public BasketService(Catalog catalog, Order order)
    {
        _catalog = catalog;
        _order = order;
    }
}
```

## 2. Load a product and update the current order

Use a published store alias and product key from the current environment. Resolving the product first gives the application a clear not-found path. `AddOrderLineAsync` validates the store, product, variants, and stock according to the active configuration.

```csharp
var product = _catalog.GetProduct(productKey, storeAlias);

if (product is null)
{
    return;
}

var currentOrder = await _order.AddOrderLineAsync(
    product.Key,
    quantity: 1,
    storeAlias: storeAlias,
    ct: ct);
```

`Order` represents the current request's order context. The returned `IOrderInfo` contains the updated lines and calculated totals. Products that require a variant need the appropriate `AddOrderSettings`; do not use this minimal call to bypass variant selection.

## 3. Read the current order

```csharp
var currentOrder = await _order.GetOrderAsync(storeAlias, ct);

if (currentOrder is null)
{
    return;
}
```

The overload without a store alias resolves the store from the current request. Passing the alias explicitly is clearer in application services and background-aware code. The returned order supplies lines, totals, selected providers, and customer data.

## 4. Continue into checkout

Provider updates use the current API's information methods and require the submitted checkout fields:

```csharp
var shippingData = new Dictionary<string, string>();
currentOrder = await _order.UpdateShippingInformationAsync(
    shippingProviderId,
    storeAlias,
    shippingData,
    ct: ct);

var paymentData = new Dictionary<string, string>();
currentOrder = await _order.UpdatePaymentInformationAsync(
    paymentProviderId,
    storeAlias,
    paymentData,
    ct: ct);
```

Real providers can require values in those dictionaries. Obtain available providers through `Ekom.API.Providers`, validate that each provider belongs to the current store and order, and follow the selected payment provider's redirect or callback flow.

`CompleteOrderAsync(orderId, ct)` exists for the trusted completion path. Call it only after payment has been verified or from the provider success integration. Never expose it as an unauthenticated storefront shortcut. Production checkout must also handle validation failures, idempotency, stock or reservation failures, provider errors, and declined or repeated callbacks.

## Next steps

- Review [Configuration](configuration.md).
- Learn how the package is organized in [Architecture overview](../architecture/overview.md).
- Use [Samples](../samples/overview.md) as reference implementations, not production starters.
- Browse the repository [README](../../README.md) and the existing `docs/` guides for checkout and API-specific topics.

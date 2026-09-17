# Payment and shipping providers

Providers are Umbraco content resolved per store and exposed through `Ekom.API.Providers`. A shipping provider contributes shipping price and data to an order. A payment provider selects either an offline path or an implementation supplied by the Ekom payments package.

## Query providers

```csharp
using Ekom.API;
using Ekom.Models;

IReadOnlyList<IShippingProvider> shipping = await providers.GetShippingProvidersAsync(
    store: "Store",
    countryCode: "IS",
    orderAmount: order.OrderLineTotal.Value,
    ct: ct);

IReadOnlyList<IPaymentProvider> payments = await providers.GetPaymentProvidersAsync(
    store: "Store",
    countryCode: "IS",
    orderAmount: order.ChargedAmount.Value,
    ct: ct);

IShippingProvider? selectedShipping = await providers.GetShippingProviderAsync(
    shippingKey,
    "Store",
    ct);

IPaymentProvider? selectedPayment = await providers.GetPaymentProviderAsync(
    paymentKey,
    "Store",
    ct);
```

If `store` is omitted, Ekom resolves it from the current request. Code without a storefront request should always pass the alias.

## Availability constraints

Collection methods apply constraints before raising provider events:

- A two-character `countryCode` retains providers whose configured zone contains that upper-case country code.
- An `orderAmount` greater than zero must be at least `StartRange` and no greater than `EndRange`; `EndRange = 0` means no upper limit.
- Results are ordered by provider `SortOrder`.
- Empty or non-two-character country values do not apply country filtering.
- `orderAmount = 0` does not apply amount filtering.

`GetAllCountries()` returns Ekom's country list and `GetAllZones()` returns configured zones.

## Configure payment providers

Payment providers are created below the store's payment-provider container in Umbraco. Relevant built-in fields include:

| Field | Behavior |
| --- | --- |
| `basePaymentProvider` | Name used to resolve the runtime `Ekom.Payments.IPaymentProvider`. When empty, the node name is used. |
| `offlinePayment` | Completes without an external provider request. |
| `successUrl` | Redirect target after successful payment. |
| `errorUrl` | Redirect target after an error. |
| `cancelUrl` | Redirect target after cancellation. |
| `language` | Provider language; the online flow falls back to `is-IS`. |
| zones/range | Country and amount availability constraints. |

Provider-specific credentials live under `Ekom:Payments` and are consumed by the relevant payment implementation. There is no single universal credential shape; document and configure the implementation actually installed by the site.

The checkout service passes customer information, currency, order number, total order item(s), success/error/cancel URLs, and provider-specific payment fields to the runtime provider. See [Checkout](checkout.md) for the handoff lifecycle.

## Configure shipping providers

Shipping providers are store-scoped Umbraco nodes with a title, price, VAT behavior, zones, amount range, and arbitrary provider properties. Their ordered snapshot is saved onto the order when selected.

List shipping methods against the merchandise amount expected by the storefront. The built-in HTTP endpoint calculates its amount from the current order's charged amount minus the currently selected shipping price.

```csharp
IOrderInfo updated = await orderApi.UpdateShippingInformationAsync(
    shippingProvider.Key,
    "Store",
    new Dictionary<string, string>
    {
        ["customerDeliveryDate"] = "2026-09-18",
    },
    ct: ct);
```

Custom values are stored in the ordered provider's data and are suitable for a site's own fulfillment integration. Validate all client-supplied values server-side.

## Filter or replace provider lists

Use `ProviderEvents.BeforeReturnShippingProvidersAsync` and `BeforeReturnPaymentProvidersAsync` for request-specific business rules after built-in zone/range filtering:

```csharp
ProviderEvents.BeforeReturnPaymentProvidersAsync += (sender, args, ct) =>
{
    args.Providers = args.Providers.Where(provider => IsAllowed(provider, args.StoreAlias));
    return Task.CompletedTask;
};
```

The event arguments contain the replaceable provider sequence and `StoreAlias`. Synchronous events remain available, but async handlers are preferred. Unsubscribe static handlers during shutdown.

## HTTP endpoints

Routes are under `/ekom/provider`:

| Route | Behavior |
| --- | --- |
| `GET /paymentsproviders/{storeAlias?}?countryCode=IS&orderAmount=5000` | Filtered payment providers. |
| `GET /paymentsprovider/{guid}` | One payment provider in the current store context. |
| `GET /shippingproviders/{storeAlias?}?countryCode=IS` | Filtered shipping providers using the current order amount. |
| `GET /shippingprovider/{guid}` | One shipping provider in the current store context. |
| `GET /zones` | All zones. |

The route is spelled `paymentsproviders` for the payment list and `paymentsprovider` for one payment provider. A missing list store returns `404`. Single-provider actions currently return `200` with a null body if the store exists but the provider key does not.

Assign a provider with:

```http
POST /ekom/order/updateshippingprovider
Content-Type: application/json

{
  "shippingProvider": "00000000-0000-0000-0000-000000000010",
  "storeAlias": "Store"
}
```

Use `/ekom/order/updatepaymentprovider` with `paymentProvider` for payment selection. Both endpoints accept JSON, form data, or query fallback for the provider key and store alias. Selecting a payment provider does not submit payment.

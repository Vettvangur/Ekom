[Ekom docs](../README.md) / [Guides](README.md) / Payment and shipping providers

# Payment and shipping providers

A provider is an option the customer chooses at checkout: a shipping provider is a delivery method, a payment provider is a way to pay. Each provider is an Umbraco content node that belongs to a store. Read this page when you set up the delivery and payment options of a store, or list them on a storefront.

A shipping provider adds its price and its data to the order. A payment provider either takes payment offline, such as pay on delivery, or sends the customer to an online gateway that a payment package supplies.

On this page:

- [List the providers a customer can choose](#list-the-providers-a-customer-can-choose)
- [How Ekom filters providers by country and amount](#how-ekom-filters-providers-by-country-and-amount)
- [Set up a payment provider](#set-up-a-payment-provider)
- [Set up a shipping provider](#set-up-a-shipping-provider)
- [Filter the list in your own code](#filter-the-list-in-your-own-code)
- [HTTP endpoints](#http-endpoints)

## List the providers a customer can choose

Inject `Ekom.API.Providers`, or use `Providers.Instance`.

```csharp
using Ekom.API;
using Ekom.Models;

IReadOnlyList<IShippingProvider> shipping = await providers.GetShippingProvidersAsync(
    store: "Store",
    countryCode: "IS",
    orderAmount: order.OrderLineTotal.Value,
    ct: ct);

IReadOnlyList<IPaymentProvider> payment = await providers.GetPaymentProvidersAsync(
    store: "Store",
    countryCode: "IS",
    orderAmount: order.ChargedAmount.Value,
    ct: ct);

IShippingProvider? homeDelivery = await providers.GetShippingProviderAsync(homeDeliveryKey, "Store", ct);
IPaymentProvider? cardPayment = await providers.GetPaymentProviderAsync(cardPaymentKey, "Store", ct);
```

- Leave out `store` and Ekom uses the store of the current storefront request. Background jobs and other code without such a request must pass the store alias. If no store can be found, these methods throw `ArgumentNullException`.
- The single-provider methods return `null` when the key does not belong to a provider of that store.
- `GetAllCountries()` returns Ekom's list of countries. `GetAllZones()` returns the zones set up in Umbraco.

To save the customer's choice on the basket, see [Prepare the basket](checkout.md#prepare-the-basket).

## How Ekom filters providers by country and amount

Each provider can have a zone (a list of countries) and an amount range: a start of range and an end of range. The list methods use them like this:

- **Country.** When `countryCode` has exactly 2 letters, Ekom keeps only providers whose zone contains that country, ignoring upper and lower case. A provider without a zone is left out. Any other value, including an empty one, does not filter by country.
- **Amount.** When `orderAmount` is above `0`, Ekom keeps only providers whose start of range is at most the amount, and whose end of range is at least the amount. An end of range of `0` means no upper limit. An `orderAmount` of `0` does not filter by amount.
- **Order.** The list is sorted by the provider's sort order in Umbraco.
- **Events.** The filtered list is then passed to the provider events. See [Filter the list in your own code](#filter-the-list-in-your-own-code).

When a store has more than one currency, the start and end of range are read for the currency in the customer's `EkomCurrency-{storeAlias}` cookie. Otherwise Ekom uses the first value.

Pass the amount the customer will see the range against. The built-in shipping list endpoint uses the order's `ChargedAmount` minus the price of the shipping provider already chosen.

### When the basket changes

Ekom checks the chosen providers again each time a basket with the status **Incomplete** is saved, with the customer's country and the order amount without shipping and payment fees. Here a provider without a zone counts as valid for every country.

- A **payment provider** that no longer fits its zone or range, or no longer exists, is removed from the basket.
- A **shipping provider** stays on the basket even when it no longer fits, so the customer still sees the choice. It is removed only when it no longer exists. Checkout checks it again before payment. See [Keep or clear the shipping choice](checkout.md#keep-or-clear-the-shipping-choice).

## Set up a payment provider

Create payment providers under the store's payment provider container (document type `ekmPaymentProviders`) in Umbraco. Ekom reads these properties:

| Property | Effect |
| --- | --- |
| `offlinePayment` | When turned on, checkout completes the order without contacting a gateway. |
| `basePaymentProvider` | The name of the payment provider implementation to use, for example the one in `Ekom.Payments.Borgun`. When empty, Ekom uses the node name. Not used for offline payment. |
| `successUrl` | Where the customer goes after a successful payment. Ekom adds `?orderId=<order id>`. |
| `errorUrl` | Where the customer goes after a failed payment. |
| `cancelUrl` | Where the customer goes after cancelling the payment. |
| `language` | The language sent to the gateway. When empty, Ekom sends `is-IS`. |
| `zone`, `startOfRange`, `endOfRange` | Which countries and order amounts the provider is offered for. |

`successUrl`, `errorUrl` and `cancelUrl` can be a content picker or a text value. Ekom reads the value for the order's culture or its store.

An offline provider needs nothing but Ekom. Each online gateway is one package. Installing it is enough for Ekom to find the provider and register its services. Configure the gateway as its package describes. See [Payments](payments.md) for how a provider package plugs in.

At payment, Ekom gives the provider the customer's details, the currency, the order number, the payment items, the success, error and cancel URLs, and any card fields in the request. [Checkout](checkout.md#what-ekom-does-when-you-submit-payment) describes the whole hand-over.

## Set up a shipping provider

Shipping providers are nodes in Umbraco that belong to a store. Each has a title, a price, VAT settings, a zone and an amount range. When the customer chooses one, Ekom saves a copy of it on the order, so later changes to the node do not change existing orders.

To let a carrier book shipments, set the carrier properties on the node. See [Shipping and shipments](shipping.md#connect-a-carrier).

Save the customer's choice, and any values your site needs later, with `UpdateShippingInformationAsync`:

```csharp
IOrderInfo updated = await orderApi.UpdateShippingInformationAsync(
    homeDeliveryKey,
    "Store",
    new Dictionary<string, string>
    {
        ["customshippingDeliveryDate"] = "2026-12-18",
    },
    ct: ct);
```

Only keys that start with `customshipping` are saved on the order's shipping provider. Ekom HTML-encodes the values. Read them later from the order's shipping provider, for example in your own fulfilment integration. They come from the customer, so check them on the server before you use them.

## Filter the list in your own code

Use `ProviderEvents.BeforeReturnShippingProvidersAsync` and `ProviderEvents.BeforeReturnPaymentProvidersAsync` for your own rules, for example to hide invoice payment from customers who are not logged in. They run after the zone and range filters.

```csharp
using Ekom.Events;

ProviderEvents.BeforeReturnPaymentProvidersAsync += (sender, args, ct) =>
{
    args.Providers = args.Providers.Where(provider => IsAllowed(provider, args.StoreAlias));
    return Task.CompletedTask;
};
```

`args.Providers` is the list you can filter or replace. `args.StoreAlias` is the store alias passed to the list method. It is an empty string when the caller did not pass a store.

The sync events `BeforeReturnShippingProviders` and `BeforeReturnPaymentProviders` still exist. The async list methods run the sync handlers first, then the async ones. Prefer the async events. Unsubscribe static handlers when the site shuts down.

These events only change what the list methods return. They do not stop a customer from choosing a provider by its key.

## HTTP endpoints

The read endpoints are under `/ekom/provider`:

| Request | Returns |
| --- | --- |
| `GET /ekom/provider/paymentsproviders/{storeAlias?}?countryCode=IS&orderAmount=5000` | Payment providers, filtered as described above. |
| `GET /ekom/provider/paymentsprovider/{key}` | One payment provider of the current store. |
| `GET /ekom/provider/shippingproviders/{storeAlias?}?countryCode=IS` | Shipping providers, filtered by country and by the current order's amount without its shipping price. |
| `GET /ekom/provider/shippingprovider/{key}` | One shipping provider of the current store. |
| `GET /ekom/provider/zones` | All zones. |

The payment routes are spelled `paymentsproviders` and `paymentsprovider`, with an `s` after `payment`.

- The list endpoints return HTTP 404 when the store is not found.
- The single-provider endpoints use the store of the current request. They return 404 when there is none, and HTTP 200 with an empty body when the key does not belong to a provider of that store.

To save the customer's choice:

```http
POST /ekom/order/updateshippingprovider
Content-Type: application/json

{
  "shippingProvider": "0b9e2a61-7c3d-4f58-8e21-5a4d6c7b9f10",
  "storeAlias": "Store",
  "customshippingDeliveryDate": "2026-12-18"
}
```

Use `POST /ekom/order/updatepaymentprovider` with `paymentProvider` to save the payment choice. Both endpoints also answer at `/ekom/order/update/shippingprovider/` and `/ekom/order/update/paymentprovider/`.

- The body can be JSON or form data. When the body has no provider key or store alias, Ekom reads them from the query string.
- A missing or empty provider key, or a missing store alias, returns HTTP 400.
- The response is the updated order.

Choosing a provider does not submit payment. It is a basket change: it ends a payment attempt that is still running and releases its stock holds. See [Cancel, edit and retry a payment](checkout.md#cancel-edit-and-retry-a-payment).

## Related pages

- [Checkout](checkout.md): use the chosen providers to take payment.
- [Payments](payments.md): add an online payment gateway and follow each payment.
- [Shipping and shipments](shipping.md): book shipments with a carrier and record tracking numbers.
- [Stores](stores.md): stores, currencies and countries.
- [HTTP API](../reference/http-api.md): every Ekom endpoint.

[Back to Guides](README.md)

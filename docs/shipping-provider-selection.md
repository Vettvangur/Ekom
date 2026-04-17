# Shipping Provider Selection

This page describes how shipping provider selection works in Ekom.

It focuses on the developer workflow for:

- loading available shipping providers
- rendering a shipping provider selector
- posting the selected provider back to Ekom
- understanding what changes on the order

## What shipping provider selection does

Selecting a shipping provider updates the current order so Ekom knows how the order should be shipped.

This step does **not** complete checkout.

It only assigns the selected shipping provider and related provider data to the order.

## Typical flow

The normal shipping-provider flow looks like this:

1. read the current order
2. load available shipping providers
3. render the provider options
4. submit the selected shipping provider
5. persist the provider on the order
6. continue through checkout

## Loading shipping providers

Ekom exposes shipping providers through `API.Providers`.

The common pattern is to load them and sort by `SortOrder`.

```csharp
var shippingProviders = Providers.Instance
    .GetShippingProviders()
    .OrderBy(x => x.SortOrder);
```

Depending on your flow, you may want to filter them by store or by current order context.

## Rendering shipping providers

The older GitBook page for shipping providers is very light, so the pattern below mirrors the existing payment-provider approach.

```cshtml
@{
    var shippingProviders = Providers.Instance.GetShippingProviders().OrderBy(x => x.SortOrder);
    var currentShippingProviderKey = order.ShippingProvider != null ? order.ShippingProvider.Key : Guid.Empty;
    currentShippingProviderKey = shippingProviders.Count() > 1 ? currentShippingProviderKey : shippingProviders.First().Key;
}

@using (Html.BeginEkomForm(FormType.UpdateShippingProvider, "Form Class", "Form Id"))
{
    <input type="hidden" name="storeAlias" value="@order.StoreInfo.Alias" />

    @foreach (var provider in shippingProviders)
    {
        <label>
            <input name="ShippingProvider" type="radio" value="@provider.Key" @(provider.Key == currentShippingProviderKey ? "checked" : "") />
            <p>@provider.Title</p>
            @if (!string.IsNullOrEmpty(provider.Description))
            {
                <p>@Html.Raw(Html.ReplaceLineBreaks(provider.Description))</p>
            }
        </label>

        <input type="text" name="customshippingText">
    }

    <button type="submit">Save Shipping Provider</button>
}
```

### What this example does

- loads shipping providers
- orders them by `SortOrder`
- checks the current provider on the order
- posts the selected provider back to Ekom

### Custom data

You can include additional provider-related fields in the request payload.

Ekom can carry those values forward when building the ordered shipping provider and updating provider-related customer data.

## Updating the shipping provider through C#

The programmatic entry point is:

```csharp
IOrderInfo order = await _order.UpdateShippingInformationAsync(
    shippingProviderId,
    "Store",
    new Dictionary<string, string>(),
    ct: ct);
```

### Parameters

- `shippingProviderId`: provider key to assign
- `storeAlias`: store alias for the current order
- `allData`: additional posted data used during provider/customer update flow

### What happens internally

When `UpdateShippingInformationAsync(...)` runs, Ekom:

- resolves the store
- loads the current order
- resolves the shipping provider from the provider registry
- creates an `OrderedShippingProvider`
- assigns it to the order
- updates provider-related customer/order data
- persists the updated order

## Updating the shipping provider through HTTP

The public order controller exposes two routes:

```text
POST /ekom/order/update/shippingprovider/
POST /ekom/order/updateshippingprovider
```

### Example JSON request

```http
POST /ekom/order/updateshippingprovider
Content-Type: application/json

{
  "ShippingProvider": "00000000-0000-0000-0000-000000000010",
  "storeAlias": "Store"
}
```

### Supported request sources

Ekom can read the shipping provider from:

- JSON body
- form data
- query string fallback

### Response

- `200 OK` with the updated order
- `400 Bad Request` if `ShippingProvider` or `storeAlias` is missing

## Provider validation behavior

Ekom resolves the provider using the current store.

If the provider cannot be resolved, the order is returned unchanged.

If the provider id is empty, the order is returned unchanged.

This makes the operation safe to call repeatedly, but you should still validate the user’s choice in your checkout UI.

## Activity log behavior

When the shipping provider actually changes, Ekom writes an activity log entry:

- `Shipping provider added. Provider: {ProviderTitle}`

Type:

- `OrderActivityLogType.Info`

If the same provider is submitted again, that log is not written.

## What shipping provider selection does not do

Selecting the shipping provider does **not**:

- submit payment
- complete the order
- finalize checkout

It only updates the order state.

## Relationship to customer information

Shipping provider updates often happen close to customer-information updates.

That is because shipping calculations and provider validation can depend on:

- shipping country
- address information
- order total

In some checkout flows, customer and provider data are submitted together.

## Minimal server-side example

```csharp
IOrderInfo? currentOrder = await _order.GetOrderAsync("Store", ct);

if (currentOrder == null)
{
    throw new InvalidOperationException("No active order found.");
}

IOrderInfo updatedOrder = await _order.UpdateShippingInformationAsync(
    shippingProviderId,
    "Store",
    new Dictionary<string, string>
    {
        ["customshippingText"] = "Leave at front desk"
    },
    ct: ct);
```

## Minimal headless example

```http
POST /ekom/order/updateshippingprovider
Content-Type: application/json

{
  "ShippingProvider": "00000000-0000-0000-0000-000000000010",
  "storeAlias": "Store",
  "customshippingText": "Leave at front desk"
}
```

## Common pitfalls

### Expecting shipping selection to complete checkout

It does not. It only updates the order.

### Forgetting `storeAlias`

Most shipping-provider update flows depend on a valid store alias.

### Assuming every provider is valid for every order

Shipping provider availability can depend on order and customer context.

### Reposting the same provider and expecting a new activity log entry

The log entry is only written when the provider actually changes.

## Related pages

- [Payment Provider Selection](payment-provider-selection.md)
- [Checkout Flow Overview](checkout-flow-overview.md)
- [Order Endpoints](order-endpoints.md)
- [API.Order Reference](api-order-reference.md)
- [Order Lifecycle](order-lifecycle.md)

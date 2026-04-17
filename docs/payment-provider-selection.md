# Payment Provider Selection

This page describes how payment provider selection works in Ekom.

It focuses on the developer workflow for:

- rendering available payment providers
- posting the selected provider back to Ekom
- understanding what changes on the order
- knowing what happens later in checkout for online and offline providers

## What payment provider selection does

Selecting a payment provider updates the current order so Ekom knows how the order should be paid.

This step does **not** complete the order.

It only sets the payment provider and related provider data on the order.

## Typical flow

The normal provider-selection flow looks like this:

1. read the current order
2. load available payment providers
3. render the available options
4. submit the selected provider
5. persist the provider on the order
6. continue into payment submission later

## Rendering payment providers

The existing GitBook page already shows the basic Razor approach.

```cshtml
@{
    var paymentProviders = Providers.Instance.GetPaymentProviders().OrderBy(x => x.SortOrder);
    var currentPaymentProviderKey = order.PaymentProvider != null ? order.PaymentProvider.Key : Guid.Empty;
    currentPaymentProviderKey = paymentProviders.Count() > 1 ? currentPaymentProviderKey : paymentProviders.First().Key;
}

@using (Html.BeginEkomForm(FormType.UpdatePaymentProvider, "Form Class", "Form Id"))
{
    <input type="hidden" name="storeAlias" value="@order.StoreInfo.Alias" />
    
    @foreach (var provider in paymentProviders)
    {
        <label>
            <input name="PaymentProvider" type="radio" value="@provider.Key" @(provider.Key == currentPaymentProviderKey ? "checked" : "") />
            <p>@provider.Title</p>
            @if (!string.IsNullOrEmpty(provider.Description))
            {
                <p>@Html.Raw(Html.ReplaceLineBreaks(provider.Description))</p>
            }
        </label>

        <input type="text" name="paymentproviderCustomText">
    }
    
    <button type="submit">Save Payment Provider</button>
}
```

### What this example does

- loads all payment providers
- orders them by `SortOrder`
- checks the currently selected provider on the order
- posts the chosen provider back to Ekom

### Important note about custom data

Custom fields can be saved with the provider as long as their field names are included in the posted payload.

The older example uses a field name prefixed with `paymentprovider...`, which can be useful when you want to carry provider-specific form data along with the provider update.

## Updating the payment provider through C#

The programmatic entry point is:

```csharp
IOrderInfo order = await _order.UpdatePaymentInformationAsync(
    paymentProviderId,
    "Store",
    new Dictionary<string, string>(),
    ct: ct);
```

### Parameters

- `paymentProviderId`: provider key to assign
- `storeAlias`: store alias for the current order
- `allData`: additional posted data to keep with the provider/order flow

### What happens internally

When `UpdatePaymentInformationAsync(...)` runs, Ekom:

- resolves the store
- loads the current order
- resolves the payment provider from the provider registry
- creates an `OrderedPaymentProvider`
- assigns it to the order
- updates provider-related customer/order data
- persists the updated order

## Updating the payment provider through HTTP

The public order controller exposes two routes:

```text
POST /ekom/order/update/paymentprovider/
POST /ekom/order/updatepaymentprovider
```

### Example JSON request

```http
POST /ekom/order/updatepaymentprovider
Content-Type: application/json

{
  "PaymentProvider": "00000000-0000-0000-0000-000000000020",
  "storeAlias": "Store"
}
```

### Supported request sources

Ekom can read the payment provider from:

- JSON body
- form data
- query string fallback

### Response

- `200 OK` with the updated order
- `400 Bad Request` if `PaymentProvider` or `storeAlias` is missing

## Provider validation behavior

Ekom resolves the provider using the current store.

If the provider cannot be resolved, the order is returned unchanged.

If the provider id is empty, the order is returned unchanged.

This means the update call is safe to use repeatedly, but you should still validate provider choice in your UI.

## Activity log behavior

When the payment provider actually changes, Ekom writes an activity log entry:

- `Payment provider added. Provider: {ProviderTitle}`

Type:

- `OrderActivityLogType.Info`

If the same provider is submitted again, that log is not written.

## What payment provider selection does not do

Selecting the payment provider does **not**:

- submit payment
- redirect to the provider
- complete the order

Those steps happen later in the checkout flow.

## What happens after provider selection

Later in checkout, Ekom uses the selected provider when `PayAsync(...)` or `/ekom/checkout/pay` is called.

At that stage, `CheckoutControllerService`:

- loads the selected payment provider from the order
- checks whether it is an offline or online provider
- builds provider-specific payment settings
- updates order status before payment handoff

## Online payment providers

For online payment providers, Ekom typically:

1. updates status to `WaitingForPayment`
2. prepares payment settings
3. hands off to the payment provider integration
4. waits for the return/callback flow

### Important behavior

The order is not completed at provider-selection time.

It is only prepared for payment.

## Offline payment providers

For offline payment providers, Ekom behaves differently.

When the payment is processed through checkout, Ekom can:

1. set status to `OfflinePayment`
2. raise pay events
3. call the completion pipeline directly

This is why payment-provider selection and payment-processing should be thought of as separate steps.

## Minimal server-side example

This example shows a simple application-side provider update flow.

```csharp
IOrderInfo? currentOrder = await _order.GetOrderAsync("Store", ct);

if (currentOrder == null)
{
    throw new InvalidOperationException("No active order found.");
}

IOrderInfo updatedOrder = await _order.UpdatePaymentInformationAsync(
    paymentProviderId,
    "Store",
    new Dictionary<string, string>
    {
        ["paymentproviderCustomText"] = "Requested invoice reference"
    },
    ct: ct);
```

## Minimal headless example

```http
POST /ekom/order/updatepaymentprovider
Content-Type: application/json

{
  "PaymentProvider": "00000000-0000-0000-0000-000000000020",
  "storeAlias": "Store",
  "paymentproviderCustomText": "Requested invoice reference"
}
```

## Common pitfalls

### Expecting provider selection to complete checkout

It does not. It only updates the order.

### Forgetting `storeAlias`

Most provider update flows depend on a valid store alias.

### Assuming all providers behave the same way

Online and offline payment providers diverge later in the checkout flow.

### Reposting the same provider and expecting a new log entry

Activity logging for provider changes only happens when the provider actually changes.

## Related pages

- [Checkout Flow Overview](checkout-flow-overview.md)
- [API.Order Reference](api-order-reference.md)
- [Order Endpoints](order-endpoints.md)
- [Order Lifecycle](order-lifecycle.md)

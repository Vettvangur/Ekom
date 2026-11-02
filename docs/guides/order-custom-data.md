# Order custom data

`OrderInfo.CustomData` stores order-level string keys and values, such as a gift message or purchase reference. It is also available through `IOrderInfo.CustomData`. Values are saved in the existing order JSON and remain available after reloading the order.

## HTML checkout forms

Add fields named `CustomData[Key]` to a customer, shipping, or payment update form. The wrapper selects order custom data; only the key inside the brackets is stored.

```html
<form method="post" action="/ekom/order/updatecustomer">
    <input type="hidden" name="storeAlias" value="Store" />
    <input name="customerEmail" type="email" />
    <input name="CustomData[GiftMessage]" type="text" />
    <input name="CustomData[PurchaseReference]" type="text" />
    <button type="submit">Save</button>
</form>
```

Keep your application's authentication and antiforgery handling. Inside an existing Razor form, standard helpers work too:

```cshtml
@Html.TextBox("CustomData[GiftMessage]", Model.GiftMessage)
@Html.Hidden("CustomData[PurchaseReference]", Model.PurchaseReference)
```

The same fields work with `/ekom/order/updateshippingprovider` and `/ekom/order/updatepaymentprovider`. Include `storeAlias` and the existing `ShippingProvider` or `PaymentProvider` GUID field respectively. Custom data is saved when the provider update succeeds, including when the provider selection is unchanged.

The checkout pay form also accepts these fields. They are saved during checkout's information-update step even when no provider selection changes.

## JSON API requests

Send `Content-Type: application/json` and a nested `CustomData` object. For `/ekom/order/updatecustomer`:

```json
{
  "storeAlias": "Store",
  "customerEmail": "customer@example.com",
  "CustomData": {
    "GiftMessage": "Happy birthday!",
    "PurchaseReference": "PO-123"
  }
}
```

For `/ekom/order/updateshippingprovider`, include the selected provider's GUID:

```json
{
  "storeAlias": "Store",
  "ShippingProvider": "11111111-1111-1111-1111-111111111111",
  "CustomData": {
    "GiftMessage": "Happy birthday!"
  }
}
```

Payment updates use `/ekom/order/updatepaymentprovider` and `PaymentProvider` instead. Replace the example GUID with a valid provider for the store. The checkout pay endpoint also accepts a top-level `CustomData` object alongside its normal payment fields.

## Reading, merging, and removing values in C#

Use the returned order or reload it after an update:

```csharp
var updatedOrder = await Order.Instance.UpdateCustomDataAsync(
    orderId,
    new Dictionary<string, string>
    {
        ["GiftMessage"] = "Happy birthday!",
        ["PurchaseReference"] = "PO-123",
    },
    ct: ct);

if (updatedOrder.CustomData.TryGetValue("giftmessage", out var message))
{
    // Use message; encode it when rendering HTML.
}

updatedOrder = await Order.Instance.RemoveCustomDataAsync(
    orderId,
    new[] { "PurchaseReference" },
    ct: ct);
```

These server-side methods load the persisted order by ID, acquire the existing order information-update ownership, and save only custom data without recalculating purchase totals. They support information edits on existing orders, including final orders, and raise the usual order-updated notifications unless disabled through `OrderSettings`. They do not accept a supplied `OrderSettings.OrderInfo` or event-handler reentry. The caller must authorize access to the order; knowing its GUID is not authorization.

For the existing customer, shipping, and payment C# update methods, pass bracketed field names in their form/data dictionary, for example `["CustomData[GiftMessage]"] = "Happy birthday!"`.

Changing a dictionary on an in-memory `OrderInfo` alone does not save it. Use an update method for persistence.

## Rules and storage

- Both the `CustomData` wrapper and keys are case-insensitive. `CustomData[GiftMessage]` and `customdata[giftmessage]` refer to the same entry, including after save/reload.
- Submitted keys are added or overwritten. Omitted keys retain their existing values.
- Empty strings are valid and do not remove entries. Use `RemoveCustomDataAsync` for removal; missing removal keys are ignored.
- Keys must not be blank or contain brackets. Duplicate keys ignoring case, repeated form values, and malformed custom-data field names are rejected.
- JSON values must be strings. Nulls, numbers, booleans, arrays, and objects are rejected. Invalid custom-data requests return HTTP 400 through the update endpoints.
- Text is stored unchanged, including casing, whitespace, `&`, and `<`. Encode values when displaying HTML; do not use raw HTML rendering for customer-supplied text.
- Orders saved before this feature have an empty dictionary when loaded. No new table or database column is needed.

Existing `customer...`, `shipping...`, `customshipping...`, and `custompayment...` fields retain their destinations and behavior. `OrderSettings.CustomData` used for adding order lines is not automatically copied into order-level custom data.

All checkout inputs, including hidden fields, are customer-controlled and may overwrite matching order custom-data keys. Do not use this collection as a trusted source for prices, permissions, payment state, or internal-only metadata. Order custom data is treated as editable information rather than part of the submitted purchase comparison.

## Related guides

- [Orders and baskets](orders-and-baskets.md)
- [Checkout](checkout.md)
- [HTTP API](../reference/http-api.md)

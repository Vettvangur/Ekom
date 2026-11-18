[Ekom docs](../README.md) / [Guides](README.md) / Order custom data

# Order custom data

Order custom data is a set of your own text keys and values saved on the whole order, such as a gift message or a purchase order number. Use it for values that belong to the order and that Ekom has no field for. Ekom saves the values in the order's stored JSON, so they are there when the order is loaded again.

Read the values from `OrderInfo.CustomData`, or from `IOrderInfo.CustomData`, which is read-only.

On this page:

- [Add custom data from a checkout form](#add-custom-data-from-a-checkout-form)
- [Add custom data with JSON](#add-custom-data-with-json)
- [Read, add and remove values in C#](#read-add-and-remove-values-in-c)
- [Rules for keys and values](#rules-for-keys-and-values)
- [Do not trust custom data](#do-not-trust-custom-data)
- [Values on a single order line](#values-on-a-single-order-line)
- [See custom data in the manager](#see-custom-data-in-the-manager)

## Add custom data from a checkout form

Give a form field the name `CustomData[Key]`. Ekom saves the key inside the brackets, without the `CustomData[...]` wrapper.

```html
<form method="post" action="/ekom/order/updatecustomer">
    <input type="hidden" name="storeAlias" value="Store" />
    <input name="customerEmail" type="email" />
    <input name="CustomData[GiftMessage]" type="text" />
    <input name="CustomData[PurchaseReference]" type="text" />
    <button type="submit">Save</button>
</form>
```

Keep your own authentication and antiforgery handling. Inside a Razor form, the standard helpers work too:

```cshtml
@Html.TextBox("CustomData[GiftMessage]", Model.GiftMessage)
@Html.Hidden("CustomData[PurchaseReference]", Model.PurchaseReference)
```

These endpoints accept `CustomData[...]` fields:

| Endpoint | Also needs | When the values are saved |
| --- | --- | --- |
| `POST /ekom/order/updatecustomer` | `storeAlias` | With the customer update. |
| `POST /ekom/order/updateshippingprovider` | `storeAlias` and `ShippingProvider` (a provider GUID) | When the provider is found in the store, also when it is already the selected provider. |
| `POST /ekom/order/updatepaymentprovider` | `storeAlias` and `PaymentProvider` (a provider GUID) | When the provider is found in the store, also when it is already the selected provider. |
| `POST /ekom/checkout/pay` and `POST /ekom/mvcCheckout/pay` | The normal payment fields | In checkout's information step, also when no provider changes. |

If the provider GUID is not a provider of that store, the provider endpoints return the basket unchanged and the custom data is not saved.

## Add custom data with JSON

Send `Content-Type: application/json` and put the values in a `CustomData` object. For `/ekom/order/updatecustomer`:

```json
{
  "storeAlias": "Store",
  "customerEmail": "jane@example.com",
  "CustomData": {
    "GiftMessage": "Happy birthday!",
    "PurchaseReference": "PO-123"
  }
}
```

For `/ekom/order/updateshippingprovider`, add the GUID of the chosen provider:

```json
{
  "storeAlias": "Store",
  "ShippingProvider": "11111111-1111-1111-1111-111111111111",
  "CustomData": {
    "GiftMessage": "Happy birthday!"
  }
}
```

Use a real provider GUID from the store. Payment updates use `/ekom/order/updatepaymentprovider` with `PaymentProvider` instead. `/ekom/checkout/pay` also accepts a `CustomData` object next to its payment fields.

JSON requests can also send a top-level field named `CustomData[Key]` with a string value. It is saved the same way as a key in the `CustomData` object.

## Read, add and remove values in C#

`orderApi` is the `Ekom.API.Order` facade. Inject it, or use `Order.Instance`.

```csharp
using Ekom.API;
using Ekom.Models;

IOrderInfo updatedOrder = await orderApi.UpdateCustomDataAsync(
    orderId,
    new Dictionary<string, string>
    {
        ["GiftMessage"] = "Happy birthday!",
        ["PurchaseReference"] = "PO-123",
    },
    ct: ct);

if (updatedOrder.CustomData.TryGetValue("giftmessage", out string? message))
{
    // Use the message. Encode it when you render HTML.
}

updatedOrder = await orderApi.RemoveCustomDataAsync(
    orderId,
    new[] { "PurchaseReference" },
    ct: ct);
```

`UpdateCustomDataAsync` and `RemoveCustomDataAsync`:

- load the saved order by ID, so they work on any order, including completed orders;
- save only the custom data, and do not recalculate prices or totals;
- save only when the stored order has not changed since they read it. Otherwise they throw `InvalidOperationException`, and you retry;
- throw `OrderInfoNotFoundException` when there is no order with that ID, and `ArgumentException` for an empty order ID;
- raise the order-updated events, unless you turn them off with `OrderSettings`;
- throw `ArgumentException` if you pass `OrderSettings.OrderInfo` or set `OrderSettings.IsEventHandler`.

These methods do not check who is calling. Check that the caller may change the order before you call them. Knowing an order GUID is not permission.

The customer, shipping and payment update methods take custom data in their dictionary with the bracket names, for example `["CustomData[GiftMessage]"] = "Happy birthday!"`.

Changing `OrderInfo.CustomData` on an order in memory does not save it. Use one of the update methods.

## Rules for keys and values

- The `CustomData` wrapper and the keys ignore upper and lower case. `CustomData[GiftMessage]` and `customdata[giftmessage]` are the same entry, also after the order is saved and loaded.
- Keys you send are added or replace the existing value. Keys you do not send keep their value.
- An empty string is a valid value and does not remove the key. Use `RemoveCustomDataAsync` to remove keys. Keys that are not on the order are ignored.
- A key must not be empty or blank, and must not contain `[` or `]`.
- These are rejected: the same key twice in one request (ignoring case), a form field with more than one value, a field name that starts with `CustomData[` but does not end with `]`, and a JSON value that is not a string (`null`, a number, `true` or `false`, an array or an object).
- Through the update endpoints, a rejected request returns HTTP 400. In C#, Ekom throws `OrderCustomDataException`, which is an `ArgumentException`.
- The public endpoints accept at most 200 fields per request, field names of at most 100 characters (counting the `CustomData[...]` wrapper), and values of at most 4,096 characters. `updatecustomer` also refuses to give an order more than 500 custom data entries.
- Ekom saves the text as it was sent: case, spaces, `&` and `<` are kept. Encode values when you show them in HTML, and do not render customer text as raw HTML.
- Orders saved before custom data existed load with an empty set. No table or column is needed.

Custom data does not change other fields. Fields that start with `customer`, `shipping`, `customshipping` and `custompayment` still go where they always go. `OrderSettings.CustomData` that you pass when adding a line is line data, and is not copied to the order's custom data.

## Do not trust custom data

Every checkout field, including hidden fields, comes from the customer, and the customer can change any custom data key. Do not use custom data for prices, permissions, payment state or internal notes.

When checkout compares the submitted purchase with the order, it leaves custom data out. A change to custom data alone does not make the purchase differ.

## Values on a single order line

Custom data is for the whole order. To save a value on one line, use a key that starts with `orderline`:

- When you add a line, put the key in `AddOrderSettings.CustomData`, or send it as a field to `POST /ekom/order/add`. See [Orders and baskets](orders-and-baskets.md#add-order-lines).
- To change line values on an existing order, use `UpdateOrderLineMetadataAsync`:

```csharp
IOrderInfo order = await orderApi.UpdateOrderLineMetadataAsync(
    orderId,
    [
        new OrderLineMetadataUpdate
        {
            LineId = lineKey,
            Properties = new Dictionary<string, string>
            {
                ["orderlineEngraving"] = "Team 2026",
            },
        },
    ],
    ct: ct);
```

It saves all updates in one save, in any order or payment state. Each key must start with `orderline` and each value must be non-null. Each line may appear once, with at least one property. Otherwise the whole batch is rejected with `ArgumentException`. A line key that is not on the order throws `OrderLineNotFoundException`. Like the order custom data methods, it saves only when the stored order has not changed since it read it.

## See custom data in the manager

In the Umbraco 17, 18 and 19 manager, the order details show custom data in a **Notes** card under the order lines, payment and shipment. Keys are shown in a readable form (`GiftMessage` is shown as "Gift Message"), and values wrap to fit. The card appears only when the order has custom data, and entries with an empty or blank value are left out. The printed order shows the same values above the billing address. The manager only displays custom data. It does not let staff edit it.

## Related pages

- [Orders and baskets](orders-and-baskets.md): the basket methods that also save custom data.
- [Checkout](checkout.md): the payment step that accepts custom data fields.
- [Linked order lines](linked-order-lines.md): save values on add-on lines.
- [HTTP API reference](../reference/http-api.md): the order endpoints and their fields.

[Back to Guides](README.md)

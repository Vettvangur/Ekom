# Linked order lines

[Ekom docs](../README.md) / [Guides](README.md) / Linked order lines

Linked order lines let you add a product together with add-ons that belong to it, such as engraving, printing, gift wrapping, a warranty or a service. Ekom adds the parent line and all its child lines in one step: either all of them are added, or none.

Each child is an ordinary order line with its own quantity. Its `OrderLineSettings.Link` holds the key of the parent line, which Ekom creates for you.

On this page:

- [Add a linked group over HTTP](#add-a-linked-group-over-http)
- [Add a linked group from C#](#add-a-linked-group-from-c)
- [What Ekom checks before it saves](#what-ekom-checks-before-it-saves)
- [How linked lines behave in the basket](#how-linked-lines-behave-in-the-basket)
- [Send linked lines from a product form](#send-linked-lines-from-a-product-form)

## Add a linked group over HTTP

Send `linkedProducts` to `POST /ekom/order/add`:

```http
POST /ekom/order/add
Content-Type: application/json

{
  "productId": "11111111-1111-1111-1111-111111111111",
  "variantId": "22222222-2222-2222-2222-222222222222",
  "storeAlias": "Store",
  "quantity": 1,
  "linkedProducts": [
    {
      "productId": "33333333-3333-3333-3333-333333333333",
      "quantity": 1,
      "customData": {
        "orderlineName": "Anna",
        "orderlineText": "Team 2026"
      }
    },
    {
      "productId": "44444444-4444-4444-4444-444444444444",
      "variantId": "55555555-5555-5555-5555-555555555555",
      "quantity": 2
    }
  ]
}
```

The response is the updated `IOrderInfo`.

When `linkedProducts` has at least one entry, Ekom always creates new lines (`OrderAction.New`) and ignores the `action` field. This keeps the line keys of each group clear.

Each entry in `linkedProducts` accepts:

| Field | Required | What it does |
| --- | --- | --- |
| `productId` | Yes | The product key. |
| `variantId` | When the product needs a variant | A variant key of that product. |
| `quantity` | No | The child's own quantity. It must be greater than `0`. The default is `1`. |
| `customData` | No | Text values for this child. Only keys that start with `orderline` are saved, in the line's `OrderLineInfo.Properties`. |

Other fields on the root object are custom data for the parent line, as on an ordinary add. Again, only `orderline` keys are saved on the line. Ekom HTML-encodes all custom data values from this endpoint, for the parent and the children, before it saves them.

`ekomUpdateInformation` cannot be sent with linked products. Update customer details with a separate request.

## Add a linked group from C#

```csharp
using Ekom.API;
using Ekom.Models;

IOrderInfo basket = await orderApi.AddLinkedOrderLinesAsync(
    mugProductKey,
    quantity: 1,
    storeAlias: "Store",
    linkedProducts:
    [
        new LinkedOrderLineRequest
        {
            ProductId = engravingProductKey,
            Quantity = 1,
            CustomData = new Dictionary<string, string>
            {
                ["orderlineText"] = "Team 2026",
            },
        },
    ],
    settings: new AddOrderSettings
    {
        VariantKey = mugVariantKey,
    },
    ct);
```

`orderApi` is the `Ekom.API.Order` facade. Inject it, or use `Order.Instance`.

- `settings.OrderAction` is ignored. Every line in the group is a new line.
- `settings.CustomData` is the parent's custom data. `settings.Consent`, `Tracking` and `AlgoliaQueryId` work as on an ordinary add.
- `settings.OrderDynamicRequest` applies to the parent only. Children do not inherit its prices.
- If `storeAlias` is empty, Ekom uses the store of the current storefront request.

## What Ekom checks before it saves

Ekom resolves and checks the parent and every child before it changes the basket. These throw `ArgumentException`:

- `linkedProducts` is empty, or contains a `null` entry
- an empty product key
- a quantity of `0` or less, for the parent or a child
- `ekomUpdateInformation` in the custom data

`linkedProducts` itself being `null` throws `ArgumentNullException`.

Product and variant errors are the same as for an ordinary add: `ProductNotFoundException`, `VariantNotFoundException`, `VariantRequiredException`, and an `EkomException` when the variant does not belong to the product.

Ekom then builds the whole group on a copy of the basket and saves once. On the copy it raises the adding-line events, checks stock, applies discounts and builds the lines. If any of these steps or the save fails, none of the new lines are added.

- **Stock.** For each line, Ekom adds the new quantity to the quantity of the same product and variant already in the basket, including lines added earlier in the same group, and checks that total against stock.
- **Discounts.** A product discount is applied to the new line unless the order has a discount that cannot be combined with others.
- **Prices, availability, consent and tracking** work the same as for an ordinary add.

After the save, Ekom raises the added-line, updated-line and order-updated events and writes one "Order line added" entry to the activity log per line. If one of these fails, Ekom logs the error and the lines stay saved.

Over HTTP, a `null` entry in `linkedProducts` returns `400`, and an empty `linkedProducts` array makes an ordinary add. The other `ArgumentException` cases are not mapped to a status code and return `500`. Check quantities in the browser before you send them.

If the customer has no basket yet, Ekom creates one before it builds the group. That basket stays, empty, when the group then fails.

## How linked lines behave in the basket

- Parent and child quantities are independent. Changing the parent quantity does not change the children.
- You can change or remove a child on its own.
- Removing the parent also removes every line whose `Settings.Link` points to it.
- Removing a child does not remove its parent or the other children.
- Children count towards quantity, tax, discounts, stock and totals like any other line.
- There is one level only: a child cannot have children of its own.
- Adding the same group again creates a second parent with its own children. It is not merged into the first group.
- An ordinary add of the parent's product never updates a parent line. It adds or updates a separate line without children.

## Send linked lines from a product form

A product form has flat fields, but the endpoint needs a `linkedProducts` array. Build the JSON in the browser and post it:

```html
<script>
document.querySelector('#personalized-product').addEventListener('submit', async event => {
  event.preventDefault();

  const form = event.currentTarget;
  const payload = Object.fromEntries(new FormData(form).entries());
  payload.quantity = Number(payload.quantity);
  payload.linkedProducts = [{
    productId: payload.linkedProductId,
    variantId: payload.linkedVariantId || null,
    quantity: Number(payload.linkedQuantity),
    customData: { orderlineText: payload.linkedOrderlineText }
  }];

  delete payload.linkedProductId;
  delete payload.linkedVariantId;
  delete payload.linkedQuantity;
  delete payload.linkedOrderlineText;

  const response = await fetch(form.action, {
    method: 'POST',
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });

  if (!response.ok) throw new Error(await response.text());
});
</script>
```

- `Object.fromEntries` keeps only the last field when several fields have the same name. If the form has more than one add-on, read those fields yourself.
- `/ekom/order/add` does not check the antiforgery token that `Html.BeginEkomForm` adds to the form. If you post JSON like this, add your own CSRF protection in the host. Do not rely on the hidden token.

## Related pages

- [Orders and baskets](orders-and-baskets.md): add, change and remove ordinary lines.
- [Order custom data](order-custom-data.md): save values on the whole order instead of on a line.
- [Stock](stock.md): how Ekom decides whether there is enough stock.
- [HTTP API reference](../reference/http-api.md): every order endpoint and its fields.
- [Headless storefronts](headless.md): call the order endpoints from a separate front end.

[Back to Guides](README.md)

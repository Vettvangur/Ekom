# Linked order lines

`POST /ekom/order/add` can add a new parent product and multiple products linked to
that parent in one request. This is useful for extras such as custom printing,
engraving, gift wrapping, or product-specific services.

When `linkedProducts` contains items, Ekom always creates a new parent order line.
Each linked product is also created as a separate new order line and its
`Settings.Link` targets the generated parent line key. Product and variant prices,
stock validation, and discounts work as they do for ordinary order lines.

The operation validates and stages the complete group before updating the basket.
If a product, variant, quantity, stock check, pre-add event, discount, or persistence
operation fails, none of the proposed lines are added. Added/updated notifications run
after persistence; notification failures are logged and do not roll back a successful
basket update. Existing requests without `linkedProducts` continue to use the ordinary
single-line behavior.

## HTTP API

```http
POST /ekom/order/add
Content-Type: application/json
```

```json
{
  "productId": "11111111-1111-1111-1111-111111111111",
  "variantId": "22222222-2222-2222-2222-222222222222",
  "storeAlias": "store",
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

The response is the updated `IOrderInfo`, matching the existing add-to-order
endpoint. `action` is ignored when linked products are supplied because the parent
and children must have new, unambiguous line keys.

Each linked product supports:

| Field | Required | Description |
| --- | --- | --- |
| `productId` | Yes | Catalog product key. |
| `variantId` | When required by the product | Variant key belonging to the product. |
| `quantity` | No | Independent positive quantity; defaults to `1`. |
| `customData` | No | Data for this child line. Only keys beginning with `orderline` are stored in `OrderLineInfo.Properties`. |

`ekomUpdateInformation` is not accepted in a linked-products request because customer
updates use a separate persistence flow. Update customer information separately before
or after adding the linked group.

## Razor MVC form

The form can collect an extra value and submit the nested JSON expected by the
existing endpoint. Replace the example product keys with values from your model.

```cshtml
<form id="add-personalized-shirt">
    <input type="hidden" name="storeAlias" value="@Model.Product.Store.Alias" />
    <input type="hidden" name="productId" value="@Model.Product.Key" />
    <input type="number" name="quantity" value="1" min="1" />

    <label for="marking-name">Name printed on the shirt</label>
    <input id="marking-name" name="markingName" maxlength="50" />

    <button type="submit">Add to basket</button>
</form>

<script>
    document.getElementById('add-personalized-shirt').addEventListener('submit', async event => {
        event.preventDefault();

        const form = new FormData(event.currentTarget);
        const payload = {
            storeAlias: form.get('storeAlias'),
            productId: form.get('productId'),
            quantity: Number(form.get('quantity')),
            linkedProducts: [
                {
                    productId: '@Model.MarkingProductKey',
                    quantity: 1,
                    customData: {
                        orderlineName: form.get('markingName')
                    }
                }
            ]
        };

        const response = await fetch('/ekom/order/add', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        if (!response.ok) {
            throw new Error(await response.text());
        }

        const basket = await response.json();
        console.log(basket);
    });
</script>
```

## Basket behavior

- Parent and child quantities are independent; changing one does not change another.
- A linked child can be edited or removed independently.
- Removing the parent also removes its directly linked children.
- Linked children count toward quantity and monetary totals like ordinary products.
- Only direct parent-to-child links are created; nested linked groups are not supported
  by this endpoint.

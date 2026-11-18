[Ekom docs](../../README.md) / [Guides](../README.md) / [Discounts and coupons](../discounts.md) / Product discounts

# Product discounts

A product discount puts the products or categories you select on sale. The lower price shows on the product page and in the basket, and the customer does not need a code. Use it for a sale such as "15% off outdoor furniture". For an offer that needs a coupon code or a minimum quantity, use an [order discount](order-discounts.md) instead.

When several product discounts fit the same product, Ekom does not add them together. It uses the one that gives the lowest price.

On this page:

- [Create a product discount](#create-a-product-discount)
- [What each field means](#what-each-field-means)
- [Example: 15% off a category, except one product](#example-15-off-a-category-except-one-product)
- [Example: a fixed amount off each item](#example-a-fixed-amount-off-each-item)
- [Set a sale price on one product](#set-a-sale-price-on-one-product)
- [Check the result](#check-the-result)

## Create a product discount

1. In the **Content** section, open **Ekom > Discounts > Product Discounts**. If your discounts are grouped in folders, open the folder you want.
2. Create a **Product Discount**. Give it a name that says what it does, for example "15% off outdoor furniture".
3. On the **Settings** tab, fill in the fields. The table in the next section explains each one.
4. In **Discount**, enter the amount for each store and currency. The field shows one box per store, with one row per currency.
5. On the **Stores** tab, turn on **Disable** for every store where the sale must not run.
6. Save and publish.

**Type** and **Discount** are required.

When **Type** is **Percentage**, the **Discount** field shows `%` after each box, for example `EUR [15] %`. Enter `15` for 15%, not `0.15`. When **Type** is **Fixed**, there is no `%` and the number is an amount in that row's currency. Changing **Type** does not change the number you typed, so check it again after you switch.

If you leave the amount at `0` for a store or currency, the discount does nothing there.

## What each field means

| Field | What it means | Example |
| --- | --- | --- |
| Title | The name of the offer. Your storefront can show it. | "15% off outdoor furniture" |
| Description | Text about the offer. Your storefront can show it. The text does not limit the discount. | "All outdoor furniture except the premium chair." |
| Type | **Percentage** takes a share of the price. **Fixed** takes an amount off each unit. | **Percentage** |
| Discount | How much to take off, per store and currency. | `15` for 15% |
| Discount Items | The products and categories that get the discount. A category covers the products under it in the content tree and products linked to that category. If you select nothing, no product gets the discount. | The "Outdoor furniture" category |
| Exclude Discount Items | Products and categories that never get this discount, even when they are inside a selected category. | The premium chair |
| Start of Range | The lowest normal unit price that gets the discount. A product priced exactly at this value is included. This is the price of one product, not a basket total. | `100`: only products that cost 100 or more |
| End of Range | The highest normal unit price that gets the discount. A product priced exactly at this value is included. `0` means no upper limit. | `500`, or `0` for no limit |
| Disable (Stores tab) | Turns the discount off in the stores where it is on. The settings are kept. | On for the stores where the sale is paused |

Two more settings on the product itself affect discounts:

- **Disable Discounts** on a product stops that product and its variants from getting any discount, including order discounts.
- **Discount Price** on a product or variant is a sale price. See [Set a sale price on one product](#set-a-sale-price-on-one-product).

## Example: 15% off a category, except one product

**Goal:** put outdoor furniture on sale, but keep the premium chair at its normal price.

| Field | Value |
| --- | --- |
| Type | Percentage |
| Discount | `15` in each store and currency where the sale runs |
| Discount Items | Outdoor furniture category |
| Exclude Discount Items | Premium chair |
| Start of Range | `0` |
| End of Range | `0` (no upper limit) |
| Disable | Off for the stores where the sale runs |

**Result:** a product in the category that normally costs 100 now costs 85. The premium chair and products outside the category keep their normal price. If another product discount gives a bigger reduction on a product, that one is used instead.

**Check:** add one included product, the premium chair, one unrelated product and one product that is already on sale. Then switch to another store and check that the sale is not running there by mistake.

## Example: a fixed amount off each item

**Goal:** take 10 off each unit of the selected products.

1. Set **Type** to **Fixed**.
2. Enter `10` in **Discount** for each currency.
3. Select the products in **Discount Items**.

| Basket | Result from this discount |
| --- | --- |
| 1 unit that normally costs 100 | Unit price 90. Saving 10. |
| 3 units that normally cost 100 each | Unit price 90. Saving 30. |
| 1 unit that normally costs 8 | Unit price 0. The price never goes below 0. |

The amount comes off each unit, not once from the basket total. These results assume there is no other discount on the products.

## Set a sale price on one product

Products and variants have a **Discount Price** field, directly below **Price**. It holds the price you want to sell at, not the amount to take off.

**Example:** the normal price is 100 and you want to sell at 85. Enter `85`, not `15`.

1. Open the product, or the variant if the variant has its own price.
2. In **Discount Price**, enter the sale price for each store and currency.
3. Save and publish.
4. Check the price on the product page and in the basket.

How Ekom uses the value:

- A sale price above 0 and below the normal price becomes one more product discount. Ekom compares it with the product discounts that fit the product and uses the lowest price. It does not add them together.
- An empty field, `0`, or a price that is not below the normal price has no effect.
- The sale price is in the same form as **Price**: with VAT or without VAT, as your store is set up.
- A variant with its own price uses its own **Discount Price**. A variant without its own price uses the product's price, and also the product's **Discount Price** and product discounts.
- **Disable Discounts** on the product also turns off the sale price.

## Check the result

Product discounts have no start or end date field. To run a sale for a set period, publish and unpublish the discount at the right time.

After you publish, check on the storefront:

1. A product that is included, one that is excluded, and one that is not selected.
2. Products priced at, inside and outside **Start of Range** and **End of Range**, if you set them.
3. A basket with more than one unit, to see the saving per unit.
4. Variants, each currency, and each store.
5. A product that also gets an [order discount](order-discounts.md#stackable-adds-to-a-sale-price-or-competes-with-it).
6. A product with **Disable Discounts** turned on.

## Related pages

- [Order discounts](order-discounts.md): offers on the basket, with or without a code.
- [Configure discounts in Umbraco](editors.md): choose the right kind of discount.
- [Discount APIs and behavior](developers.md#product-discounts): custom rules for product discounts, for developers.
- [Database price storage](../database-price-storage.md): where prices and sale prices come from when they are stored in the database.

[Back to Guides](../README.md)

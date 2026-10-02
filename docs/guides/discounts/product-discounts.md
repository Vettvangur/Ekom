# Product discounts for editors

[Editor overview](editors.md) | [Order discounts](order-discounts.md) | [Coupons](coupons.md) | [Developer guide](developers.md)

Use a product discount to put selected products or categories on sale without requiring a coupon or a basket quantity rule. Ekom evaluates applicable product offers and selects the effective price reduction; it does not add several product discounts together.

For "buy at least three" offers or discounts activated by a code, use an [order discount](order-discounts.md) instead.

## Create a product discount

1. Open **Content → Ekom → Discounts → Product Discounts** and find the intended store's container.
2. Create a **Product Discount** with a descriptive name, such as "15% off outdoor furniture".
3. Complete **Settings** using the table below.
4. Confirm the intended store in **Stores** and enter the values under the matching store/currency fields where provided.
5. Review exclusions, ranges, and Disable before saving and publishing.

Use the equivalent location and publishing action if your site's Umbraco setup differs.

## Understand the fields

| Field | What it means | When to use it / example |
| --- | --- | --- |
| Title | Name of the offer. | "15% off outdoor furniture" is easier to maintain than "Discount 1". |
| Description | Explanation of the offer, where displayed by your storefront. | Describe the sale and exclusions. Text alone does not restrict the discount. |
| Type | Percentage or fixed monetary reduction. | Choose Percentage for 15% off, or the fixed-amount type for 10 off each unit. |
| Discount | Value of the reduction. | Enter `15` for 15%, not `0.15`. A fixed value uses the configured store currency. |
| Discount Items | Products/categories eligible for the offer. | Select an outdoor furniture category to include matching products. An empty selection does not mean all products. |
| Exclude Discount Items | Products/categories removed from the selected offer. | Select a premium chair to exclude it from a category sale. Exclusions take precedence over inclusion. |
| Start of Range | Minimum product unit price for eligibility, inclusive. | Limit the sale to products priced at least 100. This is not a minimum basket total. |
| End of Range | Maximum product unit price for eligibility, inclusive. | Set `500` to cap eligible unit prices at 500, or `0` for no upper limit. |
| Disable | Turns off the discount for the relevant store. | Pause the offer without deleting its settings. |

Selecting a category can include products under its path or linked to that category. Test the actual catalog selection on your site. A product's own **Disable Discounts** setting can also prevent reductions.

## Example: 15% off a category, except one product

**Goal:** put outdoor furniture on sale, but leave the premium chair at its normal price.

| Setting | Value |
| --- | --- |
| Type | Percentage |
| Discount | `15` |
| Discount Items | Outdoor furniture category |
| Exclude Discount Items | Premium chair |
| Start of Range | `0` |
| End of Range | `0` (no upper limit) |
| Disable | Off for the intended store |

**Expected result:** an included product normally priced at 100 is reduced to 85. The excluded chair and unrelated products receive no reduction from this offer. Another applicable, better product discount can win instead.

**Test:** one included item, the excluded chair, an unrelated item, and an item already on sale. Check another store to ensure the promotion has not been enabled there unintentionally.

## Example: a fixed amount off every eligible unit

**Goal:** take 10 off each eligible unit, rather than 10 off the entire basket.

Choose the fixed-amount **Type**, enter **Discount = 10**, and select the products in **Discount Items**.

| Basket | Expected result from this offer |
| --- | --- |
| One eligible unit normally priced at 100 | Unit price 90; saving 10. |
| Three eligible units normally priced at 100 each | Unit price 90; total saving 30. |
| One eligible unit normally priced at 8 | Unit price is clamped at 0, not made negative. |

These simplified examples assume no VAT adjustment and no competing reduction. Your site's fixed-discount VAT policy and rounding can change the displayed saving; check the final prices in your storefront.

## Set a target selling price

Products and variants can have a native **Discount Price** field (the exact label may vary by site). This means **the price you want to sell at**, not an amount to subtract.

**Example:** normal price 100, target Discount Price 85. Enter `85`, not `15`.

This example assumes no before-VAT adjustment to the fixed reduction. Internally, the target becomes an amount off the normal price. If your site applies fixed reductions before VAT to VAT-inclusive prices, the final price can differ from the entered target. Ask your developer to confirm the VAT policy and check the actual storefront and basket prices before advertising an exact selling price.

1. Open the relevant product or independently priced variant.
2. Find Discount Price and enter the target for the intended store/currency.
3. Save and publish, then check the displayed price and basket price.

A positive target below the normal price becomes a candidate product reduction. It competes with configured product discounts rather than stacking on top. Blank, zero, negative, invalid, or non-reducing values do not create a native reduction.

An independently priced variant uses its own target. A variant inheriting the parent's price also inherits the parent's selected discount; do not assume it uses an independent target price.

## Publish and verify

- Check included, excluded, and unrelated products.
- Test at the price-range boundaries and outside them, if used.
- Test multiple units to confirm percentage/fixed savings.
- Check variants, other currencies, and another store.
- Test with another product offer and with an [order discount](order-discounts.md#understand-stackable).
- Check products with Disable Discounts enabled.

The standard discount fields do not include an Ekom date-range setting. If using Umbraco publish/unpublish scheduling, confirm that scheduling workflow separately rather than assuming the discount has a built-in expiry field.

For APIs, custom audience restrictions, and imported native prices, see [Product discounts for developers](developers.md#product-discounts).

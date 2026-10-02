# Order discounts for editors

[Editor overview](editors.md) | [Product discounts](product-discounts.md) | [Coupons](coupons.md) | [Developer guide](developers.md)

Use an order discount for an offer evaluated against the basket: for example, 10% off selected items with a code, or an automatic 20% reduction after a quantity requirement is met.

An order discount applies to eligible order lines. It is not automatically a reduction on every basket item, and it does not imply a discount on shipping fees.

## Create the discount

1. Open **Content → Ekom → Discounts → Order Discounts** and find the intended store's container.
2. Create an **Order Discount**. Give the node a descriptive name, such as "20% off eligible basket items when buying 3 bundles".
3. Complete **Settings** using the table below.
4. Confirm the store assignment in **Stores**, and enter values under the matching store/currency fields where provided.
5. Review any disabled state, ranges, exclusions, and stock restrictions before publishing.
6. For an automatic offer, switch **Global Discount** on and leave coupon codes out of that discount. For a code-based offer, leave it off and follow the [coupon guide](coupons.md).

Labels and node locations may vary with your site's Umbraco setup.

In Umbraco 17 and 18, the Discount input keeps the currency label before the input and shows `%` after it when Type is Percentage, for example `ISK [20] %`. Fixed discounts have no percentage suffix. The percentage value is still entered as `20` for 20%. Changing Type changes the unit display, not the entered number. Start of Range, End of Range, and other range fields keep their currency labels without a percentage suffix.

## Understand the fields

| Field | What it means | When to use it / example |
| --- | --- | --- |
| Title | Name of the offer. | Use a meaningful name such as "20% off bundles with 3 items". |
| Description | Explanation of the offer, where displayed by your storefront. | Describe the selected items and minimum quantity. Text does not enforce conditions; configure the fields too. |
| Type | Whether Discount is a percentage or a fixed monetary reduction. | Percentage for 20% off; fixed amount for a reduction in the store currency. |
| Discount | Size of the reduction in the selected type. | Enter `20` for 20%, not `0.20`. A currency label beside the field does not make it monetary when Type is Percentage. |
| Discount Items | For a non-global offer, products/categories whose eligible units receive the reduction. Global offers bypass this inclusion selection. | Select the bundle category for a category-only coupon offer. See the Global Discount warning below for automatic offers. |
| Exclude Discount Items | Items removed from the reward selection. | Select a premium product to leave it out of a category-wide offer. |
| Stackable | Whether this order discount can apply on top of the selected product price reduction. | Leave off for competing offers; switch on only when you intend an additional reduction on sale prices. |
| Global Discount | Evaluates a couponless offer automatically and broadens reward targeting to all eligible, non-excluded lines. | Switch on for an automatic basket-wide offer, not to restrict rewards to Discount Items. |
| Quantity Discount Mode | Controls whether and how quantity unlocks rewards. | Choose None, Threshold, or Repeating as described below. |
| Qualifying Items | Products/categories whose whole-unit quantities count towards the requirement. | Select products customers must buy to unlock the offer. |
| Required Quantity | Qualifying whole units needed to activate a threshold or complete a repeating group. | `3` means three qualifying units, not three different products or three orders. |
| Reward Quantity | Maximum eligible whole units discounted per completed group in Repeating mode. | `1` gives one discounted eligible unit for each completed qualifying group. |
| Start of Range | Minimum qualifying basket amount, inclusive. | Require eligible line amounts to total at least 100 before the offer applies. |
| End of Range | Maximum qualifying basket amount, inclusive; `0` means no upper limit. | Limit an offer to smaller baskets or leave `0` for no maximum. |
| Disable | Turns off the discount for the relevant store. | Stop an offer without deleting its configuration. |

The range amount is the sum of current line amounts for products that allow discounts. It is not restricted to Discount Items and does not include shipping/payment fees. Existing product reductions can affect this amount.

### Global Discount changes the reward selection

> **Global does more than remove the need for a code.** It bypasses Discount Items when choosing reward lines. Eligible products outside that selection can receive the discount; Exclude Discount Items and products' Disable Discounts settings still prevent rewards.

For an automatic offer limited to certain rewards, explicitly exclude unwanted items/categories and test a mixed basket. If exclusions cannot express the intended offer, ask your developer about custom eligibility or use a non-global coupon offer. Threshold and Repeating still require Discount Items to be populated for a valid rule, even though Global Discount bypasses that selection when choosing rewards.

Keep automatic discounts couponless. Adding any coupon codes to the discount prevents automatic global application, even if those codes have no uses left.

### Qualifying items are not the same as discounted items

**Qualifying Items** answers "What must the customer buy?" For non-global offers, **Discount Items** answers "What receives the reduction?" Global offers broaden the reward selection as described above.

- For a non-global coupon offer where buying bundle items should discount those same items, select them in both fields.
- For a non-global coupon offer where buying three main products should unlock a discount on one accessory, select the main products as Qualifying Items and the accessory as Discount Items. The accessory must also be in the basket to receive a reduction.
- An unrelated product does not count towards a quantity rule merely because it is in the basket.

Select the intended items explicitly rather than relying on an empty selection to mean "everything".

Exclusions remove reward targets, not qualifying counts. An excluded reward product can still count towards the requirement if it matches Qualifying Items. Products with Disable Discounts on are omitted from both qualification and rewards.

## Choose the quantity mode

| Mode | Use case | Settings needed |
| --- | --- | --- |
| None | A normal offer without a quantity qualification rule, such as a 10% coupon. | Configure the discount amount and eligible items; quantity-rule fields are not used. |
| Threshold | Once the minimum is reached, discount all whole eligible reward units. | Discount Items, Qualifying Items, and a positive Required Quantity. |
| Repeating | Each complete qualifying group unlocks a limited number of discounted units. | Discount Items, Qualifying Items, a positive Required Quantity, and a positive Reward Quantity. |

Both quantity modes require nonempty Discount Items, even for a global offer. For global offers, this validates the rule but does not restrict reward targets.

For quantity modes, fractional quantities are rounded down for qualification and reward allocation. A product matching multiple qualifying selections is counted once, not once per matching category.

### Example: automatic 20% off when buying at least three

**Goal:** discount all whole eligible, non-excluded basket units once the customer buys at least three bundle units. This is a basket-wide automatic offer, not a bundle-only reward restriction.

| Setting | Value |
| --- | --- |
| Type | Percentage |
| Discount | `20` |
| Discount Items | Bundle products/category (required for a valid quantity rule; does not restrict global rewards) |
| Global Discount | On; no coupon codes |
| Stackable | Off |
| Quantity Discount Mode | Threshold |
| Qualifying Items | The same bundle products/category |
| Required Quantity | `3` |
| Reward Quantity | Not used in Threshold mode |

**Expected result:** a basket containing only two bundle units gets no reduction from this offer. Three qualify. Four also qualify, and all four eligible units receive the reduction, assuming no competing discount or other restriction prevents it. Adding an unrelated eligible item after qualification also discounts that item unless it is excluded.

### Example: repeat the offer for complete groups

**Goal:** three qualifying bundle units unlock up to three discounted eligible basket units; an incomplete qualifying group does not unlock more rewards.

Use the same settings as above, but choose **Repeating**, with **Required Quantity = 3** and **Reward Quantity = 3**.

| Bundle units in the basket | Threshold: required 3 | Repeating: required 3, reward 3 |
| --- | --- | --- |
| 2 | 0 discounted units | 0 discounted units |
| 3 | 3 discounted units | 3 discounted units |
| 4 | 4 discounted units | 3 discounted units |
| 6 | 6 discounted units | 6 discounted units |

This comparison assumes the basket contains only eligible bundle items, quantities are whole units, and no other restriction or competing discount changes the result. In a mixed basket, global rewards can go to other eligible items, cheapest first.

### Example: one discounted unit per three qualifying units

Choose **Repeating**, **Required Quantity = 3**, and **Reward Quantity = 1**. Set the percentage or fixed reduction you want, and select qualifying and discounted items explicitly.

- Three qualifying units unlock up to one discounted eligible unit.
- Six unlock up to two.
- If qualifying and discounted items are the same, a basket of three can include the one discounted unit. This is not automatically "buy three full-price units and get a fourth discounted".
- When rewards are limited, Ekom assigns them to the cheapest eligible units first. Existing equal or better product discounts can change which units receive the reward.

## Understand Stackable

Suppose an eligible item normally costs 100, already has a product discount reducing it to 80, and the order offer is 10% off:

| Stackable | Intended result |
| --- | --- |
| Off | Offers compete. The product's 20% reduction is better than the order's 10%, so the item stays at 80. |
| On | The order reduction can apply to the selected discounted price: 10% off 80 gives 72. |

These simplified amounts assume the same currency and VAT treatment and no rounding differences. **Stackable does not mean "combine every offer"**; it describes interaction with product discounts, not unrestricted stacking of multiple order discounts.

For a fixed-amount offer, do not assume the configured value is deducted once from the final order total. It is a reduction on eligible units; test a basket with more than one eligible unit to confirm the intended promotion.

## Publish and verify

Save and publish the discount using the action available in your editor, then:

- For an automatic offer, test without entering any coupon.
- Test below, exactly at, and above the required quantity. For repeating rules, include an incomplete group.
- Test qualifying products with no reward products, and reward products with no qualifying products, if they are different selections.
- Add unrelated and excluded products. A non-global offer should respect Discount Items; a global offer can reward unrelated eligible products, but must respect exclusions.
- Test an existing product sale with Stackable off and on.
- Test another store and any configured range or stock restrictions.
- Check the basket's final totals and which units were discounted.

If the offer requires a code, continue with [Coupons](coupons.md). For runtime allocation and integration details, see the [developer guide](developers.md#order-discounts-and-quantity-rules).

# Order discounts

[Ekom docs](../../README.md) / [Guides](../README.md) / [Discounts and coupons](../discounts.md) / Order discounts

An order discount lowers the price of order lines in the basket. Use it for offers that depend on the basket, such as "10% off selected products with the code SPRING10" or "20% off when you buy at least 3". It can run automatically, or only when the customer enters a coupon code.

An order discount lowers the price of order lines only. It does not lower shipping or payment fees.

A basket holds one order discount at a time. When a second order discount fits, Ekom keeps the one that gives the lower basket total.

On this page:

- [Create an order discount](#create-an-order-discount)
- [What each field means](#what-each-field-means)
- [Global Discount rewards more than Discount Items](#global-discount-rewards-more-than-discount-items)
- [Qualifying Items and Discount Items are different](#qualifying-items-and-discount-items-are-different)
- [Choose a quantity mode](#choose-a-quantity-mode)
- [Stackable adds to a sale price or competes with it](#stackable-adds-to-a-sale-price-or-competes-with-it)
- [Check the result](#check-the-result)

## Create an order discount

1. In the **Content** section, open **Ekom > Discounts > Order Discounts**. If your discounts are grouped in folders, open the folder you want.
2. Create an **Order Discount**. Give it a name that says what it does, for example "20% off bundles when buying 3".
3. On the **Settings** tab, fill in the fields. The table in the next section explains each one.
4. In **Discount**, **Start of Range** and **End of Range**, enter the values for each store and currency. Each field shows one box per store, with one row per currency.
5. On the **Stores** tab, turn on **Disable** for every store where the offer must not run.
6. Decide how the offer starts:
   - **Automatic:** turn on **Global Discount**, and do not add any coupon codes.
   - **With a code:** leave **Global Discount** off, save, and then add codes on the **Coupons** tab. See [Coupons](coupons.md).
7. Save and publish.

**Type** and **Discount** are required.

When **Type** is **Percentage**, the **Discount** field shows `%` after each box, for example `EUR [20] %`. Enter `20` for 20%, not `0.20`. When **Type** is **Fixed**, there is no `%` and the number is an amount in that row's currency. Changing **Type** does not change the number you typed. **Start of Range** and **End of Range** are always amounts, never percentages.

## What each field means

| Field | What it means | Example |
| --- | --- | --- |
| Title | The name of the offer. Your storefront can show it. | "20% off bundles when buying 3" |
| Description | Text about the offer. Your storefront can show it. The text does not set any condition. | "Buy 3 bundle items and get 20% off." |
| Type | **Percentage** takes a share of the price. **Fixed** takes an amount off each discounted unit, not once per order. | **Percentage** |
| Discount | How much to take off, per store and currency. | `20` for 20% |
| Discount Items | The products and categories that get the discount. If **Global Discount** is on, this selection does not limit who gets the discount. | The "Bundles" category |
| Exclude Discount Items | Products and categories that never get this discount, also when **Global Discount** is on. They still count towards **Required Quantity** if they match **Qualifying Items**. | A premium product |
| Stackable | On: the discount applies on top of a product discount. Off: the two compete and the lower price wins. It does not let a customer use two codes together. | Off |
| Global Discount | On: the offer applies automatically, without a code, and can reward every product that is not excluded. | On for an automatic offer |
| Quantity Discount Mode | **None**, **Threshold** or **Repeating**. Empty means **None**. See [Choose a quantity mode](#choose-a-quantity-mode). | **Threshold** |
| Qualifying Items | The products and categories the customer must buy. Only whole units count. | The "Bundles" category |
| Required Quantity | How many qualifying units the customer must buy. In **Repeating** mode, the size of one group. | `3` means 3 units, not 3 different products |
| Reward Quantity | In **Repeating** mode, how many units get the discount for each complete group. Not used in other modes. | `1` |
| Start of Range | The lowest basket amount for the offer to apply. A basket exactly at this amount qualifies. | `100` |
| End of Range | The highest basket amount for the offer to apply. A basket exactly at this amount qualifies. `0` means no upper limit. | `0` |
| Disable (Stores tab) | Turns the offer off in the stores where it is on. The settings are kept. | On for paused stores |

The basket amount used by **Start of Range** and **End of Range** is the total of all order lines, after product discounts, except products with **Disable Discounts** turned on. It counts every line, not only **Discount Items**. Shipping and payment fees are not counted.

If the basket changes and the offer no longer qualifies, for example because the customer removes items, Ekom removes the offer from the basket. For an offer with a code, the code is removed too, and the customer must enter it again after the basket qualifies.

Products with **Disable Discounts** turned on never get an order discount and never count towards **Required Quantity**.

## Global Discount rewards more than Discount Items

> **Global Discount does more than remove the need for a code.** When it is on, Ekom ignores **Discount Items** when it decides which lines get the discount. Every product in the basket can get the discount, unless it is in **Exclude Discount Items** or has **Disable Discounts** turned on.

To limit an automatic offer to some products, list the products you do not want in **Exclude Discount Items**, and test a basket with mixed products. If you cannot describe the offer with exclusions, use an offer with a code (Global Discount off), or ask your developer for a custom rule.

**Threshold** and **Repeating** still need at least one item in **Discount Items**, also when **Global Discount** is on. Without it, the offer never applies.

Keep automatic offers free of coupon codes. If an order discount has any coupon code, even one with no uses left, Ekom does not apply it automatically.

Ekom checks automatic offers each time a basket is saved, for example when the customer adds a product. A basket that already exists gets a new automatic offer the next time it changes.

## Qualifying Items and Discount Items are different

- **Qualifying Items** answers: what must the customer buy?
- **Discount Items** answers: what gets cheaper? (When **Global Discount** is off.)

Two common setups, both with **Global Discount** off:

- **Buy 3 bundle items, get the bundle items cheaper:** select the bundles in both fields.
- **Buy 3 main products, get an accessory cheaper:** select the main products in **Qualifying Items** and the accessory in **Discount Items**. The accessory must also be in the basket. Ekom does not add it for the customer.

A product that is in the basket but not in **Qualifying Items** does not count towards the quantity. A product that matches more than one selection in **Qualifying Items** is counted once. If you leave a field empty, it matches nothing; it does not mean "all products".

## Choose a quantity mode

| Mode | Use it for | Fields you must fill in |
| --- | --- | --- |
| None | An offer without a quantity rule, such as 10% off with a code. Every unit of every matching line gets the discount. | **Discount Items** (or **Global Discount** on) |
| Threshold | Once the customer reaches the quantity, every whole unit that matches gets the discount. | **Discount Items**, **Qualifying Items**, **Required Quantity** above 0 |
| Repeating | Each complete group unlocks a set number of discounted units. | **Discount Items**, **Qualifying Items**, **Required Quantity** above 0, **Reward Quantity** above 0 |

If a required field is empty or 0, the offer never applies.

In **Threshold** and **Repeating**, only whole units count, both for qualifying and for the discount. A line with 2.5 units counts as 2.

When the number of discounted units is limited, Ekom gives the discount to the cheapest matching units first. If a unit already has an equal or bigger product discount, Ekom skips it and gives the discount to the next unit.

### Example: automatic 20% off when buying at least 3

**Goal:** once the customer has 3 bundle units in the basket, take 20% off. With **Global Discount** on, this is an offer on the whole basket, not only on bundles.

| Field | Value |
| --- | --- |
| Type | Percentage |
| Discount | `20` |
| Discount Items | Bundles category (required for the rule, but does not limit the reward) |
| Global Discount | On, with no coupon codes |
| Stackable | Off |
| Quantity Discount Mode | Threshold |
| Qualifying Items | Bundles category |
| Required Quantity | `3` |
| Reward Quantity | Not used in **Threshold** |

**Result:** with 2 bundle units, nothing changes. With 3 or 4 bundle units, every unit gets 20% off. If the customer then adds another product that is not excluded, that product also gets 20% off.

### Example: repeat the offer for each complete group

Use the same fields, but set **Quantity Discount Mode** to **Repeating**, **Required Quantity** to `3` and **Reward Quantity** to `3`.

| Bundle units in the basket | Threshold, Required Quantity 3 | Repeating, Required Quantity 3, Reward Quantity 3 |
| --- | --- | --- |
| 2 | 0 units discounted | 0 units discounted |
| 3 | 3 units discounted | 3 units discounted |
| 4 | 4 units discounted | 3 units discounted |
| 6 | 6 units discounted | 6 units discounted |

This table assumes the basket holds only bundle units, in whole numbers, with no other discount. In a mixed basket with **Global Discount** on, the discounted units can be other products, cheapest first.

### Example: 1 discounted unit for every 3

Set **Quantity Discount Mode** to **Repeating**, **Required Quantity** to `3` and **Reward Quantity** to `1`. Select the qualifying and the discounted products.

- 3 qualifying units unlock 1 discounted unit. 6 unlock 2.
- If the qualifying and the discounted products are the same, the discounted unit is one of the 3. This is "buy 3, one of them cheaper", not "buy 3 at full price, get a fourth cheaper".
- The cheapest matching unit gets the discount first.

## Stackable adds to a sale price or competes with it

A product normally costs 100. A product discount already lowers it to 80. The order discount is 10% off.

| Stackable | Result |
| --- | --- |
| Off | The two discounts compete. 80 is lower than 90 (10% off 100), so the product stays at 80. |
| On | The order discount applies to the sale price: 10% off 80 gives 72. |

When the two give the same price, the product discount is kept. **Stackable** only decides how an order discount works with product discounts. A basket still holds only one order discount.

For a **Fixed** order discount, the amount comes off each discounted unit. Test a basket with more than one unit to see the full saving.

## Check the result

After you publish, check on the storefront:

1. For an automatic offer, check that it appears without entering a code.
2. Test a basket below, exactly at and above **Required Quantity**. For **Repeating**, also test an incomplete group, for example 4 units when the group is 3.
3. If **Qualifying Items** and **Discount Items** differ, test a basket with only qualifying products, and one with only discounted products.
4. Add a product that is not selected, and one that is excluded. Without **Global Discount**, only **Discount Items** get cheaper. With **Global Discount**, every product that is not excluded can get cheaper.
5. Test a product that is already on sale, with **Stackable** off and on.
6. Test another store, and a basket outside **Start of Range** and **End of Range**, if you set them.
7. Check the basket total and which lines got the discount.

If the offer needs a code, continue with [Coupons](coupons.md).

## Related pages

- [Coupons](coupons.md): add codes to an order discount.
- [Product discounts](product-discounts.md): sales on products that need no code.
- [Configure discounts in Umbraco](editors.md): choose the right kind of discount.
- [Discount APIs and behavior](developers.md#order-discounts-and-quantity-rules): how order discounts are calculated, for developers.

[Back to Guides](../README.md)

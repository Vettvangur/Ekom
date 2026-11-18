# Configure discounts in Umbraco

[Ekom docs](../../README.md) / [Guides](../README.md) / [Discounts and coupons](../discounts.md) / Configure discounts in Umbraco

This page helps you, as a store editor, choose the right kind of discount for an offer and plan it before you open the backoffice. Each kind has its own page with step-by-step instructions.

On this page:

- [Choose the right kind of discount](#choose-the-right-kind-of-discount)
- [Find discounts in the backoffice](#find-discounts-in-the-backoffice)
- [Plan the offer before you start](#plan-the-offer-before-you-start)
- [Usage limits are not a customer limit](#usage-limits-are-not-a-customer-limit)
- [Check the offer before customers see it](#check-the-offer-before-customers-see-it)

## Choose the right kind of discount

| What you want | Use | Example |
| --- | --- | --- |
| Put chosen products on sale, without a code. | [Product discount](product-discounts.md) | 15% off a category, except one product. |
| Sell one product at a set sale price. | [Discount Price field](product-discounts.md#set-a-sale-price-on-one-product) | A product that normally costs 100 sells for 85. |
| Give an offer automatically when the basket qualifies. | [Order discount](order-discounts.md) with **Global Discount** on | 20% off when the customer buys at least 3 bundle items. |
| Give an offer only to customers who enter a code. | [Order discount](order-discounts.md) with a [coupon](coupons.md) | The code `SPRING10` gives 10% off selected products. |
| Limit how many times a code can be used. | [Usage limits on a coupon](coupons.md#understand-usage-limits) | A campaign code that can be used 100 times in total. |

A coupon is not a separate kind of discount. A coupon code switches on one order discount. The order discount holds the amount, the products and the conditions.

## Find discounts in the backoffice

Discounts are content nodes in the **Content** section:

- **Ekom > Discounts > Product Discounts** holds product discounts.
- **Ekom > Discounts > Order Discounts** holds order discounts and their coupon codes.

Inside each of these you can create the discount itself, or a folder (**Product Discounts Folder** or **Order Discounts Folder**) to group discounts. A folder has its own **Disable** setting per store on its **Stores** tab. Turning on **Disable** for a store on a folder turns off every discount inside that folder in that store.

A new discount is active in every store. To keep it out of a store, open the **Stores** tab on the discount (or on its folder) and turn on **Disable** for that store.

## Plan the offer before you start

Write down these six things first. Each one maps to a field.

1. **Stores and currencies.** Which stores the offer runs in. Amounts are entered per store and per currency.
2. **What the customer gets.** A percentage off (for example 15%), a fixed amount off each discounted unit (for example 10 off each item), or a set sale price on one product.
3. **Which products get it.** The products or categories you select, and any you want to leave out.
4. **What switches it on.** Always on (product discount), automatic on the basket (Global Discount), a coupon code, or a minimum quantity.
5. **Whether it adds to a sale price.** On an order discount, **Stackable** decides whether it applies on top of a product discount or competes with it.
6. **Limits.** A price or basket range, how many times a code can be used, and whether the offer is disabled in some stores.

**Global Discount** does more than remove the need for a code. When it is on, the offer can reward every product in the basket that is not excluded, even products that are not in **Discount Items**. Read [Global Discount rewards more than Discount Items](order-discounts.md#global-discount-rewards-more-than-discount-items) before you set up an automatic offer for one category.

## Usage limits are not a customer limit

The **Usage limits** value on a coupon code counts uses of that code, for all customers together. It does not look at who the customer is, their email address or whether they are logged in. A code with a limit of `1` can be used once in total, not once per customer.

Ekom has no setting for "first order only", "members only" or "once per customer". Naming a code `WELCOME` or `MEMBERS` does not limit who can use it. If you need such a rule, ask your developer to add it. Read [Coupons](coupons.md) before you plan a campaign for certain customers only.

## Check the offer before customers see it

After you save and publish the discount, check it on the storefront:

1. Fill a basket that meets the offer's conditions, and one that does not. Compare the totals.
2. Add a product that is excluded or not selected. Check that its price did not change.
3. Add a product that already has a sale price. Check which price wins.
4. Switch to another store, and to each currency the store uses. Check that the offer only appears where you want it.
5. Look at the basket total, not only a label or badge on the page.

Order discounts lower the price of order lines only. They do not lower shipping or payment fees.

## Related pages

- [Product discounts](product-discounts.md): put products on sale and set sale prices.
- [Order discounts](order-discounts.md): automatic offers, quantity rules and Stackable.
- [Coupons](coupons.md): add and generate codes, and understand usage limits.
- [Discount APIs and behavior](developers.md): the developer page, for custom rules such as "once per customer".

[Back to Guides](../README.md)

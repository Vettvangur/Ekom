# Configure discounts in Umbraco

[Discounts overview](../discounts.md) | [Developer guide](developers.md)

Start with the offer you want customers to receive, then choose the discount type below. These guides explain the editor fields and show how to check the offer before making it available to customers.

## Choose the right discount

| Your goal | Use | Example |
| --- | --- | --- |
| Put selected products on sale without a code. | [Product discount](product-discounts.md) | 15% off a category, except one excluded product. |
| Give one product a specific selling price. | [Native discount price](product-discounts.md#set-a-target-selling-price) | Sell an item normally priced at 100 for 85. |
| Apply an offer automatically when a basket qualifies. | [Order discount](order-discounts.md) with Global Discount on | 20% off eligible items when buying at least three qualifying units. |
| Require customers to enter a code for an offer. | [Order discount](order-discounts.md) with a [coupon](coupons.md) | Enter SPRING10 for 10% off selected items. |
| Limit how many times a particular code can be used. | [Coupon usage limit](coupons.md#understand-usage-limits) | A shared campaign code with 100 uses available in total. |

**Coupons are not a third discount type.** A code activates an order discount; the discount defines the amount, eligible items, and conditions.

## Decide the offer before editing

Write down:

1. **Where it applies:** the store and currency.
2. **What customers receive:** a percentage reduction, a fixed amount off eligible units, or a target selling price.
3. **Which items receive it:** selected products or categories, with any exclusions.
4. **What activates it:** automatic evaluation, a coupon, or a minimum qualifying quantity.
5. **Whether it combines with a sale price:** an order discount's Stackable setting controls interaction with product discounts.
6. **Any restrictions:** configured ranges, disabled state, usage limits, or discount stock.

Global Discount means a couponless order offer is evaluated automatically **and bypasses the Discount Items inclusion selection**. It can reward all eligible, non-excluded basket lines, but it still respects its store, qualification, and other restrictions. Read the [global targeting warning](order-discounts.md#global-discount-changes-the-reward-selection) before configuring a category-specific automatic offer.

## Important: usage is not a customer restriction

A coupon usage limit counts how many times **that code** can be used in total. It is not based on the customer, email address, or logged-in account. A single-use code is single-use overall, not once per customer.

If you need "first order only", "members only", or "once per customer", ask your developer about custom eligibility rules. Naming a coupon WELCOME or MEMBERS does not enforce those rules. Read the [coupon guide](coupons.md) before planning a customer-specific campaign.

## Follow the relevant setup guide

- [Product discounts](product-discounts.md): product sales and target selling prices.
- [Order discounts](order-discounts.md): automatic offers, quantity rules, and stacking.
- [Coupons](coupons.md): code-based offers, generation, and usage limits.

The guides use generic store, product, and price examples. Replace them with your own values. Node locations and publishing actions can differ between Umbraco versions and site setups; use the matching store container and available content type in your editor.

## Check the customer experience

After saving and publishing the relevant discount content:

- Check a basket that should qualify and one that should not.
- Add an unrelated or excluded product and inspect its price.
- Check another store and any other configured currencies.
- Test a product that already has a discount.
- Check the final basket totals, not only a promotional label.

Order discounts apply to eligible order lines. Do not assume they reduce shipping or installation fees; configure and test those services separately.

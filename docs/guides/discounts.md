# Discounts and coupons

[Ekom docs](../README.md) / [Guides](README.md) / Discounts and coupons

Ekom lowers prices in two ways: a product discount lowers the price of a product before it reaches the basket, and an order discount lowers the price of order lines once they are in the basket. Use this page to find the right discount guide for your task.

| Kind | What it does | How it starts |
| --- | --- | --- |
| Product discount | Lowers the price of the products or categories you select. The lower price shows on the product page and in the basket. | Always on for the selected products, in every store where it is not disabled. |
| Discount Price field on a product | A sale price that you type on the product or variant itself. Ekom treats it as one more product discount. | Always on while the field holds a price below the normal price. |
| Order discount | Lowers the price of order lines in the basket. It can require a minimum quantity or a basket amount. | Either automatically (Global Discount) or when the customer enters a coupon code. |

A coupon is not a third kind of discount. A coupon code switches on one order discount. The order discount decides the amount, the products and the conditions.

## Find the right guide

| Guide | Read it when you want to |
| --- | --- |
| [Configure discounts in Umbraco](discounts/editors.md) | Choose which kind of discount fits an offer, as a store editor. Start here if you work in the backoffice. |
| [Product discounts](discounts/product-discounts.md) | Put products or categories on sale, or give one product a sale price. |
| [Order discounts](discounts/order-discounts.md) | Give an offer on the basket, for example "20% off when you buy 3". |
| [Coupons](discounts/coupons.md) | Create coupon codes, generate many codes at once, and limit how often a code is used. |
| [Discount APIs and behavior](discounts/developers.md) | Apply coupons from code or over HTTP, add your own rules, and quote a coupon from another system. |

## Usage limits count uses of a code, not customers

The usage limit on a coupon code is the number of uses left for that code, for all customers together. A limit of `1` means the code can be used once in total, not once per customer. Ekom does not look at the customer, the email address or the login when it counts uses. See [Understand usage limits](discounts/coupons.md#understand-usage-limits).

## Related pages

- [Stock](stock.md): discount stock, a separate limit that developers manage.
- [Stock reservations](reservations.md): how checkout holds stock and discount stock while the customer pays.
- [Orders and baskets](orders-and-baskets.md): read the basket that discounts are applied to.
- [Checkout](checkout.md): where a coupon use is counted when the order completes.
- [Order reviews](order-reviews.md): the review flag Ekom raises when a coupon has run out before checkout completes.

[Back to Guides](README.md)

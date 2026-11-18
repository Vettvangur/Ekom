# Guides

[Ekom docs](../README.md) / Guides

Each guide covers one part of Ekom: what it does, how to set it up, and how to use it from your code. Read a guide when you are about to build that part of a store.

The guides assume that Ekom is installed and running. If it is not, start with [Getting started](../getting-started/README.md). To look up one setting, method, event or endpoint, use the [Reference](../reference/README.md) pages.

## Where to start

Building a first storefront? Read these four in order. Together they cover the path from a product page to a paid order.

1. [Stores](stores.md)
2. [Catalog and products](catalog-and-products.md)
3. [Orders and baskets](orders-and-baskets.md)
4. [Checkout](checkout.md)

## Stores and catalog

| Guide | Read it when you want to |
| --- | --- |
| [Stores](stores.md) | Set up stores, and work with the right store in requests and background jobs. |
| [Catalog and products](catalog-and-products.md) | Read categories, products and variants, and show them on a storefront. |
| [Variant App](variant-app.md) | Let editors manage the variants of a product in one view. |
| [Stock](stock.md) | Read and change stock, and control when a product is available. |
| [Database price storage](database-price-storage.md) | Update prices often from an ERP system without publishing products. |
| [Catalog imports](imports.md) | Bring products, categories, media and stock in from another system. |

## Baskets and orders

| Guide | Read it when you want to |
| --- | --- |
| [Orders and baskets](orders-and-baskets.md) | Add, change and remove lines, and read the current basket or a saved order. |
| [Linked order lines](linked-order-lines.md) | Sell add-ons that belong to another line, such as engraving or gift wrapping. |
| [Order custom data](order-custom-data.md) | Save your own values on an order, such as a gift message. |
| [Order, payment, and fulfillment statuses](order-statuses.md) | Understand what state an order is in and how it moves on. |
| [Order reviews](order-reviews.md) | Flag an order that a person needs to look at. |

## Checkout, payment and shipping

| Guide | Read it when you want to |
| --- | --- |
| [Checkout](checkout.md) | Take a basket through payment to a completed order. |
| [Payment and shipping providers](providers.md) | Set up the payment and shipping options a customer can choose. |
| [Payments](payments.md) | Add a payment gateway and follow what happened to each payment. |
| [Shipping and shipments](shipping.md) | Add a carrier and record shipments and tracking numbers. |
| [Stock reservations](reservations.md) | Hold stock for a customer while they pay. |
| [Failed gift-card preparation recovery](giftcard-recovery.md) | Recover when a gift-card hold succeeded but the checkout step failed. |

## Discounts

| Guide | Read it when you want to |
| --- | --- |
| [Discounts and coupons](discounts.md) | See how discounts work and find the right discount guide. Start here. |
| [Configure discounts in Umbraco](discounts/editors.md) | Set up an offer as a store editor. |
| [Product discounts](discounts/product-discounts.md) | Put products or categories on sale. |
| [Order discounts](discounts/order-discounts.md) | Give an offer on the basket, with or without a coupon code. |
| [Coupons](discounts/coupons.md) | Create coupon codes and limit how often they are used. |
| [Discount APIs and behavior](discounts/developers.md) | Apply coupons from code and add your own rules. |

## Storefront integration

| Guide | Read it when you want to |
| --- | --- |
| [Headless storefronts](headless.md) | Build a storefront that talks to Ekom over HTTP. |
| [Tracking and consent](tracking-and-consent.md) | Send purchase events to GA4 and Meta in line with the customer's consent. |

## Other sections

- [Getting started](../getting-started/README.md): install and configure Ekom.
- [Examples](../examples/README.md): working code you can copy for common tasks.
- [Reference](../reference/README.md): the .NET API, HTTP API, events and settings.
- [Manager](../manager/README.md): the backoffice area for orders, customers and analytics.
- [Upgrading](../upgrading/README.md): move a site to a newer Umbraco or Ekom version.

[Back to the documentation home](../README.md)

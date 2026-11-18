# Examples

[Ekom docs](../README.md) / Examples

This section has working code for common tasks. Each example is a complete class or view that you can copy into your site and adapt. Every C# example was compiled against Ekom before it was added here.

Use an example when you know what you want to build and want to see it done. To understand why the code looks the way it does, follow the link to the guide at the end of each page. To look up one method, event or setting, use the [Reference](../reference/README.md).

The examples use one made-up shop throughout: a store with the alias `Store`, products with SKUs such as `TSHIRT-RED-M`, and a coupon code `SPRING10`. Replace these with your own values.

## Find an example

| You want to | Go to |
| --- | --- |
| Run your own code when an order is completed, paid, changed or fulfilled | [React to events](events.md) |
| Send orders or stock changes to an ERP system | [React to events](events.md) |
| Stop an add-to-basket or a checkout with your own rule | [React to events](events.md) |
| Add your own properties or prices to products, variants and categories | [Use your own product, variant and category classes](custom-models.md) |
| Give signed-in members their own prices | [Use your own product, variant and category classes](custom-models.md) |
| Change how Ekom filters, searches or checks stock | [Replace Ekom services](replace-services.md) |
| Import products with images, files and variants | [Import products from another system](imports.md) |
| Update prices or stock for many products quickly | [Import products from another system](imports.md) |
| Build category, product, basket and checkout pages | [Build storefront pages](storefront.md) |
| Add search, filters or a category menu to a storefront | [Build storefront pages](storefront.md) |
| Show the old price and the saving for a product on sale | [Build storefront pages](storefront.md) |
| Call Ekom from a headless storefront | [Build storefront pages](storefront.md) |
| Add a button to an order in Ekom Manager | [Add your own actions to the Manager](manager-actions.md) |
| Use Ekom from a scheduled job or queue worker | [Use Ekom outside a web request](background-jobs.md) |

## Pages in this section

| Page | What it shows |
| --- | --- |
| [React to events](events.md) | How to subscribe to Ekom's events, and handlers for orders, payments, status changes, stock and providers. |
| [Use your own product, variant and category classes](custom-models.md) | How to make Ekom create your own classes, with extra properties and your own price rules. |
| [Replace Ekom services](replace-services.md) | How to swap one of Ekom's services for your own through dependency injection. |
| [Import products from another system](imports.md) | How to bring in categories, products, images, files, variants, prices and stock, and how to run an import safely. |
| [Build storefront pages](storefront.md) | Views, controllers and `fetch` calls for the catalog, search and filters, the category menu, sale prices, the basket, coupons, checkout and the receipt. |
| [Add your own actions to the Manager](manager-actions.md) | How to add your own buttons to an order in the backoffice. |
| [Use Ekom outside a web request](background-jobs.md) | How to use Ekom from hosted services, queue handlers and scheduled jobs. |

## Before you copy an example

- Install and configure Ekom first. See [Getting started](../getting-started/README.md).
- Each example ends with a "What to know" list. Read it. It says when the code runs, what happens when it fails, and what it must not do.
- The examples are written for Umbraco 17, 18 and 19. A page says so when something differs between them.

## Other sections

- [Guides](../guides/README.md): how each part of Ekom works.
- [Reference](../reference/README.md): the .NET API, HTTP API, events and settings.
- [Samples](../samples/overview.md): the example sites in this repository, which you can run.

[Back to the documentation home](../README.md)

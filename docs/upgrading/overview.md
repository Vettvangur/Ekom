# Before and after an upgrade

[Ekom docs](../README.md) / [Upgrading](README.md) / Before and after an upgrade

This page covers what every Ekom upgrade has in common: which packages to install, what Ekom changes in the database when the site starts, how to deploy the backoffice files, and what to test afterwards. Read it once before you follow one of the Umbraco version pages.

Ekom does not upgrade Umbraco for you. An Umbraco major version upgrade also changes the database, the configuration and any custom code that uses Umbraco APIs. Follow Umbraco's own upgrade guide for that part, and use these pages for the Ekom part.

On this page:

- [Choose your packages](#choose-your-packages)
- [Prepare the upgrade](#prepare-the-upgrade)
- [What Ekom changes in the database at startup](#what-ekom-changes-in-the-database-at-startup)
- [Deploy the backoffice files](#deploy-the-backoffice-files)
- [Check code that runs when an order completes](#check-code-that-runs-when-an-order-completes)
- [Check the site after an upgrade](#check-the-site-after-an-upgrade)

## Choose your packages

Choose the Ekom packages by the Umbraco major version the site will run after the upgrade.

| Umbraco | Ekom runtime | Ekom web assets | Algolia, Klaviyo, Mailchimp |
| --- | --- | --- | --- |
| 17 | `Ekom.U17` | `Ekom.Web.U17` | `Ekom.Algolia`, `Ekom.Klaviyo`, `Ekom.Mailchimp` |
| 18 | `Ekom.U18` | `Ekom.Web.U18` | `Ekom.Algolia.U18`, `Ekom.Klaviyo.U18`, `Ekom.Mailchimp.U18` |
| 19 (release candidate) | `Ekom.U19` | `Ekom.Web.U19` | `Ekom.Algolia.U19`, `Ekom.Klaviyo.U19`, `Ekom.Mailchimp.U19` |

- The runtime package goes in the project that runs the site. The web-assets package goes in the site project that contains `wwwroot`. Both must have the same Umbraco suffix.
- The integration packages are optional. Install only the ones the site uses. For Umbraco 17 the package name has no suffix.
- Online payment gateways are separate `Ekom.Payments.<Provider>` packages, for example `Ekom.Payments.Borgun`. Ekom does not bring them in for you. Offline payments need no extra package. See [Upgrade payments](payments.md).
- The shipping framework is part of Ekom. If the site references `Ekom.Shipping`, `Ekom.Shipping.Core` or `Ekom.Shipping.U17`/`U18`, remove them and keep only the carrier packages. See [Upgrade shipping](shipping.md).

For the Umbraco versions each runtime package accepts and the .NET version each one needs, see [Compatibility](../getting-started/compatibility.md).

## Prepare the upgrade

Do these before you change any package:

1. Back up the database. Ekom's own database changes only add tables, columns and indexes, but the Umbraco upgrade that runs with them cannot be undone without the backup.
2. Write down the Ekom packages, payment provider packages, integration packages and versions the site uses now.
3. Read the entries marked BREAKING in the [Ekom changelog](https://github.com/Vettvangur/Ekom/blob/Ekom/Ekom/CHANGELOG.md) between the Ekom version you have and the one you install. For example, the order status change events were replaced by `OrderEvents.OrderStatusChangedAsync`, `PaymentStatusChangedAsync` and `FulfillmentStatusChangedAsync`.
4. Find the custom code that uses Umbraco APIs, Ekom events, or the Ekom backoffice files.
5. Rehearse the upgrade on a copy of the production database before you run it in production.

Ekom has no command or script that upgrades a site. The steps are package changes, code changes and a normal startup.

## What Ekom changes in the database at startup

Ekom keeps its database up to date itself. You do not run a script. On every start, after Umbraco is fully running, Ekom:

- creates any Ekom table that is missing,
- adds any column that is missing from an existing Ekom table, as an empty column or with a default value,
- creates any index that is missing.

It does not delete tables or columns and does not change existing rows. If Umbraco is still waiting for its own database upgrade, Ekom does nothing until a start where Umbraco runs normally.

Ekom records how far its setup has come in Umbraco's key-value table, under the key `Umbraco.Core.Upgrader.State+Ekom`. Do not edit this value.

### When startup stops

Some problems stop the site from starting, so that Ekom never runs against a database it cannot use safely. In each case Ekom throws `InvalidOperationException`, and no orders are changed.

| Message starts with | Cause | What to do |
| --- | --- | --- |
| `EkomOrders must have a primary key on ReferenceId.` | The `EkomOrders` table was changed by hand. | Restore the primary key on `ReferenceId`, then start the site again. |
| `EkomOrders contains duplicate UniqueId values.` | Two orders share one `UniqueId`, so Ekom cannot add the unique index it needs. | Find and resolve the duplicate rows, then start the site again. |
| `IX_EkomOrders_UniqueId exists with an incompatible definition.` | An index with that name exists but is not a unique index on `UniqueId` alone. | Drop or rename that index, then start the site again. |
| `Ekom database migration failed from state` | One step of Ekom's database setup failed. | Read the inner exception in the log for the cause. |
| `Unsupported database provider for EkomOrders indexes.` | The site does not use SQL Server or SQLite. | Use SQL Server or SQLite. |

Indexes that only make queries faster are different. If Ekom cannot create one, it logs a warning that starts with `Ekom could not create lookup index` and the site starts. Ekom tries again on the next start.

### Work that runs after startup

Ekom copies payments made with Ekom.Payments 1.0 and earlier onto their orders in a background job after the site has started. See [Upgrade payments](payments.md).

Existing orders keep the status they were saved with. Ekom reads the order, payment and fulfillment statuses from it when it loads an order. To store the new statuses on old orders, use the status migration described in [Order, payment, and fulfillment statuses](../guides/order-statuses.md).

## Deploy the backoffice files

`Ekom.Web.U17`, `Ekom.Web.U18` and `Ekom.Web.U19` put the backoffice scripts under a folder named after the package version:

```text
App_Plugins/Ekom/umbraco-package.json
App_Plugins/Ekom/releases/<package version>/...
```

The `umbraco-package.json` file at the top tells Umbraco which release folder to load. `dotnet build`, `dotnet publish` and `dotnet pack` create this layout. A local `npm run build` in the client project writes files without a version folder, for development only.

When you deploy:

1. Deploy `umbraco-package.json` and the whole release folder together.
2. Restart the site, so that Umbraco reads the package registration again.
3. Ask backoffice users to reload the page. A browser that still has the old backoffice open keeps asking for the old release folder.

Because each release has its own folder, you can let browsers and CDNs cache files under `releases/` for a long time. Two rules keep that safe:

- Never change files inside a release folder that is already deployed. Publish a new package version instead.
- Do not cache Umbraco's management API response that lists the installed packages. It must point to the new release after a deployment.

During a rolling deployment, keep the previous release folder on the servers until every backoffice session has reloaded.

## Check code that runs when an order completes

`CheckoutEvents.CompleteCheckout` and `CheckoutEvents.CompleteCheckoutAsync` are raised after the order has completed. Before, they were raised first, and an order could reach a handler and then fail to complete.

- A handler that sends the order to another system or sends an email needs no change. It now runs only for orders that completed, and `args.OrderInfo` shows the final status.
- A handler that sets `args.StockValidation` or `args.UpdateOrderStatus` no longer changes anything, and the build shows an obsolete warning for it. Move that code to a `CheckoutEvents.CompletingCheckoutAsync` handler, which has the same two properties.
- A handler that throws no longer stops the order from completing. Ekom logs the error and opens an order review with the code `completion-handler-failed`.

See [Checkout events](../guides/checkout.md#checkout-events).

## Check the site after an upgrade

Test these on the upgraded site before you open it to customers:

1. The site starts, and the log has no Ekom errors from the database setup.
2. Products, categories and prices show on the storefront.
3. The **Ekom** section opens in the backoffice for the users who should see it, and each user sees the orders of their own stores.
4. A customer can create a basket, choose shipping and payment, pay, return from the payment page, and get a completed order.
5. Stock and discount counts go down after an order. If stock reservations are on, check that a hold is released when a customer gives up, and consumed when they pay.
6. Each installed payment, shipping, search, marketing and ERP integration still works.

Ekom cannot test the site's own extensions. Add tests for the custom code the site has.

## Related pages

- [Compatibility](../getting-started/compatibility.md): the Umbraco and .NET versions each package needs.
- [Packages](../architecture/packages.md): every Ekom package and what it contains.
- [Startup and data](../architecture/startup-and-data.md): what Ekom does at startup and where it keeps its data.
- [Integrations](../integrations/README.md): set up the Algolia, Klaviyo and Mailchimp packages.
- [Manager access and permissions](../manager/access-and-permissions.md): who can open the Ekom section and which stores they see.

[Back to Upgrading](README.md)

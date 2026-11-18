# Upgrade from Umbraco 17 to 18

[Ekom docs](../README.md) / [Upgrading](README.md) / Upgrade from Umbraco 17 to 18

This page moves an Ekom site from Umbraco 17 to Umbraco 18. On the Ekom side you swap the Umbraco 17 packages for their Umbraco 18 versions. Ekom works the same way on both versions, so storefront code that uses the Ekom API does not change.

On this page:

- [Who needs this page](#who-needs-this-page)
- [What changes](#what-changes)
- [Before you start](#before-you-start)
- [Upgrade the site](#upgrade-the-site)
- [Check that it worked](#check-that-it-worked)
- [What Ekom does not do for you](#what-ekom-does-not-do-for-you)

## Who needs this page

Your site needs this page if it references `Ekom.U17` and you are upgrading Umbraco to 18.

## What changes

| | Umbraco 17 | Umbraco 18 |
| --- | --- | --- |
| Ekom runtime package | `Ekom.U17` | `Ekom.U18` |
| Ekom web-assets package | `Ekom.Web.U17` | `Ekom.Web.U18` |
| Algolia | `Ekom.Algolia` | `Ekom.Algolia.U18` |
| Klaviyo | `Ekom.Klaviyo` | `Ekom.Klaviyo.U18` |
| Mailchimp | `Ekom.Mailchimp` | `Ekom.Mailchimp.U18` |
| Target framework | .NET 10 (`net10.0`) | .NET 10 (`net10.0`) |
| Umbraco versions accepted | 17.0.0 or later, below 18.0.0 | 18.0.0 or later, below 19.0.0 |

`Ekom.U18` is compiled from the same source code as `Ekom.U17`, and `Ekom.Web.U18` is built from the same backoffice client as `Ekom.Web.U17`. The Ekom API, the configuration, the database tables, the backoffice section alias `ekommanager` and the `/ekom/manager` endpoints are the same.

Ekom has no data conversion for this step. On the first start it adds any table, column or index that the new Ekom version needs, as on every start.

## Before you start

1. Read [Before and after an upgrade](overview.md). It explains what Ekom changes in the database and what to test.
2. Back up the database and the site files. The Umbraco upgrade changes the Umbraco database, and the only way back is to restore the backup.
3. Check that each payment provider package and other third-party Ekom package the site uses has a version for Umbraco 18.

## Upgrade the site

1. Upgrade the site to Umbraco 18, following Umbraco's upgrade guide. Do not start the site yet.
2. In the project that runs the site, replace the Ekom runtime package:

   ```bash
   dotnet remove package Ekom.U17
   dotnet add package Ekom.U18
   ```

3. In the site project that contains `wwwroot`, replace the web-assets package:

   ```bash
   dotnet remove package Ekom.Web.U17
   dotnet add package Ekom.Web.U18
   ```

   Run each command in the project that has the reference, or pass the project path. If the solution uses central package management, change the package names in `Directory.Packages.props` instead.

4. Replace each integration package the site uses with its `.U18` package, for example `Ekom.Algolia` with `Ekom.Algolia.U18`.
5. If the site still references `Ekom.Payments.U17` or `Ekom.Payments.AspNetCore`, remove them and follow [Upgrade payments](payments.md).
6. Build the solution and fix the compile errors in custom code. They come from Umbraco 18 API changes and from Ekom changes marked BREAKING in the [Ekom changelog](https://github.com/Vettvangur/Ekom/blob/Ekom/Ekom/CHANGELOG.md).
7. Start the site against a copy of the production database. Umbraco upgrades its database first, then Ekom checks its own tables.
8. If the site used Hangfire for stock holds, follow [Move from Hangfire reservations](reservations.md) before you take orders.
9. Run the checks in [Check that it worked](#check-that-it-worked). When they pass, repeat steps 7 to 9 on production.

## Check that it worked

1. The log has no errors from Ekom's database setup.
2. Open a product, a category and a store in the backoffice. Every Ekom property shows its editor.
3. The **Ekom** section opens for the user groups that had it before, with the same store permissions.
4. Place a test order through checkout and payment, and check that stock and discount counts go down.
5. Run the rest of the checks in [Check the site after an upgrade](overview.md#check-the-site-after-an-upgrade).

## What Ekom does not do for you

- Upgrade Umbraco, its database or its configuration. Follow Umbraco's own guide.
- Rebuild custom code against the Umbraco 18 packages.
- Check that third-party Ekom packages support Umbraco 18.
- Undo the upgrade. To go back to Umbraco 17, restore the backup.

## Related pages

- [Before and after an upgrade](overview.md): the database changes and the checks that apply to every upgrade.
- [Compatibility](../getting-started/compatibility.md): the packages and .NET version for each Umbraco version.
- [Integrations](../integrations/README.md): the Algolia, Klaviyo and Mailchimp packages for each Umbraco version.
- [Upgrade payments](payments.md): if the site still uses the old Ekom.Payments packages.

Next: [Upgrade from Umbraco 18 to 19](umbraco-18-to-19.md)

[Back to Upgrading](README.md)

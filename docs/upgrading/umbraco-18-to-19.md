[Ekom docs](../README.md) / [Upgrading](README.md) / Upgrade from Umbraco 18 to 19

# Upgrade from Umbraco 18 to 19

This page moves an Ekom site from Umbraco 18 to Umbraco 19. You swap the Umbraco 18 packages for their Umbraco 19 versions and move the site to .NET 11. The one Ekom change that affects storefront code is catalog search, because Umbraco 19 replaces the Examine indexes that Ekom searched before.

Umbraco 19 is a release candidate. The Ekom Umbraco 19 packages are built against Umbraco `19.0.0-rc1`. Test the upgrade on a copy of the site before you run it in production.

On this page:

- [Who needs this page](#who-needs-this-page)
- [What changes](#what-changes)
- [Catalog search](#catalog-search)
- [Algolia content indexing](#algolia-content-indexing)
- [Before you start](#before-you-start)
- [Upgrade the site](#upgrade-the-site)
- [Check that it worked](#check-that-it-worked)
- [What Ekom does not do for you](#what-ekom-does-not-do-for-you)

## Who needs this page

Your site needs this page if it references `Ekom.U18` and you are upgrading Umbraco to 19. If the site is on Umbraco 17, start with [Upgrade from Umbraco 17 to 18](umbraco-17-to-18.md).

## What changes

| | Umbraco 18 | Umbraco 19 |
| --- | --- | --- |
| Ekom runtime package | `Ekom.U18` | `Ekom.U19` |
| Ekom web-assets package | `Ekom.Web.U18` | `Ekom.Web.U19` |
| Algolia | `Ekom.Algolia.U18` | `Ekom.Algolia.U19` |
| Klaviyo | `Ekom.Klaviyo.U18` | `Ekom.Klaviyo.U19` |
| Mailchimp | `Ekom.Mailchimp.U18` | `Ekom.Mailchimp.U19` |
| Target framework | .NET 10 (`net10.0`) | .NET 11 (`net11.0`) |
| Umbraco versions accepted | 18.0.0 or later, below 19.0.0 | 19.0.0-rc1 or later, below 20.0.0 |

`Ekom.U19` is compiled from the same source code as `Ekom.U17` and `Ekom.U18`, with the Umbraco 19 differences switched on. `Ekom.Web.U19` is built from the same backoffice client, against the Umbraco 19 backoffice packages. The Ekom API, the configuration keys, the database tables, the backoffice section alias `ekommanager` and the `/ekom/manager` endpoints are the same.

Ekom has no data conversion for this step. On the first start it adds any table, column or index that the new Ekom version needs, as on every start.

## Catalog search

Umbraco 19 removes the Examine indexes of earlier versions, including `ExternalIndex`, and replaces them with a search abstraction that a search provider plugs into. On Umbraco 19, Ekom's catalog search queries Umbraco's published content index through that abstraction. This applies to `ICatalogSearchService`, `Catalog.ProductSearchAsync` and the `ekom/catalog/productsearch` endpoint.

What this means for search results:

- The registered search provider decides matching, relevance, stop words and text analysis. Umbraco's default provider is Examine.
- `SearchRequest.SearchFields` is not used. Per-field boosts, wildcard and fuzzy settings have no effect.
- `Ekom:ExamineSearchNormalizedFields` has no effect.
- `Ekom:ExamineSearchIndex` and `SearchRequest.ExamineIndex` name a search index alias. An empty value, or the old default `ExternalIndex`, means Umbraco's published content index.
- `SearchRequest.NodeTypeAlias` still limits results to those document types. If none of the aliases exists, the search returns no results.
- `SearchRequest.SearchNodeById` still limits results to content below that node. It takes a node id or key. If the node is not found, the search returns no results.
- Ekom removes accents from the search text before it sends it to the provider.
- Without `SearchRequest.PageSize`, a search returns at most 500 results.

When the search fails, Ekom logs an error that starts with `Failed to query search service` and returns no results. When no searcher is registered for the index, it logs a warning that starts with `No searcher is registered for the search index` and returns no results. The storefront gets an empty result in both cases, not an exception.

Custom code that queries Examine directly, or that reads the old index field names, must be moved to Umbraco's search API. Ekom does not do this for you.

## Algolia content indexing

Umbraco 19 replaces the index value factories of property editors with search property value handlers. `Ekom.Algolia.U19` reads content property values through those handlers:

- All text values of a property are joined into one string, separated by spaces.
- A property without text values gives its first keyword, number or date.
- A property whose editor has no handler is left out of the record.

After the upgrade, reindex and compare a few Algolia records with the same records from Umbraco 18. Pay most attention to rich text and block properties.

## Before you start

1. Read [Before and after an upgrade](overview.md). It explains what Ekom changes in the database and what to test.
2. Back up the database and the site files. The Umbraco upgrade changes the Umbraco database, and the only way back is to restore the backup.
3. Install the .NET 11 SDK on the build machines and the .NET 11 runtime on the servers.
4. Check that each payment provider package and other third-party Ekom package the site uses has a version for Umbraco 19.
5. List the places the storefront uses catalog search, and write down a few searches with their current results so you can compare them afterwards.

## Upgrade the site

1. Upgrade the site to Umbraco 19 and .NET 11, following Umbraco's upgrade guide. Do not start the site yet.
2. In the site project, add this property:

   ```xml
   <PropertyGroup>
     <CompressionEnabled>false</CompressionEnabled>
   </PropertyGroup>
   ```

   Umbraco 19 ships its backoffice files already compressed, and the .NET build must not compress them again. Ekom's own Umbraco 19 sample site sets this property.

3. In the project that runs the site, replace the Ekom runtime package:

   ```bash
   dotnet remove package Ekom.U18
   dotnet add package Ekom.U19
   ```

4. In the site project that contains `wwwroot`, replace the web-assets package:

   ```bash
   dotnet remove package Ekom.Web.U18
   dotnet add package Ekom.Web.U19
   ```

   Run each command in the project that has the reference, or pass the project path. If the solution uses central package management, change the package names in `Directory.Packages.props` instead.

5. Replace each integration package the site uses with its `.U19` package, for example `Ekom.Algolia.U18` with `Ekom.Algolia.U19`.
6. Build the solution and fix the compile errors in custom code. Move code that queries Examine directly to Umbraco's search API.
7. If `Ekom:ExamineSearchIndex` names an index other than `ExternalIndex`, change it to the alias of an index that exists on Umbraco 19, or remove it to use the published content index.
8. Start the site against a copy of the production database. Umbraco upgrades its database first, then Ekom checks its own tables.
9. If the site uses Algolia, run a full reindex.
10. Run the checks in [Check that it worked](#check-that-it-worked). When they pass, repeat steps 8 to 10 on production.

## Check that it worked

1. The log has no errors from Ekom's database setup, and no `Failed to query search service` or `No searcher is registered` messages.
2. Run the searches you wrote down before the upgrade. Results come back, and their order is acceptable for the store. The order can differ from Umbraco 18, because the search provider now ranks them.
3. Open a product, a category and a store in the backoffice. Every Ekom property shows its editor.
4. The **Ekom** section opens for the user groups that had it before, with the same store permissions.
5. Place a test order through checkout and payment, and check that stock and discount counts go down.
6. Run the rest of the checks in [Check the site after an upgrade](overview.md#check-the-site-after-an-upgrade).

## What Ekom does not do for you

- Upgrade Umbraco, its database or its configuration. Follow Umbraco's own guide.
- Move custom Examine queries to Umbraco's search API.
- Recreate field boosts, fuzzy matching or wildcard behaviour from `SearchFields`. Configure the search provider for that.
- Check that third-party Ekom packages support Umbraco 19.
- Undo the upgrade. To go back to Umbraco 18, restore the backup.

## Related pages

- [Before and after an upgrade](overview.md): the database changes and the checks that apply to every upgrade.
- [Compatibility](../getting-started/compatibility.md): the packages and .NET version for each Umbraco version.
- [Catalog and products](../guides/catalog-and-products.md): how the storefront reads and searches the catalog.
- [Configuration reference](../reference/configuration-reference.md): the `Ekom:ExamineSearchIndex` setting and the others.
- [Integrations](../integrations/README.md): the Algolia, Klaviyo and Mailchimp packages for each Umbraco version.

[Back to Upgrading](README.md)

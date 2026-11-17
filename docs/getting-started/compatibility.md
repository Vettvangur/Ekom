[Ekom docs](../README.md) / [Getting started](README.md) / Compatibility

# Compatibility

Ekom ships one pair of packages for each Umbraco major version: a runtime package and a web-assets package. Use this page to find the pair for your site before you install anything.

These docs cover Umbraco 17, 18 and 19. They do not cover Umbraco 13.

On this page:

- [Find your packages](#find-your-packages)
- [Umbraco versions each package accepts](#umbraco-versions-each-package-accepts)
- [What differs between Umbraco 17, 18 and 19](#what-differs-between-umbraco-17-18-and-19)
- [Tools and databases you need](#tools-and-databases-you-need)

## Find your packages

| Umbraco | Site target framework | Runtime package | Web-assets package |
| --- | --- | --- | --- |
| 17 | .NET 10 (`net10.0`) | `Ekom.U17` | `Ekom.Web.U17` |
| 18 | .NET 10 (`net10.0`) | `Ekom.U18` | `Ekom.Web.U18` |
| 19 (release candidate) | .NET 11 (`net11.0`) | `Ekom.U19` | `Ekom.Web.U19` |

The runtime package contains the Ekom API, the Umbraco integration and the database setup. The web-assets package contains the Ekom backoffice user interface: the **Ekom** section and Ekom's property editors.

Install both packages of one row, with the same version number. Ekom publishes all of these packages together, with one version number, from the same release. Do not mix packages from different rows.

## Umbraco versions each package accepts

Each runtime package declares the range of Umbraco versions it accepts. NuGet refuses to restore the package when the site's Umbraco version is outside that range.

| Runtime package | Accepted Umbraco versions |
| --- | --- |
| `Ekom.U17` | 17.0.0 or later, below 18.0.0 |
| `Ekom.U18` | 18.0.0 or later, below 19.0.0 |
| `Ekom.U19` | 19.0.0-rc1 or later, below 20.0.0 |

Umbraco 19 is a release candidate. The `Ekom.U19` range starts at the first release candidate and also includes the final 19.x releases.

## What differs between Umbraco 17, 18 and 19

The three runtime packages are built from the same source code, and the three web-assets packages are built from the same backoffice client. A feature described in these docs works on all three versions unless the page says otherwise.

The one difference that affects storefront code is catalog search:

- On Umbraco 17 and 18, Ekom searches the Examine index named in `Ekom:ExamineSearchIndex` (default `ExternalIndex`).
- On Umbraco 19, the Examine indexes of earlier versions no longer exist. Ekom searches through Umbraco's search provider instead. Field boosts, fuzzy matching and wildcard settings on a search request are not applied.

See [Upgrade from Umbraco 18 to 19](../upgrading/umbraco-18-to-19.md#catalog-search) for the full list of search differences.

## Tools and databases you need

| Tool | Needed for |
| --- | --- |
| .NET 10 SDK | A site on Umbraco 17 or 18. |
| .NET 11 SDK | A site on Umbraco 19. |

You do not need Node.js to use Ekom. The published web-assets packages already contain the built backoffice files. Node.js is needed only to build Ekom itself from source; see [Set up a development environment](../contributing/setup.md).

Ekom supports SQL Server and SQLite databases. On any other database provider, startup fails with `Unsupported database provider for EkomOrders indexes.`

## Related pages

- [Installation](installation.md): add the packages to your site.
- [Packages](../architecture/packages.md): every Ekom package and what it contains.
- [Integrations](../integrations/README.md): the matching Algolia, Klaviyo and Mailchimp packages for each Umbraco version.
- [Upgrading](../upgrading/README.md): move a site from one Umbraco version to the next.

Next: [Installation](installation.md)

[Back to Getting started](README.md)

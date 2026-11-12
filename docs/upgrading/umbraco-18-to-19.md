# Upgrade from Umbraco 18 to 19

Umbraco 19 is currently a release candidate. The Ekom Umbraco 19 packages are built against `19.0.0-rc1` and should be treated as pre-release until Umbraco 19 is final.

Replace the Umbraco 18 Ekom packages with their Umbraco 19 equivalents:

| Umbraco 18 | Umbraco 19 |
| --- | --- |
| `Ekom.U18` | `Ekom.U19` |
| `Ekom.Web.U18` | `Ekom.Web.U19` |
| `Ekom.Algolia.U18`, `Ekom.Klaviyo.U18`, `Ekom.Mailchimp.U18` | `Ekom.Algolia.U19`, `Ekom.Klaviyo.U19`, `Ekom.Mailchimp.U19` |

The `Ekom.U19` project targets .NET 11 and accepts Umbraco package versions from 19.0.0-rc1 up to, but not including, 20.0.0. It compiles the shared Umbraco 17/18/19 source with the `UMBRACO_18` and `UMBRACO_19` compilation constants rather than maintaining a separate copy of that implementation.

`Ekom.U19` still depends on `Ekom.Payments.U18`. No `Ekom.Payments.U19` package exists yet; verify payment providers against Umbraco 19 before relying on them.

## Replace the Ekom package references

```bash
dotnet remove package Ekom.U18
dotnet remove package Ekom.Web.U18
dotnet add package Ekom.U19
dotnet add package Ekom.Web.U19
```

Run each command against the project that owns the reference, or pass the project path. `Ekom.Web.U19` belongs in the site project that contains `wwwroot`. If the solution uses central package management, update its existing package files instead.

The host application must target .NET 11. Umbraco 19 ships precompressed backoffice assets, so its project template sets `<CompressionEnabled>false</CompressionEnabled>`; add that property to an upgraded site project if the build fails in the static web assets compression step.

## Catalog search

Umbraco 19 removes the legacy Examine indexes, including `ExternalIndex`, and replaces them with a provider-based search abstraction. On Umbraco 19, Ekom's catalog search (`ICatalogSearchService`, used by the `ekom/catalog/productsearch` endpoint and `Catalog.ProductSearchAsync`) queries Umbraco's published content search index through that abstraction. As a result:

- Matching, relevance, stop words, and text analysis are decided by the registered search provider. The default is Umbraco's Examine provider.
- `SearchRequest.SearchFields`, including per-field boosts, wildcard, and fuzzy settings, is not applied.
- `Ekom:ExamineSearchIndex` and `SearchRequest.ExamineIndex` are treated as a search index alias. The legacy default `ExternalIndex` maps to Umbraco's published content index.
- `SearchRequest.NodeTypeAlias` and `SearchRequest.SearchNodeById` are still applied, as content type and ancestor filters.
- `Ekom:ExamineSearchNormalizedFields` has no effect.

Custom code that queries Examine directly, or that depends on the legacy index field names, must be ported to Umbraco's search API as part of the host upgrade.

## Algolia content indexing

Umbraco 19 replaces property editor index value factories with search property value handlers. `Ekom.Algolia.U19` reads content property values through those handlers. Text values of a property are joined into one string; review the indexed records for rich text and block properties after upgrading.

## Shared behavior

The U19 integration reuses the U17/U18 implementation, and the U19 manager assets are built from the same client source against the Umbraco 19 backoffice packages. The Ekom section alias remains `ekommanager`, with the same `/ekom/manager` endpoint surface.

## Data and configuration

The repository does not define a separate Ekom-only data conversion or manual schema script for this package transition. Allow Umbraco's own upgrade and Ekom's normal startup and schema initialization to run after completing the host upgrade.

Ekom's document types still use Umbraco's multi node tree picker, which Umbraco 19 marks obsolete and schedules for removal in a later major version. It continues to work on Umbraco 19.

## Verify

Verify the application-specific checkout and payment callbacks as well as:

- startup, schema initialization, and cache readiness;
- catalog search results and ordering;
- manager section permission, store authorization, orders, and analytics;
- catalog and backoffice property editors used by the application;
- checkout, stock, discounts, and payment return flows;
- every custom or third-party Ekom integration.

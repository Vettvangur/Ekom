# Packages

Install the runtime and web-assets package that exactly matches the Umbraco major version of the site.

| Umbraco | Runtime package | Web-assets package | Target framework |
| --- | --- | --- | --- |
| 13 | `Ekom.U10` | `Ekom.Web` | .NET 8 |
| 17 | `Ekom.U17` | `Ekom.Web.U17` | .NET 10 |
| 18 | `Ekom.U18` | `Ekom.Web.U18` | .NET 10 |

The runtime package owns the Umbraco integration and automatic composition. The web-assets package supplies Ekom backoffice assets and belongs in the web host project. The `Ekom.U10` package name is legacy terminology for the Umbraco 13 implementation.

## Repository projects

The runtime package chain is composed from these repository projects:

| Project | NuGet package | Role |
| --- | --- | --- |
| `Ekom/Ekom.Core/Ekom.Common` | `Ekom.Common` | Shared primitives. |
| `Ekom/Ekom.Core/Ekom` | `Ekom.Core` | Commerce domain, APIs, services, persistence, and caches. |
| `Ekom/Ekom.Core/Ekom.AspNetCore` | `Ekom.AspNetCore` | ASP.NET Core hosting integration. |
| `Ekom/Ekom.Umbraco/Ekom.U10` | `Ekom.U10` | Umbraco 13 integration. |
| `Ekom/Ekom.Umbraco/Ekom.U17` | `Ekom.U17` | Umbraco 17 integration. |
| `Ekom/Ekom.Umbraco/Ekom.U18` | `Ekom.U18` | Umbraco 18 integration built from shared U17/U18 source. |

`Ekom.Web`, `Ekom.Web.U17`, and `Ekom.Web.U18` package the corresponding backoffice assets. Consumers should install the versioned runtime and web-assets pair rather than assembling the lower-level core packages themselves.

The U17 and U18 web projects build their client assets with `npm ci` and `npm run build` when built from source. Their declared tool requirements are:

| Web-assets project | Node.js | npm |
| --- | --- | --- |
| `Ekom.Web.U17` | `>=22.17.1` | `>=10.9.2` |
| `Ekom.Web.U18` | `>=24.13.0` | `>=11` |

Published packages contain their assets, so these Node/npm requirements apply to contributors building those projects, not ordinary NuGet consumers.

## Integrations

The repository also contains optional Algolia, Klaviyo, and Mailchimp integrations. The main plugin projects multi-target .NET 8 and .NET 10 for Umbraco 13 and 17; separate U18 projects produce the Umbraco 18 packages. They have their own registration, configuration, release workflows, and documentation. Installing Ekom does not configure credentials or enable an external integration.

## Source references versus package references

Core projects reference each other directly. Plugin projects switch between local source and published-style package references with the `UseProjectReferences` MSBuild property. It defaults to `true` in each plugin's `Directory.Build.props`; setting it to `false` makes the plugin consume an Ekom NuGet package at `EkomPackageVersion`. See [Contributor setup](../contributing/setup.md) before changing this property.

For local source development, see [Contributing setup](../contributing/setup.md).

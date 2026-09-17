# Compatibility

Ekom supports the following Umbraco lines in this branch. The runtime and web-assets packages must match the site's Umbraco major version.

| Umbraco | Application target | Runtime package | Web-assets package |
| --- | --- | --- | --- |
| 13 | .NET 8 (`net8.0`) | `Ekom.U10` | `Ekom.Web` |
| 17 | .NET 10 (`net10.0`) | `Ekom.U17` | `Ekom.Web.U17` |
| 18 | .NET 10 (`net10.0`) | `Ekom.U18` | `Ekom.Web.U18` |

The U17 runtime references Umbraco packages in the `[17.0.0,18.0.0)` range. The U18 runtime references `[18.0.0,19.0.0)`. The U10 project currently builds against Umbraco 13.13 packages.

## Why Umbraco 13 uses `Ekom.U10`

`Ekom.U10` is a historical package and project name retained from the first ASP.NET Core Umbraco implementation. The current project targets .NET 8 and references Umbraco 13, so the suffix does not describe its current Umbraco compatibility. For an Umbraco 13 site, install `Ekom.U10` with `Ekom.Web`.

Do not replace `Ekom.U10` with `Ekom.U17` based only on the package name. `Ekom.U17` requires Umbraco 17 and .NET 10.

## U17 and U18 implementation sharing

`Ekom.U18` compiles the U17 integration source against Umbraco 18 dependencies with an `UMBRACO_18` compile constant. This reduces duplication, but the output packages are not interchangeable. Install the U17 pair for Umbraco 17 and the U18 pair for Umbraco 18.

## Tooling requirements

Package consumers need an SDK capable of building the site's target framework: .NET 8 for Umbraco 13 or .NET 10 for Umbraco 17 and 18. The repository has no `global.json`, so it does not pin an SDK patch version.

Node.js is not required to consume the published NuGet packages. It is required only when building the U17 or U18 web-assets projects from source; see [Contributor setup](../contributing/setup.md) for their declared Node.js and npm versions.

Continue with [Installation](installation.md), or see [Packages](../architecture/packages.md) for package and project boundaries.

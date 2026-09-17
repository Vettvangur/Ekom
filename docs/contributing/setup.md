# Contributor Setup

## Prerequisites

Install the SDKs required for the targets you intend to build:

| Work | Requirement |
| --- | --- |
| Umbraco 13 and `Ekom.U10` | .NET 8 SDK |
| Umbraco 17/18 and their Ekom packages | .NET 10 SDK |
| U17 web assets | Node.js `>=22.17.1`, npm `>=10.9.2` |
| U18 web assets | Node.js `>=24.13.0`, npm `>=11` |

`Ekom.Web.U17` and `Ekom.Web.U18` invoke `npm ci` and `npm run build` as part of their source builds. Node.js is not needed to consume the published web-assets packages.

There is no repository `global.json`; install both .NET SDK major versions when working across all supported targets. Use Git and a Node/npm installation that satisfies the relevant `Client/package.json` engines. No separate repository-wide lint command is configured.

## Restore and build

From the repository root:

```bash
dotnet restore "Ekom Build.sln"
dotnet build "Ekom Build.sln"
```

For the wider site-oriented solution, including the sample projects:

```bash
dotnet build "Ekom Site.sln"
```

The solutions have intentionally different coverage:

| Solution | Included coverage | Important omissions |
| --- | --- | --- |
| `Ekom Build.sln` | Shared core, U10/U17 runtime and web assets, Mailchimp main/U18 projects, and Mailchimp tests. | U18 Ekom runtime/web assets, `Ekom.Tests`, Algolia, Klaviyo, and all samples. |
| `Ekom Site.sln` | Shared core; all three runtime, web-assets, and sample lines; `Ekom.Tests`; Algolia and Klaviyo main/U18 projects. | Mailchimp projects/tests and `BuildProject`. |

Neither solution alone is complete repository coverage. Build the changed project directly when it is omitted, and run its applicable tests.

## Local project references

`UseProjectReferences` controls how the Algolia, Klaviyo, and Mailchimp plugin projects resolve Ekom. It defaults to `true` in `Ekom/Directory.Build.props` and each plugin's `Directory.Build.props`, which makes plugin builds use local Ekom project references. The samples always declare explicit local project references.

Use `-p:UseProjectReferences=false` when validating the package-consumer path. In that mode a plugin selects `Ekom.U10`, `Ekom.U17`, `Ekom.U18`, or `Ekom.Core` according to its target and uses `EkomPackageVersion`. Plugin publishing workflows first pack Ekom dependencies into a local feed, then restore, build, and pack the plugin with package references forced.

## Run a sample

Choose the sample matching the intended Umbraco version:

- `Samples/U10/Ekom.Site` targets Umbraco 13 and .NET 8.
- `Samples/U17/Ekom.Site.U17` targets Umbraco 17 and .NET 10.
- `Samples/U18/Ekom.Site.U18` targets Umbraco 18 and .NET 10.

The samples use local project references and development configuration. They are reference applications, not production-ready templates. Configure credentials, payment providers, connection strings, domains, and security before using their patterns outside local development. See [Samples](../samples/overview.md).

Run one directly with, for example:

```bash
dotnet run --project "Samples/U17/Ekom.Site.U17/Ekom.Site.U17.csproj"
```

Building or running U17/U18 also builds the matching web-assets project, so the corresponding Node.js/npm requirement applies.

[Ekom docs](../README.md) / [Architecture](README.md) / Packages

# Packages

Ekom is published as a set of NuGet packages. A site installs two of them, and those bring in the rest. This page lists every package, what it contains, which project in the repository builds it, and how the packages depend on each other.

On this page:

- [Packages a site installs](#packages-a-site-installs)
- [How the packages depend on each other](#how-the-packages-depend-on-each-other)
- [How the packages are released](#how-the-packages-are-released)
- [Optional packages](#optional-packages)
- [Projects in the repository](#projects-in-the-repository)

## Packages a site installs

| Umbraco | Runtime package | Web-assets package | Target framework |
| --- | --- | --- | --- |
| 17 | `Ekom.U17` | `Ekom.Web.U17` | .NET 10 |
| 18 | `Ekom.U18` | `Ekom.Web.U18` | .NET 10 |
| 19 (release candidate) | `Ekom.U19` | `Ekom.Web.U19` | .NET 11 |

- The **runtime package** contains the Umbraco integration: the composer that sets Ekom up, database setup, Ekom's document types, memory cache loading and catalog routing. It depends on the core packages, so a site does not install those itself.
- The **web-assets package** contains the built backoffice files for the **Ekom** section and Ekom's property editors. It has no code dependencies. When the site's web project builds, the package copies its files into `wwwroot/App_Plugins/Ekom`. Install it in the web project.

Installation steps are in [Installation](../getting-started/installation.md).

## How the packages depend on each other

```text
Ekom.U17 / Ekom.U18 / Ekom.U19
  -> Ekom.AspNetCore
       -> Ekom.Core
            -> Ekom.Common
```

| Package | Target frameworks | Contents |
| --- | --- | --- |
| `Ekom.Common` | .NET 8, .NET 10 | Shared primitives used by the other core packages. |
| `Ekom.Core` | .NET 8, .NET 10 | The `Ekom.API` classes, models, services, caches, database access, HTTP controllers, tracking and background services. |
| `Ekom.AspNetCore` | .NET 8, .NET 10 | Dependency injection registrations for the core, and the mail service. |
| `Ekom.U17` | .NET 10 | Umbraco 17 integration. Accepts Umbraco 17.0.0 or later, below 18.0.0. |
| `Ekom.U18` | .NET 10 | Umbraco 18 integration. Accepts Umbraco 18.0.0 or later, below 19.0.0. |
| `Ekom.U19` | .NET 11 | Umbraco 19 integration. Accepts Umbraco 19.0.0-rc1 or later, below 20.0.0. |

A class library that only calls the Ekom API can reference the runtime package for its Umbraco version. Do not assemble Ekom from the core packages on an Umbraco site: without the runtime package nothing registers Ekom with Umbraco.

## How the packages are released

`Ekom.Common`, `Ekom.Core`, `Ekom.AspNetCore`, the three runtime packages and the three web-assets packages are released together, from one Git tag, with the same version number. Use the same version for the runtime and web-assets package on a site.

Each package includes the Ekom `README.md` and `CHANGELOG.md`. The release history is in [`Ekom/CHANGELOG.md`](../../Ekom/CHANGELOG.md).

## Optional packages

These packages are released on their own, with their own version numbers.

| Package | What it does | Documentation |
| --- | --- | --- |
| `Ekom.Printer` | Prints picking slips for orders on a warehouse printer, through a Windows agent. Targets .NET 8 and .NET 10, and depends on `Ekom.Core`. | [Ekom.Printer README](../../Ekom/Printing/Ekom.Printer/README.md) |
| `Ekom.Algolia`, `Ekom.Algolia.U18`, `Ekom.Algolia.U19` | Indexes products, categories and content in Algolia, searches them, and sends Algolia Insights events. | [Integrations](../integrations/README.md) |
| `Ekom.Klaviyo`, `Ekom.Klaviyo.U18`, `Ekom.Klaviyo.U19` | Sends order events and catalog data to Klaviyo, and manages subscriptions. | [Integrations](../integrations/README.md) |
| `Ekom.Mailchimp`, `Ekom.Mailchimp.U18`, `Ekom.Mailchimp.U19` | Mailchimp audience tags and subscriptions, purchase tracking, and transactional email. | [Integrations](../integrations/README.md) |

Installing Ekom does not install, configure or turn on any of these.

## Projects in the repository

| Project | Package it builds |
| --- | --- |
| `Ekom/Ekom.Core/Ekom.Common` | `Ekom.Common` |
| `Ekom/Ekom.Core/Ekom` | `Ekom.Core` |
| `Ekom/Ekom.Core/Ekom.AspNetCore` | `Ekom.AspNetCore` |
| `Ekom/Ekom.Umbraco/Ekom.U17` | `Ekom.U17`. Its source is shared with U18 and U19. |
| `Ekom/Ekom.Umbraco/Ekom.U18` | `Ekom.U18`, compiled from the `Ekom.U17` source. |
| `Ekom/Ekom.Umbraco/Ekom.U19` | `Ekom.U19`, compiled from the `Ekom.U17` source. |
| `Ekom/Ekom.Web/Ekom.Web.U17` | `Ekom.Web.U17`. Its `Client` folder holds the backoffice source shared with U18 and U19. |
| `Ekom/Ekom.Web/Ekom.Web.U18` | `Ekom.Web.U18`, built from the `Ekom.Web.U17` client source. |
| `Ekom/Ekom.Web/Ekom.Web.U19` | `Ekom.Web.U19`, built from the `Ekom.Web.U17` client source. |
| `Ekom/Printing/Ekom.Printer` | `Ekom.Printer` |
| `Plugins/Ekom.Algolia`, `Plugins/Ekom.Klaviyo`, `Plugins/Ekom.Mailchimp` and their `U18` and `U19` folders | The integration packages. |

The repository also contains the older `Ekom/Ekom.Umbraco/Ekom.U10` and `Ekom/Ekom.Web/Ekom.Web` projects, which these docs do not cover. They are part of both solutions and need the .NET 8 SDK to build.

### Local project references in plugins

The plugin projects and `Ekom.Printer` can reference Ekom in two ways, chosen by the MSBuild property `UseProjectReferences`:

- `true` (the default in `Ekom/Directory.Build.props` and in each plugin's `Directory.Build.props`): they reference the Ekom projects in this repository. Use this when you change Ekom and a plugin together.
- `false`: they reference the published Ekom NuGet packages, at the version in the `EkomPackageVersion` property. The release workflows build plugins this way.

See [Set up a development environment](../contributing/setup.md#use-local-ekom-projects-or-published-packages-in-plugins).

## Related pages

- [Compatibility](../getting-started/compatibility.md): which pair to install for each Umbraco version.
- [Architecture overview](overview.md): what each layer does at runtime.
- [Integrations](../integrations/README.md): install and configure the Algolia, Klaviyo and Mailchimp packages.
- [Build, test and release](../contributing/build-test-release.md): how the packages are built and published.

[Back to Architecture](README.md)

# Installation

Install two packages into the Umbraco web application: the versioned Ekom runtime package and its matching web-assets package. The web application is the executable project that hosts Umbraco and owns `wwwroot`.

| Umbraco version | Install |
| --- | --- |
| 13 | `Ekom.U10` and `Ekom.Web` |
| 17 | `Ekom.U17` and `Ekom.Web.U17` |
| 18 | `Ekom.U18` and `Ekom.Web.U18` |

## Install the matching pair

### Umbraco 13

```bash
dotnet add package Ekom.U10
dotnet add package Ekom.Web
```

### Umbraco 17

```bash
dotnet add package Ekom.U17
dotnet add package Ekom.Web.U17
```

### Umbraco 18

```bash
dotnet add package Ekom.U18
dotnet add package Ekom.Web.U18
```

Run the commands from the web project directory, or pass `--project path/to/site.csproj`. Package Manager Console users can use `Install-Package <package-name>` instead.

The web-assets packages contain the Ekom backoffice extension. Reference them from the host project, not only from a class library. A separate class library may also reference the runtime package when it compiles against Ekom APIs, but that does not replace the host references.

## Startup registration

No explicit `AddEkom(...)` call is required for a standard package installation. Each runtime package contains an Umbraco `IComposer`; Umbraco discovers it during composition. On Umbraco 17 and 18, keep the normal `AddComposers()` call in the host's Umbraco builder pipeline. The composer calls Ekom's internal registration extension and registers services, components, content finders, URL providers, notification handlers, and startup filters.

Do not call the public `AddEkom(...)` extension a second time in application startup. That extension is used by the composer and duplicate registration is not part of the supported installation path.

## First run

After Umbraco reaches its running state, Ekom:

- creates or updates its database tables through the Ekom migration plan;
- creates the Ekom data types, document types, and root content when they do not exist;
- initializes catalog, store, provider, discount, and stock caches;
- wires content and language notifications that keep those caches synchronized.

Ekom skips this initialization while Umbraco itself is installing or upgrading. Restart the application after completing Umbraco's installer if the Ekom root and schema are not yet present. See [Startup and data](../architecture/startup-and-data.md) for details.

## Verify the installation

1. Restore and run the site.
2. Complete Umbraco's normal installation when this is a new site, then let the application reach the running state.
3. Sign in to the backoffice and confirm that Ekom root content and manager features are available.
4. Create and publish a store before adding catalog content or implementing checkout.

If the runtime starts but Ekom's backoffice UI is missing, verify that the matching `Ekom.Web*` package is referenced directly by the host and that its `App_Plugins/Ekom` content was copied to the build or publish output.

Continue with [Configuration](configuration.md) and [Quick start](quick-start.md).

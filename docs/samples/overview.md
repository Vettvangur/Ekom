# Samples

The repository contains four sample Umbraco sites:

| Sample | Umbraco package | Target | Data and wiring |
| --- | --- | --- | --- |
| `Samples/U10/Ekom.Site` | 13.13.0 | .NET 8 | Uses a configured SQL Server connection and checked-in `App_Plugins/Ekom` assets. |
| `Samples/U17/Ekom.Site.U17` | 17.8.1 | .NET 10 | References U17 runtime/web projects and includes a local SQLite database. |
| `Samples/U18/Ekom.Site.U18` | 18.3.1 | .NET 10 | References U18 runtime/web projects, includes SQLite, and links selected U17 source/views. |
| `Samples/U19/Ekom.Site.U19` | 19.0.0-rc1 | .NET 11 | References U19 runtime/web projects, includes SQLite, and links selected U17 source/views. |

The directory names preserve Ekom's package naming: `Samples/U10` is the Umbraco 13 sample because the compatible runtime package is still named `Ekom.U10`.

## What samples demonstrate

Samples show how local project references, an Umbraco host, Ekom configuration, views, checkout pages, and optional integrations can be assembled while developing Ekom. U17, U18, and U19 call the standard `AddComposers()` pipeline, which discovers Ekom's composer automatically; their explicit `AddAlgolia()`, `AddKlaviyo()`, and `AddMailchimp()` calls are plugin registration, not core Ekom registration.

All four samples are listed in `Ekom Site.sln`; none is in `Ekom Build.sln`. To run a sample from the repository root:

```bash
dotnet run --project "Samples/U17/Ekom.Site.U17/Ekom.Site.U17.csproj"
```

Use .NET 8 for U10, .NET 10 for U17/U18, or .NET 11 for U19. Building U17, U18, or U19 also builds the matching web-assets project and therefore requires the Node.js/npm version listed in [Contributor setup](../contributing/setup.md).

## Limitations

Samples are not turnkey commerce applications:

- U17, U18, and U19 include checked-in SQLite databases and development runtime settings. Treat their data as disposable local data.
- U10 is configured for an environment-specific SQL Server and does not include a local database. Replace that connection before running it outside its original environment.
- Sample configuration contains test, placeholder, or environment-specific payment and integration values. Do not treat any checked-in value as a usable or safe production credential.
- U10 keeps Ekom backoffice assets in the sample tree instead of referencing the `Ekom.Web` project; consumers should still install `Ekom.Web` as described in [Installation](../getting-started/installation.md).
- U18 and U19 link their custom product filter, utility code, and most views from U17. They are not standalone templates and changes in the U17 sample can affect them.
- Optional integrations may be registered while disabled or incompletely configured. Enable them only after supplying valid credentials through secure configuration.

The samples do not provide payment-provider onboarding, production deployment settings, secret management, monitoring, accessibility assurance, or a security review for a real storefront.

Treat a sample as a reference: install the matching package pair in the actual site, configure stores and providers for that environment, and implement the storefront and checkout policies required by the project.

For local prerequisites and solution coverage limitations, see [Contributor setup](../contributing/setup.md). For consumer installation, see [Installation](../getting-started/installation.md).

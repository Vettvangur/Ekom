# Upgrade from Umbraco 17 to 18

Replace the Umbraco 17 Ekom packages with their Umbraco 18 equivalents:

| Umbraco 17 | Umbraco 18 |
| --- | --- |
| `Ekom.U17` | `Ekom.U18` |
| `Ekom.Web.U17` | `Ekom.Web.U18` |
| `Ekom.Payments.U17` dependency | `Ekom.Payments.U18` dependency |

The `Ekom.U18` project targets .NET 10 and accepts Umbraco package versions from 18.0.0 up to, but not including, 19.0.0. It compiles the shared Umbraco 17/18 source with the `UMBRACO_18` compilation constant rather than maintaining a separate copy of that implementation.

## Replace the Ekom package references

For projects that use direct .NET CLI package references, the equivalent operations are:

```bash
dotnet remove package Ekom.U17
dotnet remove package Ekom.Web.U17
dotnet add package Ekom.U18
dotnet add package Ekom.Web.U18
```

Run each command against the project that owns the reference, or pass the project path. `Ekom.Web.U18` belongs in the site project that contains `wwwroot`. If the solution uses central package management, update its existing package files instead of treating these commands as the source of truth.

Review separately installed payment-provider and plugin packages for explicit Umbraco 18 support. The fact that `Ekom.U18` depends on `Ekom.Payments.U18` does not establish compatibility for every application-specific provider or plugin.

## Shared behavior

The U18 integration reuses the U17 implementation, and the checked-in U18 manager assets match the U17 manager assets apart from package identity metadata. The Ekom section alias remains `ekommanager`, with **Orders** and **Analytics** section views and the same `/ekom/manager` endpoint surface.

This shared implementation does not remove the need to follow Umbraco's 17-to-18 upgrade requirements for the host application or to rebuild custom code against its new dependencies.

## Data and configuration

The repository does not define a separate Ekom-only data conversion, configuration rewrite, or manual schema script for this package transition. Allow Ekom's normal startup and schema initialization to run after completing the host upgrade.

If native stock reservations are new to the version being deployed, review [Reservation compatibility](reservations.md). In particular, Ekom does not migrate or execute outstanding legacy Hangfire reservation jobs.

## Verify

Verify the application-specific checkout and payment callbacks as well as:

- startup, schema initialization, and cache readiness;
- manager section permission, store authorization, orders, and analytics;
- catalog and backoffice property editors used by the application;
- checkout, stock, discounts, and payment return flows;
- every custom or third-party Ekom integration.

No additional Ekom migration steps are asserted by this repository.

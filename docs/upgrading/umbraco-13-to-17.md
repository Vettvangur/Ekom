# Upgrade from Umbraco 13 to 17

The verified Ekom package transition is:

| Umbraco 13 | Umbraco 17 |
| --- | --- |
| `Ekom.U10` | `Ekom.U17` |
| `Ekom.Web` | `Ekom.Web.U17` |
| `Ekom.Payments.U10` dependency | `Ekom.Payments.U17` dependency |

Despite its name, `Ekom.U10` is the repository's current Umbraco 13 integration. Its project references Umbraco 13 packages and targets .NET 8. There is no `Ekom.U13` package in this repository.

The Umbraco 17 integration is `Ekom.U17`. Its project accepts Umbraco package versions from 17.0.0 up to, but not including, 18.0.0, and targets .NET 10.

## Replace the Ekom package references

In the application project, replace the direct `Ekom.U10` package reference with `Ekom.U17`. In the site project that contains `wwwroot`, replace `Ekom.Web` with `Ekom.Web.U17`.

For projects that use the .NET CLI for direct package references, the equivalent package operations are:

```bash
dotnet remove package Ekom.U10
dotnet remove package Ekom.Web
dotnet add package Ekom.U17
dotnet add package Ekom.Web.U17
```

Run each command against the project that owns that package reference, or pass the project path. If package versions are centrally managed, make the substitutions in the repository's existing package-management files instead.

Review separately installed Ekom payment-provider packages for Umbraco 17 compatibility. The core `Ekom.U17` project itself depends on `Ekom.Payments.U17`; that does not prove compatibility for every provider package used by an application.

## Manager change

The manager keeps section alias `ekommanager`, the **Orders** and **Analytics** views, and the shared `/ekom/manager` server endpoints. Its client implementation changes from the Umbraco 13 AngularJS dashboard supplied by `Ekom.Web` to Umbraco 17 extension section views supplied by `Ekom.Web.U17`.

Umbraco 17 applies its section-user-permission condition to the Ekom section. Confirm that the intended backoffice groups have permission to the section, then verify the Ekom group and store mapping described in [Manager access and permissions](../manager/access-and-permissions.md).

## Data and configuration

The repository does not define a separate general-purpose Ekom data migration or configuration rewrite for this transition. Do not rename the existing `Ekom` configuration section or infer an `Ekom.U13` package name.

Complete the required Umbraco 13-to-17 host upgrade, allow Ekom's normal startup and schema initialization to run, and validate the application's Ekom extensions. If the application is moving from Hangfire-based stock rollback behavior to native reservations at the same time, read [Reservation compatibility](reservations.md) before deployment.

## Verify

Verify the application-specific checkout and payment callbacks as well as:

- Ekom startup and catalog/cache initialization;
- manager section visibility, order access, and analytics for each authorized store;
- order editing and activity logs;
- stock and discount deductions;
- custom payment, shipping, import, search, and integration code.

No additional Ekom migration steps are asserted by this repository.

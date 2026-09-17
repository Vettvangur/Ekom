# Upgrading Ekom

Choose Ekom packages by the target Umbraco major version:

| Umbraco | Ekom integration | Web assets | Ekom payments dependency used by the integration |
| --- | --- | --- | --- |
| 13 | `Ekom.U10` | `Ekom.Web` | `Ekom.Payments.U10` |
| 17 | `Ekom.U17` | `Ekom.Web.U17` | `Ekom.Payments.U17` |
| 18 | `Ekom.U18` | `Ekom.Web.U18` | `Ekom.Payments.U18` |

`Ekom.U10` is the legacy name retained by the current Umbraco 13 package, assembly, project, and payments dependency. Do not replace it with an assumed `Ekom.U13` package; this repository does not define one.

The web-assets package belongs in the site project that contains `wwwroot`. Keep its Umbraco suffix aligned with the Ekom integration package.

## Before changing packages

These guides document only transitions verified in this repository. An Umbraco major-version upgrade can require application, database, configuration, and custom-extension work outside Ekom. Follow the official Umbraco upgrade guidance for the host application and review every third-party Ekom integration separately.

Record the Ekom packages and payment providers currently installed, and review application code that uses Umbraco APIs or Ekom backoffice assets. The repository does not provide an automated application migration command or a general Ekom data-conversion script for either major-version transition.

## Guides

- [Umbraco 13 to 17](umbraco-13-to-17.md)
- [Umbraco 17 to 18](umbraco-17-to-18.md)
- [Reservation compatibility](reservations.md)

## Verification after an upgrade

At minimum, exercise the paths used by the application:

- application startup and Ekom schema initialization;
- cache initialization and catalog reads;
- the Ekom manager and its configured store permissions;
- basket creation, provider selection, checkout, payment return, and order completion;
- stock and discount behavior, including reservations if enabled;
- installed payment, shipping, search, marketing, and ERP integrations.

This is a verification list, not an additional migration procedure. The required tests depend on the application's own extensions.

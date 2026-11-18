# Upgrading

[Ekom docs](../README.md) / Upgrading

This section is for developers who move an existing Ekom site to a newer Umbraco version, or to an Ekom version that changes how payments, shipping or stock reservations work. Each page is a procedure you can follow on a production site: what changes, what to prepare, the steps, and how to check the result.

Upgrade one Umbraco major version at a time. A site on Umbraco 17 goes to 18 first, then to 19. Each Umbraco version page ends with a link to the next step.

## Find the page for your site

| You are on | You want | Read |
| --- | --- | --- |
| Umbraco 17 with `Ekom.U17` | Umbraco 18 | [Upgrade from Umbraco 17 to 18](umbraco-17-to-18.md) |
| Umbraco 18 with `Ekom.U18` | Umbraco 19 (release candidate) | [Upgrade from Umbraco 18 to 19](umbraco-18-to-19.md) |
| Any version, with `Ekom.Payments` packages from 1.0 or earlier | The Ekom version that records payments on the order | [Upgrade payments](payments.md) |
| Any version, with `Ekom.Shipping` packages | The Ekom version that includes the shipping framework | [Upgrade shipping](shipping.md) |
| Any version, with stock holds that were Hangfire jobs | Ekom's database stock reservations | [Move from Hangfire reservations](reservations.md) |
| Not sure which packages you need | A list of packages and the checks to run after any upgrade | [Before and after an upgrade](overview.md) |

The payments, shipping and reservations pages are not tied to an Umbraco version. Read them in addition to the Umbraco version page when your site is affected.

## Pages in this section

| Page | Read it when you want to |
| --- | --- |
| [Before and after an upgrade](overview.md) | Choose the right packages, deploy the backoffice files safely, and test the site after any upgrade. |
| [Upgrade from Umbraco 17 to 18](umbraco-17-to-18.md) | Swap the Umbraco 17 packages for the Umbraco 18 packages. |
| [Upgrade from Umbraco 18 to 19](umbraco-18-to-19.md) | Move to Umbraco 19 and adjust to its new catalog search. |
| [Upgrade payments](payments.md) | Replace the old Ekom.Payments packages and convert code that followed payments. |
| [Upgrade shipping](shipping.md) | Remove the old Ekom.Shipping packages and read shipments from the order. |
| [Move from Hangfire reservations](reservations.md) | Switch to stock reservations stored in the Ekom database. |

## Other sections

- [Getting started](../getting-started/README.md): install and configure Ekom on a new site.
- [Guides](../guides/README.md): how each part of Ekom works today.
- [Architecture](../architecture/README.md): the packages, what happens at startup, and where Ekom keeps its data.
- [Integrations](../integrations/README.md): the Algolia, Klaviyo and Mailchimp packages for each Umbraco version.

[Back to the documentation home](../README.md)

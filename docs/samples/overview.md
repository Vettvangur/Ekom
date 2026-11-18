# Samples

[Ekom docs](../README.md) / Samples

The repository contains a sample Umbraco site for each supported Umbraco version. The Ekom team uses them to develop and test Ekom, and you can read them to see how a storefront, a checkout and the optional packages fit together in a real site. They are examples to learn from, not templates to start a shop from.

On this page:

- [The sample sites](#the-sample-sites)
- [What the samples show](#what-the-samples-show)
- [Run a sample](#run-a-sample)
- [What the samples do not do](#what-the-samples-do-not-do)

## The sample sites

| Sample | Umbraco | .NET | Ekom projects it uses |
| --- | --- | --- | --- |
| `Samples/U17/Ekom.Site.U17` | 17.8.1 | .NET 10 | `Ekom.U17`, `Ekom.Web.U17`, `Ekom.Printer`, and the main Algolia, Klaviyo and Mailchimp projects |
| `Samples/U18/Ekom.Site.U18` | 18.3.1 | .NET 10 | `Ekom.U18`, `Ekom.Web.U18`, `Ekom.Printer`, and the U18 Algolia, Klaviyo and Mailchimp projects |
| `Samples/U19/Ekom.Site.U19` | 19.0.0-rc1 | .NET 11 | `Ekom.U19`, `Ekom.Web.U19`, `Ekom.Printer`, and the U19 Algolia, Klaviyo and Mailchimp projects |

Each sample references the Ekom and plugin projects in the repository directly, not the NuGet packages. Each has its own SQLite database, checked in under `umbraco/Data`.

The U18 and U19 samples are not complete sites on their own. They link most of their code and views from the U17 sample: the custom product filter, the member login controller, the utility code and all views except `_ViewImports.cshtml`. A change in the U17 sample changes them too.

## What the samples show

- **Startup.** `Program.cs` uses Umbraco's normal builder with `AddComposers()`, which sets Ekom up. It does not call `AddEkom`. The explicit calls `AddAlgolia()`, `AddKlaviyo()`, `AddMailchimp()` and `AddEkomPrinter(...)` register the optional packages, which need their own registration.
- **Replacing an Ekom service.** The sample registers its own `IProductFilterService`, `CustomProductFilterService`. In the Development environment it also replaces the product and variant factories with versions for members.
- **A storefront and checkout.** The views cover a front page, category and product pages, and a checkout in steps: basket, contact information, delivery options, payment options and receipt.
- **Member login**, through a small `AuthController`.
- **Printing.** Printing is set up in `appsettings.json` with a placeholder station. See `Samples/PRINTING.md` in the repository and the [Ekom.Printer README](https://github.com/Vettvangur/Ekom/blob/Ekom/Ekom/Printing/Ekom.Printer/README.md).
- **Settings.** The `Ekom` section of each sample's `appsettings.json` shows many of the settings described in [Configuration](../getting-started/configuration.md).

## Run a sample

From the repository root:

```bash
dotnet run --project "Samples/U17/Ekom.Site.U17/Ekom.Site.U17.csproj"
```

You need the .NET SDK for the sample's version, and Node.js for its web-assets project, because running a sample also builds the Ekom backoffice client. The versions are in [Set up a development environment](../contributing/setup.md#install-the-tools).

All three samples are in `Ekom Site.sln`. None is in `Ekom Build.sln`.

The U17 sample can use payment providers from a local checkout of the Ekom.Payments repository, in a folder named `Ekom.Payments` next to this repository's folder. Without it, the sample has offline payment only. See [Run a sample site](../contributing/setup.md#run-a-sample-site).

## What the samples do not do

- **The data is test data.** The SQLite databases are in Git. Changes you make while running a sample show up as changes to those files.
- **The settings are not safe to copy.** The checked-in configuration has test, placeholder and development values for payments, printing and the integrations. None of them is a real credential, and none is safe for production.
- **Integrations may be registered but not configured.** An integration can be registered in `Program.cs` without working settings. Turn one on only after you add real credentials through user secrets or environment variables.
- **No production setup.** The samples have no payment provider onboarding, production deployment settings, secret management, monitoring, accessibility review or security review.

To build a real shop, install the package pair for your Umbraco version in your own site, as described in [Installation](../getting-started/installation.md). Then set up stores and providers for your environment, and build the storefront and checkout your project needs.

## Related pages

- [Installation](../getting-started/installation.md): install Ekom in your own site.
- [Set up a development environment](../contributing/setup.md): tools, solutions and running a sample.
- [Checkout](../guides/checkout.md): the checkout flow the sample views use.
- [Integrations](../integrations/README.md): the Algolia, Klaviyo and Mailchimp packages the samples register.

[Back to the documentation home](../README.md)

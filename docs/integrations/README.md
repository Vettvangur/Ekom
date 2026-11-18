# Integrations

[Ekom docs](../README.md) / Integrations

Ekom has optional packages that connect a store to Algolia, Klaviyo and Mailchimp. This page shows which package to install for your Umbraco version and where to find each package's instructions. Ekom runs without them.

Each package has its own README with the installation steps, settings and API. These docs link to the READMEs and do not repeat them.

## Choose the package for your Umbraco version

| Integration | What it does | Umbraco 17 | Umbraco 18 | Umbraco 19 | Instructions |
| --- | --- | --- | --- | --- | --- |
| Algolia | Indexes products, categories and content in Algolia, searches them, and sends Algolia Insights events. | `Ekom.Algolia` | `Ekom.Algolia.U18` | `Ekom.Algolia.U19` | [Algolia README](https://github.com/Vettvangur/Ekom/blob/Ekom/Plugins/Ekom.Algolia/README.md) |
| Klaviyo | Sends order events and catalog data to Klaviyo, and manages subscriptions. | `Ekom.Klaviyo` | `Ekom.Klaviyo.U18` | `Ekom.Klaviyo.U19` | [Klaviyo README](https://github.com/Vettvangur/Ekom/blob/Ekom/Plugins/Ekom.Klaviyo/README.md) |
| Mailchimp | Manages audience tags and subscriptions, tracks completed purchases, and sends transactional email. | `Ekom.Mailchimp` | `Ekom.Mailchimp.U18` | `Ekom.Mailchimp.U19` | [Mailchimp README](https://github.com/Vettvangur/Ekom/blob/Ekom/Plugins/Ekom.Mailchimp/README.md) |

The package without a suffix is the one for Umbraco 17; its .NET 10 build depends on `Ekom.U17`. The `.U18` and `.U19` packages depend on `Ekom.U18` and `Ekom.U19`. Install the package that matches the Ekom runtime package on your site.

## What every integration has in common

- **You register it yourself.** Installing the package is not enough. Call its registration method in `Program.cs`: `AddAlgolia()`, `AddKlaviyo()` or `AddMailchimp()`. What each package registers with Umbraco by itself is limited to event handlers (and, for Klaviyo, its controllers); its services come only from that call.
- **Its settings are under `Ekom`.** The packages read `Ekom:Algolia`, `Ekom:Klaviyo` and `Ekom:Mailchimp`. Keep API keys in user secrets, environment variables or another secret store, not in committed files.
- **It has its own version.** Each integration is released on its own, with its own version number and changelog. The main, `.U18` and `.U19` packages of one integration always have the same version. The versions are not the same as Ekom's.

```csharp
using Ekom.Algolia;
using Ekom.Klaviyo;
using Ekom.Mailchimp;

builder.Services.AddAlgolia();
builder.Services.AddKlaviyo();
builder.Services.AddMailchimp();
```

Each method also accepts a delegate to change the options in code, for example `builder.Services.AddKlaviyo(options => { ... })`.

## Release history

- [Algolia changelog](https://github.com/Vettvangur/Ekom/blob/Ekom/Plugins/Ekom.Algolia/CHANGELOG.md)
- [Klaviyo changelog](https://github.com/Vettvangur/Ekom/blob/Ekom/Plugins/Ekom.Klaviyo/CHANGELOG.md)
- [Mailchimp changelog](https://github.com/Vettvangur/Ekom/blob/Ekom/Plugins/Ekom.Mailchimp/CHANGELOG.md)

## Related pages

- [Packages](../architecture/packages.md): every Ekom package, including the optional ones.
- [Compatibility](../getting-started/compatibility.md): the Ekom packages for each Umbraco version.
- [Configuration](../getting-started/configuration.md): Ekom's own settings.
- [Samples](../samples/overview.md): sites that register all three integrations.

[Back to the documentation home](../README.md)

[Ekom docs](../README.md) / [Getting started](README.md) / Installation

# Installation

This page shows how to add Ekom to an Umbraco 17, 18 or 19 site, what Ekom does the first time the site starts, and how to check that it is running. You install two NuGet packages in the site's web project. There is no setup code to write.

The web project is the project that hosts Umbraco: it has `Program.cs` and the `wwwroot` folder.

On this page:

- [Before you start](#before-you-start)
- [Install the packages](#install-the-packages)
- [Make Ekom available in your views](#make-ekom-available-in-your-views)
- [What Ekom does when the site starts](#what-ekom-does-when-the-site-starts)
- [Check that Ekom is running](#check-that-ekom-is-running)
- [Fix common installation problems](#fix-common-installation-problems)

## Before you start

- The site runs Umbraco 17, 18 or 19, and you know which package pair it needs. See [Compatibility](compatibility.md).
- The site uses a SQL Server or SQLite database.
- The .NET SDK for the site's target framework is installed: .NET 10 for Umbraco 17 and 18, .NET 11 for Umbraco 19.

## Install the packages

1. Open a terminal in the web project's folder.
2. Add the runtime package and the web-assets package for your Umbraco version:

   | Umbraco | Commands |
   | --- | --- |
   | 17 | `dotnet add package Ekom.U17` and `dotnet add package Ekom.Web.U17` |
   | 18 | `dotnet add package Ekom.U18` and `dotnet add package Ekom.Web.U18` |
   | 19 | `dotnet add package Ekom.U19` and `dotnet add package Ekom.Web.U19` |

   For example, on Umbraco 17:

   ```bash
   dotnet add package Ekom.U17
   dotnet add package Ekom.Web.U17
   ```

   To run the commands from another folder, add `--project path/to/YourSite.csproj`. In the Visual Studio Package Manager Console, use `Install-Package Ekom.U17` and `Install-Package Ekom.Web.U17` instead.
3. Check that `Program.cs` still calls `AddComposers()` on the Umbraco builder. New Umbraco sites have this call by default:

   ```csharp
   builder.CreateUmbracoBuilder()
       .AddBackOffice()
       .AddWebsite()
       .AddComposers()
       .Build();
   ```

4. Build the site.

Install the web-assets package in the web project itself. When the web project builds, the package copies Ekom's backoffice files into `wwwroot/App_Plugins/Ekom`. A class library in your solution can also reference the runtime package if it calls the Ekom API, but the web project still needs both packages.

### Why there is no setup code

The runtime package contains an Umbraco composer, `EkomComposer`. Umbraco finds it through `AddComposers()` and runs it at startup. The composer registers Ekom's catalog routing and URLs, its startup steps and its handlers for Umbraco content changes. It then calls `AddEkom`, which registers Ekom's services and adds Ekom's middleware to the request pipeline: session, a request size limit, and Ekom's request and tracking handling.

Do not call `AddEkom` yourself. The composer already calls it, and registering Ekom twice is not supported.

## Make Ekom available in your views

Razor views do not see Ekom until you import its namespaces. Add these lines to `Views/_ViewImports.cshtml`, below the lines Umbraco put there:

```cshtml
@using Ekom
@using Ekom.API
@using Ekom.Interfaces
@using Ekom.Models
@using Ekom.Utilities

@inject Catalog _catalog
@inject Order _order
@inject global::Ekom.API.Store _store
@inject global::Ekom.API.Providers _providers
```

- The `@using` lines let a view name Ekom's types, such as `IProduct` and `ProductQuery`, without their namespace.
- The `@inject` lines give every view the four Ekom services a storefront uses most: the catalog, the basket and orders, the stores, and the payment and shipping providers.
- `Store` is written as `global::Ekom.API.Store` because `Ekom.Models` also has a class named `Store`, and the name on its own would be ambiguous. `Providers` is written the same way so that it cannot clash with a namespace of that name in your site.
- The names `_catalog`, `_order`, `_store` and `_providers` are yours to choose. The sample site uses them. The pages in [Examples](../examples/README.md) inject the services at the top of each view instead, so that each example is complete without this file.

A view in a folder with its own `_ViewImports.cshtml`, such as an area, needs the lines there too.

## What Ekom does when the site starts

Ekom waits until Umbraco is fully running. While Umbraco's installer or an Umbraco upgrade is running, Ekom skips all of the steps below.

When Umbraco is running, Ekom:

1. Creates its database tables, or updates them to the current version. See [Startup and data](../architecture/startup-and-data.md) for the tables and indexes.
2. Creates the Ekom document types and data types, and the **Ekom** root content node with these folders under it: Catalog, Shipping Providers, Payment Providers, Discounts (with Product Discounts and Order Discounts), Stores, Zones and Metafields. It does this only when no **Ekom** root node exists, so your content is not replaced on later starts.
3. Loads stores, the catalog, providers, discounts, zones, prices and stock into memory.
4. From then on, refreshes that memory copy when content, domains or languages change in Umbraco.

If you create a new Umbraco site and install Ekom before running Umbraco's installer, Ekom does none of this during the installer. If the **Ekom** root node and the Ekom tables are not there after the installer finishes, restart the site.

## Check that Ekom is running

1. Start the site. On a new site, finish Umbraco's installer first.
2. Sign in to the Umbraco backoffice.
3. In the **Content** section, check that the **Ekom** root node and its folders exist.
4. In **Users**, open your user group and add the **Ekom** section to its allowed sections. Reload the backoffice and check that the **Ekom** section appears.
5. In the site's log, look for `Ekom started` and `Ekom cache rebuild published successfully`.

You can now set up your first store. Continue with [Configuration](configuration.md), then [Quick start](quick-start.md).

## Fix common installation problems

| What you see | Cause | What to do |
| --- | --- | --- |
| The **Ekom** section is not in the backoffice. | Your user group does not have the section, or the web-assets files are missing. | Add the **Ekom** section to your user group. Then check that the web project references the `Ekom.Web.*` package that matches the runtime package, and that `wwwroot/App_Plugins/Ekom` exists in the build or publish output. |
| The log says `Ekom cache initialization failed`, with the error `Ekom root node not found.` | The **Ekom** root node does not exist or is not published. | Publish the existing node, or restart the site so Ekom can create it. If Ekom could not create it, the log also says `Failed to Initialize EnsureNodesExist`. |
| The log says `Ekom cache initialization failed` with another error. | Ekom could not load the catalog into memory. | Read the error logged with it. If content kept changing while Ekom loaded it at startup, Ekom retries by itself. Otherwise fix the cause, then rebuild the Ekom caches from the backoffice or restart the site. |
| The log says `Ekom startup failed`. | Ekom's last startup step failed: setting up request cultures or updating the order table. | Read the error logged with it. The website keeps running, so do not take a running site as proof that Ekom started. |
| The site does not start and the error mentions `EkomOrders`. | The database is not SQL Server or SQLite, or the existing order table cannot get its required index. | See [Startup and data](../architecture/startup-and-data.md#required-order-index). |
| The site does not start with an `ArgumentOutOfRangeException` for `PollInterval`, `BatchSize` or `CompletedRetention`. | A value under `Ekom:Reservations` is out of range. | See [Configuration](configuration.md#stock-reservations). |
| The site does not start with `Ekom:Manager:Customers:IdentityField must be CustomerEmail or CustomerUsername.` | The setting has another value. | Set it to `CustomerEmail` (the default) or `CustomerUsername`, or remove it. |

## Related pages

- [Compatibility](compatibility.md): which packages match your Umbraco version.
- [Configuration](configuration.md): the settings to set after installing.
- [Startup and data](../architecture/startup-and-data.md): the database changes and memory loading in detail.
- [Access and permissions](../manager/access-and-permissions.md): who can use the **Ekom** section.

Next: [Configuration](configuration.md)

[Back to Getting started](README.md)

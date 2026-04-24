# Installation

This page shows the minimum setup needed to install Ekom into an Umbraco site.

## Packages to install

Install the appropriate Umbraco versioned package into your solution:

```bash
dotnet add package Ekom.U10
```

Or in Package Manager:

```powershell
Install-Package Ekom.U10
```

Install `Ekom.Web` into your main web project:

```bash
dotnet add package Ekom.Web
```

Or in Package Manager:

```powershell
Install-Package Ekom.Web
```

## Which package goes where

### `Ekom.U10`

Install this package in the project where you use Ekom APIs and services. If that code lives outside the main web project, make sure the project is referenced by the Umbraco web application.

### `Ekom.Web`

Install this package in the main Umbraco web project that contains `wwwroot`.

## Add Ekom references to `_ViewImports.cshtml`

Add these references to your `Views/Partials/_ViewImports.cshtml` file:

```cshtml
@using Ekom.Interfaces
@using Ekom.Utilities
@using Ekom.API
@using Ekom
@using Ekom.Models

@inject Order _order
@inject Catalog _catalog
@inject global::Ekom.API.Store _store
```

This makes the main Ekom APIs available in Razor views.

## First run behavior

When the site starts with Ekom installed, Ekom will bootstrap its required setup.

This includes creating Ekom-related data structures such as:

- document types
- data types
- nodes required by Ekom

You should also see an Ekom root node added to the Umbraco content tree.

After Ekom has completed its bootstrap setup, the next step is to create a store. This should be done before you start working with products, checkout, providers, or other Ekom features.

## Verify installation

After installing and starting the site, verify:

- the site starts successfully
- Ekom root content is created
- Umbraco backoffice loads normally
- Ekom manager assets are available if `Ekom.Web` is installed

## Common mistakes

## Installing only `Ekom.U10`

If you want the manager UI and web assets, you also need `Ekom.Web`.

## Forgetting `_ViewImports.cshtml`

If you are rendering Ekom from Razor, missing imports and injections will make examples fail.

## Installing `Ekom.Web` into the wrong project

`Ekom.Web` should go into the project that owns the site web assets.

## Related pages

- [What is Ekom](what-is-ekom.md)
- [Configuration](configuration.md)
- [Quick Start](quick-start.md)

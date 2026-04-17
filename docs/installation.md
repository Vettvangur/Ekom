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

Install into the Umbraco application that runs Ekom.

### `Ekom.Web`

Install into the main site project that contains the site web assets and `wwwroot`.

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
- [Appsettings Reference](appsettings-reference.md)
- [Quick Start](quick-start.md)

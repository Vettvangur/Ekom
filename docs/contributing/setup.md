# Set up a development environment

[Ekom docs](../README.md) / [Contributing](README.md) / Set up a development environment

This page shows how to prepare a machine to work on Ekom: the tools to install, how to build the two solutions, how to work on a plugin against your local Ekom code, and how to run a sample site. It is for people who change Ekom itself. To use Ekom on a site, see [Getting started](../getting-started/README.md).

On this page:

- [Install the tools](#install-the-tools)
- [Restore and build](#restore-and-build)
- [Choose a solution](#choose-a-solution)
- [Use local Ekom projects or published packages in plugins](#use-local-ekom-projects-or-published-packages-in-plugins)
- [Run a sample site](#run-a-sample-site)

## Install the tools

| Tool | Version | Needed for |
| --- | --- | --- |
| .NET 8 SDK | 8.0 | The core projects and tests, which target .NET 8 and .NET 10. |
| .NET 10 SDK | 10.0 | The Umbraco 17 and 18 projects, the core projects and the tests. |
| .NET 11 SDK | 11.0 | The Umbraco 19 projects: `Ekom.U19`, `Ekom.Web.U19`, the U19 plugin projects and the U19 sample. |
| Node.js and npm | Node.js `>=22.17.1`, npm `>=10.9.2` | Building `Ekom.Web.U17`. |
| Node.js and npm | Node.js `>=24.13.0`, npm `>=11` | Building `Ekom.Web.U18` and `Ekom.Web.U19`. |
| Git | Any current version | Cloning and branching. |

Install all three .NET SDKs if you build the solutions. The repository has no `global.json`, so it does not pin an SDK version. The release workflows use Node.js 24.13.0, which meets the requirement of all three web-assets projects.

The Node.js versions come from the `engines` field in each web-assets project's `Client/package.json`.

## Restore and build

From the repository root:

```bash
dotnet restore "Ekom Build.sln"
dotnet build "Ekom Build.sln"
```

To build the solution with the sample sites, all Umbraco versions and the main tests:

```bash
dotnet build "Ekom Site.sln"
```

Building a web-assets project (`Ekom.Web.U17`, `Ekom.Web.U18` or `Ekom.Web.U19`) also builds its backoffice client:

- If the project's `Client/node_modules` folder does not exist, the build runs `npm ci` in `Client`.
- On every build, it runs `npm run build` in `Client`.
- It then adds the built files under `App_Plugins/Ekom` to the build output and the package. This step also runs Node.js.

`Ekom.Web.U18` and `Ekom.Web.U19` have their own `Client` folder with their own `package.json`, but they build the TypeScript source in `Ekom/Ekom.Web/Ekom.Web.U17/Client/src`. A backoffice change is made once, in the U17 client.

The same applies to the runtime: `Ekom.U18` and `Ekom.U19` compile the source files of `Ekom/Ekom.Umbraco/Ekom.U17`. Use `#if UMBRACO_18` for code that applies to Umbraco 18 and 19, and `#if UMBRACO_19` for code that applies to Umbraco 19 only.

There is no separate lint command. The compiler, the analyzers configured in `.editorconfig`, TypeScript type checking and the tests are the checks.

## Choose a solution

The two solutions contain different projects, and neither contains every project in the repository.

| Solution | Contains | Does not contain |
| --- | --- | --- |
| `Ekom Build.sln` | The core projects; `Ekom.U17` and `Ekom.Web.U17`; the Mailchimp projects for all versions and `Ekom.Mailchimp.Tests`; the printing projects and their tests. | `Ekom.U18`, `Ekom.U19` and their web-assets projects; `Ekom.Tests`; Algolia and Klaviyo; the samples. |
| `Ekom Site.sln` | The core projects; the runtime and web-assets projects for Umbraco 17, 18 and 19; all sample sites; `Ekom.Tests`; Algolia and Klaviyo for all versions and `Ekom.Klaviyo.Tests`; the main Mailchimp project and `Ekom.Mailchimp.Tests`; the printing projects and their tests. | `Ekom.Mailchimp.U18` and `Ekom.Mailchimp.U19` as listed projects, and `BuildProject`. |

Both solutions also contain the older `Ekom.U10` and `Ekom.Web` projects, which these docs do not cover. They target .NET 8.

When you change a project that is not in the solution you build, build that project directly, for example:

```bash
dotnet build "Ekom/Ekom.Umbraco/Ekom.U19/Ekom.U19.csproj"
```

Then run the tests that cover it. See [Build, test and release](build-test-release.md).

## Use local Ekom projects or published packages in plugins

The MSBuild property `UseProjectReferences` decides how the Algolia, Klaviyo and Mailchimp projects, and `Ekom.Printer`, reference Ekom.

- `true` is the default. It is set in `Ekom/Directory.Build.props` and in each plugin's `Directory.Build.props`. The plugin references the Ekom projects in this repository, so a change in Ekom is visible in the plugin at once. Each plugin target references the matching project: `Ekom.U17` for the .NET 10 target of the main plugin project, and `Ekom.U18` or `Ekom.U19` for the plugin's `U18` and `U19` projects.
- `false` makes the plugin reference the published Ekom NuGet packages at the version in `EkomPackageVersion`. Use it to check the plugin the way a site will consume it:

  ```bash
  dotnet build "Plugins/Ekom.Klaviyo/U19/Ekom.Klaviyo.U19.csproj" -p:UseProjectReferences=false -p:EkomPackageVersion=1.4.0
  ```

The plugin release workflows first pack the Ekom packages they need into a local folder, add it as a NuGet source, and then build and pack the plugin with `UseProjectReferences=false`.

The sample sites always reference the local Ekom and plugin projects.

## Run a sample site

| Sample | Umbraco | .NET | Local URL |
| --- | --- | --- | --- |
| `Samples/U17/Ekom.Site.U17` | 17.8.1 | .NET 10 | `https://localhost:44318` |
| `Samples/U18/Ekom.Site.U18` | 18.3.1 | .NET 10 | `https://localhost:44319` |
| `Samples/U19/Ekom.Site.U19` | 19.0.0-rc1 | .NET 11 | `https://localhost:44320` |

Run one from the repository root:

```bash
dotnet run --project "Samples/U17/Ekom.Site.U17/Ekom.Site.U17.csproj"
```

Running a sample also builds its web-assets project, so the Node.js requirement for that Umbraco version applies.

The samples use a SQLite database that is checked in to the repository. Treat it as test data: your changes to it show up as changes in Git.

The U17 sample can use payment providers from a local checkout of the Ekom.Payments repository. It looks for it in a folder named `Ekom.Payments` next to this repository's folder. To use another folder, set the `EkomPaymentsRoot` property in an untracked `.csproj.user` file. Without the folder, the sample has offline payment only.

See [Samples](../samples/overview.md) for what each sample contains and what it does not do.

## Related pages

- [Build, test and release](build-test-release.md): run the tests and release packages.
- [Packages](../architecture/packages.md): which project builds which package.
- [Samples](../samples/overview.md): what the sample sites show.
- [Integrations](../integrations/README.md): the plugin packages and their READMEs.

[Back to Contributing](README.md)

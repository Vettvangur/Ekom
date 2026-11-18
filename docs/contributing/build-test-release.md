# Build, test and release

[Ekom docs](../README.md) / [Contributing](README.md) / Build, test and release

This page shows how to test a change to Ekom, how to check a package before it is released, and how releases work: which branch releases what, how the version number is chosen, and what the publishing workflow checks. It is for people who change Ekom itself.

On this page:

- [Run the tests](#run-the-tests)
- [Check a package](#check-a-package)
- [Write the pull request title](#write-the-pull-request-title)
- [How a release happens](#how-a-release-happens)
- [Release branches](#release-branches)
- [What the Ekom publishing workflow checks](#what-the-ekom-publishing-workflow-checks)

## Run the tests

Start with the narrowest command that covers your change.

```bash
dotnet test "Ekom/Tests/Ekom.Tests/Ekom.Tests.csproj"
dotnet test "Ekom/Tests/Ekom.Tests/Ekom.Tests.csproj" --filter "FullyQualifiedName~PriceTests"
dotnet test "Ekom/Tests/Ekom.Tests/Ekom.Tests.csproj" --filter "FullyQualifiedName=Ekom.Tests.Tests.PriceTests.Can_Calculate"
```

`Ekom.Tests` targets .NET 8 and .NET 10, and a plain `dotnet test` runs the tests on both. It covers `Ekom.Core` and the main `Ekom.Algolia` project. The tests use xUnit and Moq.

The tests marked `[Trait("Category", "Benchmark")]` are manual benchmarks. They are skipped unless the environment variable `EKOM_BENCH` is `1`.

Other test projects:

| Project | Covers | Target frameworks |
| --- | --- | --- |
| `Plugins/Ekom.Klaviyo.Tests` | `Ekom.Klaviyo` | .NET 8, .NET 10 |
| `Plugins/Ekom.Mailchimp.Tests` | `Ekom.Mailchimp` | .NET 10 |
| `Ekom/Tests/Ekom.Printer.Tests` | `Ekom.Printer` | .NET 8, .NET 10 |
| `Ekom/Tests/Ekom.Printer.Agent.Tests` | The printer agent client | .NET 8, .NET 10 |

`Ekom.Tests` is in `Ekom Site.sln` and not in `Ekom Build.sln`, so `dotnet test "Ekom Site.sln"` runs it and `dotnet test "Ekom Build.sln"` does not. See [Choose a solution](setup.md#choose-a-solution).

The backoffice client has its own checks. In `Ekom/Ekom.Web/Ekom.Web.U17/Client`:

```bash
npm run typecheck
npm test
```

`Ekom.Web.U18/Client` and `Ekom.Web.U19/Client` have `npm run typecheck`, which checks the shared source against the Umbraco 18 and 19 backoffice packages.

A change in the shared `Ekom.U17` source also builds into `Ekom.U18` and `Ekom.U19`. Build those projects directly when your change touches code inside `#if UMBRACO_18` or `#if UMBRACO_19`, because `Ekom Build.sln` does not contain them.

## Check a package

Build or pack the project you changed:

```bash
dotnet build "Plugins/Ekom.Klaviyo/Ekom.Klaviyo.csproj"
dotnet pack "Plugins/Ekom.Klaviyo/Ekom.Klaviyo.csproj"
```

For a plugin, also build it against the published Ekom packages, as a site would use it:

```bash
dotnet build "Plugins/Ekom.Klaviyo/Ekom.Klaviyo.csproj" -p:UseProjectReferences=false -p:EkomPackageVersion=1.4.0
```

Many projects restore with a lock file (`packages.lock.json`). When you change a package reference, commit the updated lock file. Do not delete a lock file.

## Write the pull request title

Pull requests are squash-merged, and the pull request title becomes the commit message. Release-please reads these commit messages to decide what to release, so the title must follow Conventional Commits. A check on pull requests to `Ekom` and `release/0.2` rejects other titles.

Allowed types: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`. A scope is optional.

```text
feat: add checkout setting
fix(orders): handle missing provider
chore: update dependencies
```

The type decides the version change for a package on version 1.0 or later, such as Ekom:

- `fix` gives a patch release, for example 1.4.0 to 1.4.1.
- `feat` gives a minor release, for example 1.4.0 to 1.5.0.
- A breaking change, marked with `!` after the type or a `BREAKING CHANGE:` footer, gives a major release.

Use `fix` for small changes and `feat` only for a new feature, because `feat` starts a minor release.

The Algolia and Klaviyo packages are still below 1.0. For them, a `feat` gives a patch release.

## How a release happens

Each package is a release-please component with its own path, version and tag. A change is released for a component when a commit touches its path:

| Component | Path | Tag |
| --- | --- | --- |
| Ekom | `Ekom/`, except `Ekom/Tests` and `Ekom/Printing` | `Ekom-v<version>` |
| Ekom.Printer | `Ekom/Printing/Ekom.Printer` | `Ekom.Printer-v<version>` |
| Ekom.Algolia, Ekom.Klaviyo, Ekom.Mailchimp | `Plugins/<plugin>`, with separate components for its `U18` and `U19` folders | `<component>-v<version>` |

The `U18` and `U19` components of a plugin share one version with the main plugin component.

1. You merge a pull request into `Ekom`.
2. The `release-please` workflow updates one combined release pull request. It lists every component with unreleased changes, bumps their versions in `.release-please-manifest.json` and in the project files, and updates their changelogs.
3. When a maintainer merges the release pull request, release-please creates one tag per released component.
4. Each tag starts that component's publishing workflow, which builds, packs and pushes the packages to NuGet.org. The workflows sign in to NuGet.org with trusted publishing, so no API key is stored in the repository. The Ekom workflow builds with the .NET 8, 10 and 11 SDKs and Node.js 24.13.0. The plugin workflows first build the plugin with `UseProjectReferences=false`, against Ekom packages packed in the same run.

Creating a branch or merging an ordinary pull request does not publish anything. Only merging the release pull request does.

Do not edit the versions in `.release-please-manifest.json` by hand, create release tags yourself, or publish packages outside these workflows, unless the maintainers ask for it. Before you merge a release pull request, read the generated changelog.

To change the release workflows, check them with `actionlint` and run `node --test .github/scripts/select-release-config.test.mjs`.

## Release branches

| Branch | Version line | Releases |
| --- | --- | --- |
| `Ekom` | `1.x.x` | Ongoing development. Releases Ekom, Ekom.Printer and the plugins. |
| `release/0.2` | `0.2.x` | Hotfixes for sites that still run 0.2. Releases only the Ekom packages, and only patch versions. |

The `release-please` workflow runs on both branches and opens release pull requests against the branch it runs on. On `Ekom` it uses `release-please-config.json`. The file `release-please-config-1.0.json` was used only to move the `Ekom` branch from 0.2 to 1.0.0; the workflow picks it only when the `Ekom` manifest version is a 0.2 version.

To release a hotfix on 0.2:

1. Create a branch from `release/0.2`.
2. Open a pull request with a `fix:` title that targets `release/0.2`.
3. Merge it, then review and merge the release pull request that release-please opens on `release/0.2`.
4. If the fix also applies to `Ekom`, port it with a separate pull request. Do not merge the 0.2 manifest, version files or release configuration into `Ekom`.

## What the Ekom publishing workflow checks

The `publish-ekom` workflow runs on an `Ekom-v*` tag. Before it uploads anything, it checks:

1. The tag is a stable `Ekom-v0.2.x` or `Ekom-v1.x.x` version. On `release/0.2`, only `Ekom-v0.2.x` is accepted. Another major version needs a change to this check.
2. The tag version matches the `Version` in `Ekom/Directory.Build.props` and the Ekom version in `.release-please-manifest.json`.
3. The packed set is exactly these 11 packages, all with the tag version: `Ekom.Common`, `Ekom.Core`, `Ekom.AspNetCore`, `Ekom.U10`, `Ekom.Web`, `Ekom.U17`, `Ekom.Web.U17`, `Ekom.U18`, `Ekom.Web.U18`, `Ekom.U19` and `Ekom.Web.U19`.
4. Each package records the full commit SHA of the tagged source.
5. For each package, the script `.github/scripts/verify-ekom-packages.py` asks NuGet.org whether that version already exists, unlisted versions included. If it exists and was built from the same commit, the package is skipped. If it exists from another commit or without source information, the whole release stops before any upload. Only an HTTP 404 answer counts as "does not exist"; any other error stops the release.

The upload does not use `--skip-duplicate`, so a version that appears on NuGet.org between the check and the upload makes the release fail instead of passing silently. Running the workflow again checks again, and skips packages already uploaded from the same commit.

Never reuse a version number for different source code. If a version on NuGet.org is taken by a wrong build, release the next version.

Run the tests for this script with:

```bash
python -m unittest discover -s .github/scripts -p 'test_*.py' -v
```

Pull requests that change `.github/scripts` or the publishing workflow run these tests on Linux and Windows. The check compares where a package came from, not whether two builds are byte for byte the same. The printer and plugin packages have their own publishing workflows. They do not run this check, and they push with `--skip-duplicate`, so a version that already exists on NuGet.org is skipped.

## Related pages

- [Set up a development environment](setup.md): tools, solutions and local project references.
- [Packages](../architecture/packages.md): which project builds which package.
- [Integrations](../integrations/README.md): the plugin packages and their changelogs.
- [Upgrading](../upgrading/README.md): changes in a release that need action from sites.

[Back to Contributing](README.md)

# Build, Test, and Release

## Build and test

Run the narrowest useful command first:

```bash
dotnet restore "Ekom Build.sln"
dotnet build "Ekom Build.sln"
dotnet test "Ekom/Tests/Ekom.Tests/Ekom.Tests.csproj"
```

The main test project targets both .NET 8 and .NET 10. To run one test area:

```bash
dotnet test "Ekom/Tests/Ekom.Tests/Ekom.Tests.csproj" --filter "FullyQualifiedName~PriceTests"
```

`Ekom.Tests` targets both `net8.0` and `net10.0`; a normal test invocation runs both. `Ekom Build.sln` does not contain the U18 Ekom projects or `Ekom.Tests`. `Ekom Site.sln` includes all three sample lines and the main tests, but omits Mailchimp and is still not complete repository coverage. See [Contributor setup](setup.md) for the exact solution split.

The U17 and U18 web-assets projects require their declared Node/npm versions when built from source. Their MSBuild targets run `npm ci` when needed and then build the client assets.

## Package checks

Build or pack the changed project directly when changing a package:

```bash
dotnet build "Plugins/Ekom.Klaviyo/Ekom.Klaviyo.csproj"
dotnet pack "Plugins/Ekom.Klaviyo/Ekom.Klaviyo.csproj"
```

Keep lock files current for projects that use them. Test plugin projects with `UseProjectReferences=true` for local source development, then use `-p:UseProjectReferences=false -p:EkomPackageVersion=<version>` when validating package references.

There is no standalone lint task. Compiler diagnostics, analyzers, TypeScript type checking, and tests provide the checks. The web client packages expose `npm run typecheck`; their MSBuild projects run `npm ci` when `node_modules` is absent and run `npm run build` before every build.

## Releases

Releases are managed with release-please on pushes to the `Ekom` branch. The release configuration tracks the Ekom runtime and the Klaviyo, Algolia, and Mailchimp components; the Mailchimp and Mailchimp U18 release versions are linked.

Use Conventional Commit-style pull request titles so release-please can determine the release change. Examples:

```text
feat: add checkout setting
fix: handle missing provider
chore: update dependencies
```

Merging a release-please release PR creates component tags. Tag-triggered publishing workflows build and pack the matching packages and push them to NuGet.org through trusted publishing. The Ekom workflow builds .NET 8 and .NET 10 package lines and uses Node.js 24.13.0 for web assets. Plugin workflows validate package references with `UseProjectReferences=false` before packing.

Do not manually edit manifest versions, create release tags, or publish artifacts outside these workflows unless the maintainers explicitly request it. Before a release, inspect the generated changelog, ensure package lock files are current, and verify every changed target that is not covered by the selected solution.

See [Contributor setup](setup.md) for environment requirements.

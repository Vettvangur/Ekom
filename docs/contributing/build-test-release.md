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

Ekom has two release branches:

| Branch | Release line | Purpose |
| --- | --- | --- |
| `Ekom` | `1.x.x` | New manager, status changes, and ongoing development |
| `release/0.2` | `0.2.x` | Hotfixes for existing installations |

The maintenance branch starts at published tag `Ekom-v0.2.378`. Its next release is `0.2.379`; it uses patch-only increments and tracks only the Ekom package group. Plugin and printer releases remain on `Ekom`.

On `Ekom`, while the published manifest still contains `0.2.x`, the workflow selects `release-please-config-1.0.json` to prepare exactly `1.0.0`. Once the release PR updates the manifest to `1.0.0`, subsequent runs select the normal configuration and use semantic versioning. Keep the bootstrap configuration identical to the normal configuration except for Ekom's `release-as` override until 1.0.0 ships. Do not manually bump the manifest to 1.0.0 before the release PR.

Release-please explicitly targets the triggering branch. Publishing requires a stable `Ekom-v0.2.x` or `Ekom-v1.x.x` tag matching both the Ekom build version and manifest. The maintenance branch's publishing guard permits only `0.2.x`. A future major release requires an explicit guard change.

For a hotfix, branch from `release/0.2`, open a `fix:` PR targeting `release/0.2`, then review and merge the generated release PR when ready to publish. Port applicable source fixes to `Ekom` separately; do not merge maintenance manifests, version files, or release configuration into `Ekom`.

Validate workflow changes with `actionlint` and `node --test .github/scripts/select-release-config.test.mjs`. Creating a branch or merging ordinary changes does not publish packages; merging the generated release PR creates the release tag and starts publishing.

Use Conventional Commit-style pull request titles so release-please can determine the release change. Examples:

```text
feat: add checkout setting
fix: handle missing provider
chore: update dependencies
```

Merging a release-please release PR creates component tags. Tag-triggered publishing workflows build and pack the matching packages and push them to NuGet.org through trusted publishing. The Ekom workflow builds .NET 8 and .NET 10 package lines and uses Node.js 24.13.0 for web assets. Plugin workflows validate package references with `UseProjectReferences=false` before packing.

Do not manually edit manifest versions, create release tags, or publish artifacts outside these workflows unless the maintainers explicitly request it. Before a release, inspect the generated changelog, ensure package lock files are current, and verify every changed target that is not covered by the selected solution.

See [Contributor setup](setup.md) for environment requirements.

# AGENTS

This file orients coding agents working in this repository.

## Scope
- Repo root: `D:\Ekom\Ekom`
- Primary plugin: `Plugins/Ekom.Klaviyo`
- Tests live in: `Ekom/Ekom.Tests`
- Solutions: `Ekom Build.sln`, `Ekom Site.sln`

## Build / Lint / Test
- Restore: `dotnet restore "Ekom Build.sln"`
- Build all: `dotnet build "Ekom Build.sln"`
- Build site solution: `dotnet build "Ekom Site.sln"`
- Build plugin only: `dotnet build "Plugins/Ekom.Klaviyo/Ekom.Klaviyo.csproj"`
- Pack plugin: `dotnet pack "Plugins/Ekom.Klaviyo/Ekom.Klaviyo.csproj"`
- Tests (all): `dotnet test "Ekom/Ekom.Tests/Ekom.Tests.csproj"`
- Tests via solution: `dotnet test "Ekom Build.sln"`
- Single test by name: `dotnet test "Ekom/Ekom.Tests/Ekom.Tests.csproj" --filter "FullyQualifiedName~PriceTests"`
- Single test by class+method: `dotnet test "Ekom/Ekom.Tests/Ekom.Tests.csproj" --filter "FullyQualifiedName=Ekom.Tests.Tests.PriceTests.Can_Calculate"`
- Run one trait (if used): `dotnet test "Ekom/Ekom.Tests/Ekom.Tests.csproj" --filter "Category=Unit"`
- Lint: no separate lint task; rely on dotnet build/test analyzers + `.editorconfig`
- Format: no formatter configured; respect `.editorconfig`

## Dependencies / Restore Notes
- .NET SDK: net8.0 (projects target net8.0).
- Plugin uses lock file; keep `Plugins/Ekom.Klaviyo/packages.lock.json`.
- `Plugins/Ekom.Klaviyo/Directory.Build.props` and `Ekom/Directory.Build.props` set `UseProjectReferences`.
- When `UseProjectReferences=true`, plugin references `Ekom/Ekom.U10/Ekom.U10.csproj`; otherwise uses NuGet.

## Repo-Specific Rules (Cursor/Copilot)
- No `.cursor/rules/`, `.cursorrules`, or `.github/copilot-instructions.md` found.

## Code Style Overview
- Language: C# with file-scoped namespaces and implicit usings enabled.
- Nullable reference types enabled; prefer explicit null handling.
- Indentation: 4 spaces; CRLF; final newline (see `.editorconfig`).
- Use System.Text.Json (JsonDocument/JsonObject) for payloads, not Newtonsoft, unless existing code does.
- Prefer early returns for guard clauses and feature flags.

## Imports
- Place using directives at the top; keep them sorted by namespace group.
- Use project namespaces first (Ekom.*), then framework (Microsoft.*, System.*), then third-party.
- Prefer explicit using rather than fully-qualified types for repeated use.

## Naming
- Namespaces: Ekom.*; file-scoped `namespace X;`.
- Types: PascalCase; interfaces prefixed with I (IKlaviyoProfilesClient).
- Methods/properties: PascalCase; locals/parameters: camelCase.
- Private fields: `_camelCase` prefix.
- Async methods: suffix `Async` with Task/ValueTask return.
- Enums: PascalCase values; avoid underscores (CA1707 is silent but keep consistent).

## Formatting
- Braces on new line; single-line `if` with immediate return allowed.
- Use expression-bodied members for trivial getters or lambdas when readable.
- Use `var` when type is obvious; otherwise explicit.
- Keep object/collection initializers multi-line when >1 member.
- Prefer trailing commas in multi-line initializers if consistent with nearby code.

## Types and Nullability
- Honor `<Nullable>enable</Nullable>` and avoid null-forgiving unless justified.
- Use nullable annotations on inputs/outputs when null is valid.
- Use `string.IsNullOrWhiteSpace` for string guards.
- For JSON parsing, check `JsonValueKind` before accessing values.
- Favor `IReadOnlyList<T>` for read-only collection returns.

## Async and Concurrency
- Use async/await and `ConfigureAwait(false)` in library calls.
- Use `ValueTask` for hot-path service methods, Task otherwise.
- CancellationToken is optional and last parameter (CA1068).
- Avoid sync-over-async; no `.Result`/`.Wait()`.

## Error Handling
- Prefer guard clauses and early exits before throwing.
- Throw `InvalidOperationException` for impossible/invalid state.
- Catch specific exceptions (e.g., `KlaviyoApiException`) and log at Debug/Warning as shown.
- Do not swallow exceptions unless explicitly handling a known case.
- Preserve stack by rethrowing `throw;` not `throw ex;`.

## Logging
- Use `ILogger<T>`; log with structured message templates.
- Include store alias or identifiers in logs when available.
- Avoid logging secrets (API keys, tokens, PII beyond necessary identifiers).

## Dependency Injection
- Register services via `KlaviyoServiceCollectionExtensions.AddKlaviyo`.
- Use `IOptions<T>` for configuration (`KlaviyoOptions`).
- Prefer `AddSingleton` for stateless services; `AddScoped` for per-request logic.

## JSON and API Payloads
- Klaviyo API expects snake_case keys; maintain mapping in mappers.
- Build payloads with `System.Text.Json.Nodes` for flexible shapes.
- Validate required identifiers before building payloads.
- Keep serialization logic in `Mappers/` and avoid scattering JSON logic.

## Collections and LINQ
- Avoid extra allocations; prefer loops for hot paths.
- Use `HashSet<T>` for uniqueness and membership checks.
- Use `Array.Empty<T>()` instead of new empty arrays.

## Tests
- Testing framework: xUnit with Moq.
- Test project: `Ekom/Ekom.Tests/Ekom.Tests.csproj`.
- Test naming: PascalCase; use descriptive method names.
- Use `Fact`/`Theory` and clear arrange/act/assert sections.

## Docs and Packaging
- Plugin packs README, CHANGELOG, and VV_Logo.png; update as needed.
- Keep `CHANGELOG.md` current for user-facing changes.
- When changing public APIs, update README or docs references.

## Analyzers / Rules
- `.editorconfig` enables many CA rules; treat warnings seriously.
- Exceptions: CA2007 is disabled; CS1591 is suggestion.
- Prefer `nameof` in exceptions and logs where relevant.

## File Hygiene
- Do not edit generated files under `obj/` or `bin/`.
- Keep line endings CRLF (per .editorconfig).
- Avoid non-ASCII unless existing file uses it.

## Common Patterns Observed
- Guard feature flags: `if (!_opt.Enabled) return;`.
- Use `Try...` methods returning null or bool for safe parsing.
- Use switch expressions for small mapping tables.
- Favor `internal sealed` for implementation classes.

## Suggested Workflow for Agents
- Identify target project/solution and run `dotnet restore`.
- Make changes with nullability and analyzers in mind.
- Run focused tests with `dotnet test` filters.
- Ensure formatting matches nearby code; avoid reformatting unrelated sections.

## Quick Commands (copy/paste)
```ps
dotnet restore "Ekom Build.sln"
dotnet build "Ekom Build.sln"
dotnet test "Ekom/Ekom.Tests/Ekom.Tests.csproj"
dotnet test "Ekom/Ekom.Tests/Ekom.Tests.csproj" --filter "FullyQualifiedName~PriceTests"
dotnet build "Plugins/Ekom.Klaviyo/Ekom.Klaviyo.csproj"
dotnet pack "Plugins/Ekom.Klaviyo/Ekom.Klaviyo.csproj"
```

## Notes
- If adding new projects, update solutions and lock files as needed.
- Keep API revisions and base URLs configurable via options.
- This repo supports Umbraco 10+; avoid older APIs.
- For local dev with plugin, confirm UseProjectReferences value.
- Update this file when build/test conventions change.

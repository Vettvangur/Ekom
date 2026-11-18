[Ekom docs](../README.md) / [Contributing](README.md) / Printer agent releases

# Printer agent releases

This page describes how the Windows printer agent and the Ekom.Printer NuGet package are built, signed and released, and how to check that a downloaded agent installer is genuine. The agent installer is released on GitHub, separately from Ekom's NuGet packages.

**Who this page is for:** Ekom maintainers who release the printer agent and the Ekom.Printer package. Operators who install the agent need only [Download the agent](#download-the-agent) and [Verify a downloaded installer](#verify-a-downloaded-installer).

On this page:

- [Download the agent](#download-the-agent)
- [Verify a downloaded installer](#verify-a-downloaded-installer)
- [Release a new agent version](#release-a-new-agent-version)
- [Configure signing](#configure-signing)
- [Repository settings the workflows need](#repository-settings-the-workflows-need)
- [Release the Ekom.Printer NuGet package](#release-the-ekomprinter-nuget-package)

## Download the agent

[Download agent releases](https://github.com/Vettvangur/Ekom/releases?q=printer-agent-v) from the GitHub releases filtered on `printer-agent-v`. Do not use the repository's `/releases/latest` link: it can point to an Ekom release that is not an agent.

Each release contains:

- the installer, `Ekom.Printer.Agent-<version>-win-x64-setup.exe`,
- `SHA256SUMS.txt`, with the hash of every other file,
- `INSTALLATION.md`, a copy of the [Windows agent guide](../../Ekom/Printing/Ekom.Printer.Agent/README.md),
- `LICENSE.txt`,
- `release.json`, with the version and repository,
- `ReleaseNotes.md`, the generated change notes.

## Verify a downloaded installer

Run three checks on the downloaded installer, here for version 1.0.1.

1. Compare its SHA-256 hash with the matching line in `SHA256SUMS.txt`:

   ```powershell
   Get-FileHash ./Ekom.Printer.Agent-1.0.1-win-x64-setup.exe -Algorithm SHA256
   ```

2. Check the Windows signature:

   ```powershell
   Get-AuthenticodeSignature ./Ekom.Printer.Agent-1.0.1-win-x64-setup.exe
   ```

3. Check the GitHub build provenance:

   ```bash
   gh attestation verify Ekom.Printer.Agent-1.0.1-win-x64-setup.exe --repo Vettvangur/Ekom
   ```

The hash shows that the download is complete and matches the release. The provenance shows which GitHub workflow built it. The Authenticode signature shows the Windows publisher. None of them replaces a test print on the warehouse computer. An installer built locally by hand has no GitHub provenance.

If a release is unsigned, its notes say **Unsigned installer**, and Windows SmartScreen or company application-control rules can block it. The [Windows agent guide](../../Ekom/Printing/Ekom.Printer.Agent/README.md#windows-security-warnings-and-company-managed-pcs) explains how to get it approved.

## Release a new agent version

The workflow **Build printer agent installer** (`.github/workflows/build-printer-agent.yml`) builds the installer.

1. Commit and push the reviewed agent code, build scripts and workflow. Finish all changes before you tag.
2. Choose a new version with three parts, for example `1.0.1`. Never reuse a tag or replace a published file.
3. Tag the reviewed commit and push the tag:

   ```bash
   git tag -a printer-agent-v1.0.1 <reviewed-commit-sha> -m "Printer agent 1.0.1"
   git push origin printer-agent-v1.0.1
   ```

4. Watch **Build printer agent installer** in GitHub Actions. The build job runs on Windows. It checks installer storage, tests transport and setup validation, builds the NSIS installer, tests the bundled HTML renderer, installs, reinstalls and uninstalls the agent, and prepares the release notes and checksums.
5. When the build job succeeds, a separate release job attests the installer's build provenance and creates a **draft pre-release** with the files listed under [Download the agent](#download-the-agent).
6. Edit the draft notes: list the real changes, known issues and the Ekom.Printer package versions you tested with.
7. Test physical printing and recovery on Windows.
8. Publish the release. Keep a candidate that is not validated marked as pre-release. Clear the pre-release flag when you publish a validated stable version.

How the workflow behaves:

- Every release is created as not "latest", so an agent draft never replaces the main Ekom release as the latest release.
- Running the workflow again on the same tag refreshes the draft's files but keeps notes a maintainer edited.
- The workflow never changes a release that is already published. It fails instead.
- Publishing a release and promoting a pre-release are always done by a maintainer.
- A manual run on a branch builds downloadable Actions artifacts only. Its version comes from the `version` input (default `1.0.0`).
- A manual run on an existing `printer-agent-v*` tag follows the release path and takes the version from the tag, ignoring the input.
- Pull requests to the `Ekom` branch build and test an unsigned installer with version `1.0.0`. Download it from the workflow artifacts for review. Pull request builds have read-only permissions, never use signing secrets, and never create releases.

The workflow must exist in the tagged commit. A manual run also needs the workflow on the repository's default branch. Editing these files locally does not publish anything.

## Configure signing

Without signing settings, builds are unsigned and suited for controlled testing, and the draft notes say **Unsigned installer**. To sign with an exportable Authenticode certificate that Windows `signtool` supports, add these repository settings:

| Repository setting | Purpose |
| --- | --- |
| Secret `PRINTER_AGENT_SIGNING_PFX_BASE64` | The signing certificate (PFX), Base64-encoded. |
| Secret `PRINTER_AGENT_SIGNING_PFX_PASSWORD` | The PFX password. |
| Variable `PRINTER_AGENT_TIMESTAMP_URL` | Your certificate provider's RFC 3161 timestamp server. |

The Windows job imports the certificate for the build only, signs the service and setup programs and the final installer, then removes the PFX file and the certificate with its private key. If the settings are incomplete or signing fails, the build stops; it never falls back to an unsigned build. The uninstaller that NSIS generates is not signed separately.

A certificate kept in hardware or a cloud signing service needs its own signing integration instead of the PFX settings. Never put credentials in source control or in release files.

## Repository settings the workflows need

- The release job needs `contents: write`, `id-token: write` and `attestations: write`. The build job needs only `contents: read`. Check that the repository's Actions settings allow these.
- The agent workflow never runs its release job on pull requests.
- Protect the `printer-agent-v*` tags with a repository rule, so only release maintainers can create them.

## Release the Ekom.Printer NuGet package

The website package Ekom.Printer is released by its own workflow, `.github/workflows/publish-ekom-printer.yml`, independently of the agent installer.

**What the workflow does.** On a pull request to `Ekom` or a manual run, it runs the printer tests on .NET 8 and .NET 10 and uploads the package as an artifact, without publishing. When a tag `Ekom.Printer-v<major>.<minor>.<patch>` is pushed (a prerelease suffix is allowed), it also publishes the package to NuGet through `NuGet/login@v1`.

**Versions and tags.** Release Please keeps one combined release pull request for every package with pending changes, and tracks `Ekom/Printing/Ekom.Printer` as its own component `Ekom.Printer`. A printer change adds the printer to that pull request and raises its version according to the commit types. Merging the pull request updates the package version and changelog and creates the `Ekom.Printer-v...` tag, which starts the NuGet workflow. The `RELEASE_PLEASE_TOKEN` secret must be a token that is allowed to start other workflows: a tag created with GitHub's default `GITHUB_TOKEN` does not start a new workflow.

`Ekom/Printing` and `Ekom/Tests` are excluded from the core Ekom release. The agent keeps its own manual `printer-agent-v...` release described above. The printer's `PackageVersion` is separate from the core `Version`, so a printer-only fix does not need a core release. A core change the printer depends on must be released first.

**Core dependency.** The printer project sets its lowest supported Ekom.Core version in `EkomPackageVersion`, currently `0.2.352`, which includes the transactional status hook the printer needs. Raise it only when the printer needs newer core APIs, and publish that core version before the printer release. The workflow does not take this from the latest core release. Tag builds compile against that published NuGet package, not local project references, so a printer cannot be published against a core package that lacks the API it needs. Pull request artifacts use local project references and are for review only, until the matching core release exists.

**Trusted Publishing.** Before the first publication, a NuGet account owner must create a Trusted Publishing policy on nuget.org:

- NuGet login user: `Vettvangur`, as in the other publishing workflows.
- GitHub repository owner: `Vettvangur`.
- Repository: `Ekom`.
- Workflow file: `publish-ekom-printer.yml` (file name only).
- Environment: leave empty. This workflow does not use a GitHub environment.
- Package scope: `Ekom.Printer`. Allow creating the package if it does not exist yet, and updates for later releases.

The policy is set on nuget.org; there is no API-key secret in GitHub. The policy owner must own the package and be allowed to create new packages. See [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing). Nothing in this repository creates the policy, and creating it does not publish a package or prove that the first sign-in from the workflow works.

## Related pages

- [Windows agent guide](../../Ekom/Printing/Ekom.Printer.Agent/README.md): install, configure and update the agent.
- [Ekom.Printer README](../../Ekom/Printing/Ekom.Printer/README.md): website setup and settings.
- [Printer agent protocol](../printer-agent-protocol.md): the API the agent uses.
- [Build, test and release](build-test-release.md): how the rest of Ekom is released.

[Back to Contributing](README.md)

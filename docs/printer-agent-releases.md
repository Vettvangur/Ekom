# Windows printer agent releases

Agent installers are distributed as GitHub Release assets, separately from Ekom's NuGet packages.
[Download public agent releases](https://github.com/Vettvangur/Ekom/releases?q=printer-agent-v).
Do not use the repository's `/releases/latest` link: it can point to an unrelated Ekom release.

## Create a release candidate

1. Commit and push the reviewed agent code, build scripts and workflow. Complete the changes before tagging.
2. Choose a new three-part agent version. Do not reuse a tag or replace a published binary.
3. Tag the intended commit and push the tag, for example:

   ```bash
   git tag -a printer-agent-v0.1.1 <reviewed-commit-sha> -m "Printer agent 0.1.1"
   git push origin printer-agent-v0.1.1
   ```

4. Watch **Build printer agent installer** in GitHub Actions. The Windows runner builds the NSIS installer,
   tests transport, rendering, installation, setup access, reinstall and uninstall, then prepares checksums.
5. After those checks and provenance attestation succeed, a separate job creates a **draft pre-release**.
   It contains the installer, `SHA256SUMS.txt`, `INSTALLATION.md`, `LICENSE.txt`, `release.json` and generated change notes.
6. Edit the draft's notes with actual changes, known issues and tested Ekom.Printer package versions.
   Test physical printing and recovery on Windows. Keep initial versions marked pre-release, then publish the draft.

Manual workflow runs on a branch produce downloadable Actions artifacts only. A manual run on an existing
`printer-agent-v*` tag follows the release path and derives the version from that tag, ignoring the version input.
Rerunning a tag build can refresh draft assets; it preserves maintainer-edited release text. Published releases
are never modified by this workflow. All releases are initially marked non-latest so agent drafts do not displace
the main Ekom release. Publishing and promoting a pre-release remain maintainer actions.

The workflow must exist in the tagged commit. For manual dispatch, GitHub also needs the workflow on the
repository's default branch. No workflow, tag or release is published merely by editing these local files.

## Signing configuration

Without signing configuration, builds remain available for controlled testing and the draft notes explicitly
say **unsigned**. To use an exportable Authenticode certificate supported by Windows signtool, configure:

| Repository setting | Purpose |
| --- | --- |
| Secret `PRINTER_AGENT_SIGNING_PFX_BASE64` | Base64-encoded signing PFX |
| Secret `PRINTER_AGENT_SIGNING_PFX_PASSWORD` | PFX password |
| Variable `PRINTER_AGENT_TIMESTAMP_URL` | Your certificate provider's RFC3161 timestamp endpoint |

The Windows job imports the certificate temporarily, signs the service/setup executables and final installer,
and removes the PFX and private-key certificate afterward. An incomplete configuration or signing failure stops
the build; it does not silently fall back to unsigned output. The generated NSIS uninstaller is not separately
Authenticode-signed by this initial workflow. Hardware-backed or cloud signing certificates need a provider-specific
signing integration instead of the PFX path. Do not place credentials in source control or release assets.

Repository Actions permissions/policies must allow the release job's `contents: write` and the build job's
`id-token: write` / `attestations: write`. The workflow never runs on pull requests. Protect the
`printer-agent-v*` tag namespace with repository rules so only release maintainers can create release tags.

## Verify a downloaded installer

Compare its SHA-256 with the matching entry in `SHA256SUMS.txt`:

```powershell
Get-FileHash ./Ekom.Printer.Agent-0.1.1-win-x64-setup.exe -Algorithm SHA256
Get-AuthenticodeSignature ./Ekom.Printer.Agent-0.1.1-win-x64-setup.exe
```

For GitHub build provenance:

```bash
gh attestation verify Ekom.Printer.Agent-0.1.1-win-x64-setup.exe --repo Vettvangur/Ekom
```

Checksums detect mismatched downloads; provenance identifies the build workflow; Authenticode identifies the
Windows publisher. These checks complement the Windows/hardware acceptance tests rather than replacing them.
Local manual builds do not automatically have a GitHub provenance attestation.

Pull requests targeting `Ekom` also build and smoke-test an unsigned installer using version `1.0.0`. Download it from the workflow artifacts for review. PR builds use read-only repository permissions, never use signing credentials, and never create releases. Attestation permissions belong only to the tag-triggered release job.

## Publishing the Ekom.Printer NuGet package

The server package uses `.github/workflows/publish-ekom-printer.yml`, independently of the Windows installer. PRs and manual runs test both frameworks and upload a package artifact without publishing. Pushing a reviewed `Ekom.Printer-v<major.minor.patch>` tag (optionally with a prerelease suffix) publishes through `NuGet/login@v1`.

Release Please creates separate component release PRs (the linked Mailchimp
packages remain grouped) and tracks `Ekom/Printing/Ekom.Printer` independently.
Printer package changes create a separate release PR and advance its stable version
(for example, `0.1.0-beta.1` to `0.1.0-beta.2`). Merging that PR updates the
package version and changelog and creates the `Ekom.Printer-v...` release tag,
which triggers the NuGet workflow. The existing `RELEASE_PLEASE_TOKEN` must be
a token allowed to trigger other workflows; GitHub's default `GITHUB_TOKEN`
does not trigger a new workflow when it creates a tag.

`Ekom/Printing` and `Ekom/Tests` are excluded from core release detection. Agent
projects remain on their separate manual `printer-agent-v...` release process.
The printer's `PackageVersion` is independent of the inherited core `Version`,
so a printer-only fix does not require a core version bump. Core changes still
require their own release before a printer release that depends on them.

Before the first publication, a NuGet account owner must create a Trusted Publishing policy:

- NuGet login user: `Vettvangur` (matching the existing repository publishing workflows).
- GitHub repository owner: `Vettvangur`.
- Repository: `Ekom`.
- Workflow file: `publish-ekom-printer.yml` (filename only).
- Environment: leave empty; this workflow does not use a GitHub environment.
- Package scope: `Ekom.Printer`; allow creation of the new package if it does not yet exist, and updates for subsequent releases.

See [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing). The policy is configured on nuget.org, not by adding an API-key secret in GitHub. Ensure the policy owner has the required package ownership and new-package scope.

The printer project declares its minimum published Ekom.Core dependency in `EkomPackageVersion` (currently `0.2.352`, which includes the transactional status hook). Raise that minimum only when the printer requires newer core APIs, and publish that core version before releasing the printer. The workflow does not derive this dependency from the latest core release version. Tag builds compile the printer against that published NuGet dependency instead of local project references, preventing publication against an older core package missing the required API. PR package artifacts use local project references and are for review until that matching core release is available. No NuGet publishing policy is created automatically by this repository.

#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$')][string]$Version,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-fA-F]{40}$')][string]$SourceRevision,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')][string]$Repository,
    [string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/installers')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = Split-Path $PSScriptRoot -Parent
$name = "Ekom.Printer.Agent-$Version-win-x64-setup.exe"
$installer = Join-Path $OutputDirectory $name
if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) { throw "Installer is missing: $name" }
if (@(Get-ChildItem $OutputDirectory -Filter '*-setup.exe').Count -ne 1) { throw 'Release directory must contain exactly one installer.' }
$protocolSource = Get-Content (Join-Path $repo 'Ekom/Ekom.Printer/Protocol/PrinterProtocol.cs') -Raw
if ($protocolSource -notmatch 'const int CurrentVersion\s*=\s*(\d+)\s*;') { throw 'Cannot determine the agent protocol version.' }
$protocol = [int]$Matches[1]
$signed = $false
if ($IsWindows) {
    $signature = Get-AuthenticodeSignature -FilePath $installer
    if ($signature.Status -notin @('Valid', 'NotSigned')) { throw "Invalid installer signature: $($signature.Status)" }
    $signed = $signature.Status -eq 'Valid'
}
$signing = if ($signed) { 'Authenticode-signed installer.' } else { '**Unsigned test installer.** Use only for controlled acceptance testing until a signed release is available.' }
Copy-Item (Join-Path $repo 'Ekom/Ekom.Printer.Agent/README.md') (Join-Path $OutputDirectory 'INSTALLATION.md')
Copy-Item (Join-Path $repo 'LICENSE') (Join-Path $OutputDirectory 'LICENSE.txt')
@{
    version = $Version; sourceRevision = $SourceRevision; repository = $Repository;
    protocolVersion = $protocol; authenticodeSigned = $signed; installer = $name
} | ConvertTo-Json | Set-Content (Join-Path $OutputDirectory 'release.json') -Encoding utf8NoBOM
$notes = @"
# Ekom Printer Agent $Version

$signing

Download **$name** for Windows x64. The installer includes the .NET runtime and Chromium;
clients do not need to install either separately. See INSTALLATION.md for setup and testing.

## Compatibility

- Agent/server protocol: **$protocol**.
- Requires the Ekom.Printer package and Manager assets from a compatible release.
- Until a published package compatibility matrix is established, use the Ekom.Printer
  implementation from [this source revision](https://github.com/$Repository/tree/$SourceRevision).
- Printer driver must be installed and available to the Windows service account.

## Changes

Maintainer: replace this paragraph with the agent-specific changes and known issues before publishing.
Source: [$SourceRevision](https://github.com/$Repository/commit/$SourceRevision).

## Upgrade

Pause automatic printing, let pending submissions finish, close setup and run the newer
installer as administrator. Settings and recovery journals are retained. Do not delete
journals to force a reprint. Automatic agent updates are not implemented.

## Release validation

- [ ] Review the passing Windows build, renderer and install/reinstall/uninstall checks.
- [ ] Confirm real-printer output and restart/recovery behavior under the service account.
- [ ] Record compatible Ekom.Printer package versions and known issues.
- [ ] Review signing status and complete the change notes.

This release is created as a draft pre-release for maintainer review; it is not published automatically.
SHA256SUMS.txt contains hashes of the installer and accompanying files. GitHub build
provenance can be verified with: gh attestation verify $name --repo $Repository
"@
$notes | Set-Content (Join-Path $OutputDirectory 'ReleaseNotes.md') -Encoding utf8NoBOM
$hashes = foreach ($file in @($name, 'INSTALLATION.md', 'LICENSE.txt', 'release.json', 'ReleaseNotes.md')) {
    $hash = (Get-FileHash (Join-Path $OutputDirectory $file) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $file"
}
$hashes | Set-Content (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Encoding ascii

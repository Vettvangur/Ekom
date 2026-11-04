#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$')][string]$Version,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')][string]$Repository,
    [string]$AssetDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/installers')
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
Set-StrictMode -Version Latest
$tag = "printer-agent-v$Version"
$files = @("Ekom.Printer.Agent-$Version-win-x64-setup.exe", 'INSTALLATION.md', 'LICENSE.txt', 'release.json', 'ReleaseNotes.md')
$manifest = @(Get-Content (Join-Path $AssetDirectory 'SHA256SUMS.txt'))
if ($manifest.Count -ne $files.Count) { throw 'Unexpected checksum manifest contents.' }
$assets = foreach ($file in $files) {
    $path = Join-Path $AssetDirectory $file
    $hash = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($manifest -cnotcontains "$hash  $file") { throw "Checksum mismatch: $file" }
    $path
}
$metadata = Get-Content (Join-Path $AssetDirectory 'release.json') -Raw | ConvertFrom-Json
if ($metadata.version -ne $Version -or $metadata.repository -ne $Repository) { throw 'Release metadata does not match the requested repository and version.' }
$assets += Join-Path $AssetDirectory 'SHA256SUMS.txt'

# A failed lookup may be a missing release or a transient error. Create will fail safely if it already exists.
$draft = & gh release view $tag --repo $Repository --json isDraft --jq '.isDraft' 2>$null
if ($LASTEXITCODE -eq 0) {
    if ($draft -ne 'true') { throw "Release $tag is already published. Use a new version; published assets are never overwritten." }
    & gh release upload $tag @assets --repo $Repository --clobber
    if ($LASTEXITCODE -ne 0) { throw 'Uploading draft assets failed.' }
    # Preserve notes edited by a maintainer. Updated generated notes are attached as ReleaseNotes.md.
} else {
    & gh release create $tag @assets --repo $Repository --verify-tag --draft --prerelease --latest=false `
        --title "Ekom Printer Agent $Version" --notes-file (Join-Path $AssetDirectory 'ReleaseNotes.md')
    if ($LASTEXITCODE -ne 0) { throw 'Creating the draft release failed.' }
}

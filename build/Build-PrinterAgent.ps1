#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [string]$CertificateThumbprint,
    [string]$TimestampUrl,
    [string]$SignTool = 'signtool.exe',
    [string]$MakeNsis = 'makensis.exe'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'Build on Windows x64 (or a Windows CI runner) with .NET 10 SDK, PowerShell 7 and NSIS 3.' }
$parsed = [version]$Version
if ($parsed.Major -gt 65535 -or $parsed.Minor -gt 65535 -or $parsed.Build -gt 65535) { throw 'Version components must not exceed 65535.' }
if (-not (Get-Command $MakeNsis -ErrorAction SilentlyContinue)) { throw 'Install NSIS 3 and add makensis.exe to PATH, or supply -MakeNsis with its full path.' }
if ($CertificateThumbprint -and -not $TimestampUrl) { throw 'Provide an RFC3161 TimestampUrl when signing.' }
if ($CertificateThumbprint -and $CertificateThumbprint -notmatch '^[0-9a-fA-F]{40}$') { throw 'Invalid signing certificate thumbprint.' }

function Invoke-Checked([string]$Executable, [string[]]$Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable failed with exit code $LASTEXITCODE." }
}

$repo = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repo 'artifacts/printer-agent'
$agent = Join-Path $output 'Agent'
$setup = Join-Path $output 'Setup'
$installer = Join-Path $repo 'artifacts/installers'
# These folders contain generated payload only. Keep prior installer artifacts for upgrade testing.
if (Test-Path $output) { Remove-Item $output -Recurse -Force }
New-Item -ItemType Directory -Force $agent, $setup, $installer | Out-Null
foreach ($item in @(@('Ekom.Printer.Agent', $agent), @('Ekom.Printer.Agent.Setup', $setup))) {
    $project = Join-Path $repo "Ekom/Printing/$($item[0])/$($item[0]).csproj"
    Invoke-Checked 'dotnet' @('publish', $project, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=false', "-p:Version=$Version", '-o', $item[1])
}

# Install the exact Chromium revision required by the pinned Playwright NuGet package into the installer payload.
$previousBrowserPath = $env:PLAYWRIGHT_BROWSERS_PATH
try {
    $env:PLAYWRIGHT_BROWSERS_PATH = Join-Path $agent 'browsers'
    Invoke-Checked 'pwsh' @('-NoProfile', '-File', (Join-Path $agent 'playwright.ps1'), 'install', 'chromium', '--only-shell')
} finally { $env:PLAYWRIGHT_BROWSERS_PATH = $previousBrowserPath }
if (-not (Get-ChildItem (Join-Path $agent 'browsers') -Recurse -Filter chrome-headless-shell.exe)) {
    throw 'The Windows Chromium headless-shell executable is missing from the payload.'
}

if ($CertificateThumbprint) {
    foreach ($file in @((Join-Path $agent 'Ekom.Printer.Agent.exe'), (Join-Path $setup 'Ekom.Printer.Agent.Setup.exe'))) {
        Invoke-Checked $SignTool @('sign', '/sha1', $CertificateThumbprint, '/fd', 'SHA256', '/tr', $TimestampUrl, '/td', 'SHA256', $file)
    }
}
$package = Join-Path $installer "Ekom.Printer.Agent-$Version-win-x64-setup.exe"
Invoke-Checked $MakeNsis @('/V3', "/DAGENT_VERSION=$Version", "/DAGENT_PAYLOAD=$agent", "/DSETUP_PAYLOAD=$setup",
    "/DOUTPUT_FILE=$package", (Join-Path $repo 'Ekom/Printing/Ekom.Printer.Agent.Installer/Agent.nsi'))
if (-not (Test-Path $package)) { throw "Installer was not created: $package" }
if ($CertificateThumbprint) {
    Invoke-Checked $SignTool @('sign', '/sha1', $CertificateThumbprint, '/fd', 'SHA256', '/tr', $TimestampUrl, '/td', 'SHA256', $package)
    Invoke-Checked $SignTool @('verify', '/pa', $package)
} else { Write-Warning 'This installer is unsigned. Use a signing certificate for customer releases.' }
Get-FileHash $package -Algorithm SHA256 | Format-List
Write-Output "Installer: $package"

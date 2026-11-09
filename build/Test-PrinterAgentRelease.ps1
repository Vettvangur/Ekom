#Requires -Version 7.0
# Uses a local installer and a mocked GitHub CLI. No remote release is created or changed.
param([Parameter(Mandatory)][string]$InstallerPath)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('ekom-release-test-' + [Guid]::NewGuid().ToString('N'))
New-Item $temporary -ItemType Directory | Out-Null
$global:PrinterReleaseTestCalls = [Collections.Generic.List[object]]::new()
$global:PrinterReleaseTestState = 'missing'
function global:gh {
    param([Parameter(ValueFromRemainingArguments)][string[]]$Arguments)
    $global:PrinterReleaseTestCalls.Add($Arguments)
    $global:LASTEXITCODE = 0
    if ($Arguments[1] -eq 'view') {
        switch ($global:PrinterReleaseTestState) {
            'missing' { $global:LASTEXITCODE = 1 }
            'draft' { 'true' }
            'published' { 'false' }
        }
    }
}
function Assert-Release([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
try {
    $fixture = Join-Path $temporary 'Ekom.Printer.Agent-0.1.0-win-x64-setup.exe'
    Copy-Item $InstallerPath $fixture
    & (Join-Path $PSScriptRoot 'New-PrinterAgentRelease.ps1') -Version 0.1.0 -SourceRevision ('a' * 40) -Repository 'example/ekom' -OutputDirectory $temporary
    Assert-Release (@(Get-Content (Join-Path $temporary 'SHA256SUMS.txt')).Count -eq 5) 'Expected five checksummed assets.'
    $publish = @{ Version = '0.1.0'; Repository = 'example/ekom'; AssetDirectory = $temporary }
    & (Join-Path $PSScriptRoot 'Publish-PrinterAgentDraft.ps1') @publish
    $create = $global:PrinterReleaseTestCalls | Where-Object { $_[1] -eq 'create' } | Select-Object -First 1
    foreach ($flag in @('--draft', '--prerelease', '--verify-tag', '--latest=false', '--notes-file')) {
        Assert-Release ($create -contains $flag) "Release creation is missing $flag."
    }
    $global:PrinterReleaseTestCalls.Clear()
    $global:PrinterReleaseTestState = 'draft'
    & (Join-Path $PSScriptRoot 'Publish-PrinterAgentDraft.ps1') @publish
    Assert-Release (@($global:PrinterReleaseTestCalls | Where-Object { $_[1] -eq 'upload' }).Count -eq 1) 'Draft retry did not upload assets.'
    Assert-Release (@($global:PrinterReleaseTestCalls | Where-Object { $_[1] -in @('edit', 'create') }).Count -eq 0) 'Draft retry replaced maintainer notes.'
    $global:PrinterReleaseTestCalls.Clear()
    $global:PrinterReleaseTestState = 'published'
    $blocked = $false
    try { & (Join-Path $PSScriptRoot 'Publish-PrinterAgentDraft.ps1') @publish }
    catch { $blocked = $_.Exception.Message -like '*already published*' }
    Assert-Release $blocked 'Published release was not protected.'
    Assert-Release ($global:PrinterReleaseTestCalls.Count -eq 1) 'Published release triggered a write.'
    $global:PrinterReleaseTestCalls.Clear()
    [IO.File]::AppendAllText($fixture, 'tampered')
    $blocked = $false
    try { & (Join-Path $PSScriptRoot 'Publish-PrinterAgentDraft.ps1') @publish }
    catch { $blocked = $_.Exception.Message -like '*Checksum mismatch*' }
    Assert-Release $blocked 'Tampered installer was not rejected.'
    Assert-Release ($global:PrinterReleaseTestCalls.Count -eq 0) 'Tampered installer reached GitHub.'
    Write-Output 'Release tests passed: checksums, draft-only creation, safe draft retry, published-release protection and tamper rejection.'
} finally {
    Remove-Item $temporary -Recurse -Force
    Remove-Item Function:\gh
    Remove-Variable PrinterReleaseTestCalls, PrinterReleaseTestState -Scope Global
}

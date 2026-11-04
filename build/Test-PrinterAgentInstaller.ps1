#Requires -Version 7.0
# Run only on a disposable Windows acceptance machine. This installs and uninstalls the supplied NSIS installer.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$InstallerPath)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'Installer acceptance requires Windows.' }
if (Get-Service EkomPrinterAgent -ErrorAction SilentlyContinue) { throw 'Use a clean test machine without an existing Ekom Printer Agent installation.' }
$data = Join-Path $env:ProgramData 'Ekom/PrinterAgent'
if (Test-Path $data) { throw 'Use a clean test machine without existing station settings or recovery data.' }
$installer = (Resolve-Path $InstallerPath).Path
$installRoot = Join-Path $env:ProgramFiles 'Ekom/Printer Agent'
$log = Join-Path $data 'installer.log'
$installed = $false
try {
    $process = Start-Process -FilePath $installer -ArgumentList '/S' -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Installer failed ($($process.ExitCode)); see $log" }
    $installed = $true
    $service = Get-Service EkomPrinterAgent
    try { $service.WaitForStatus('Running', [TimeSpan]::FromSeconds(30)) } finally { $service.Dispose() }
    $details = Get-CimInstance Win32_Service -Filter "Name='EkomPrinterAgent'"
    if ($details.StartMode -ne 'Auto' -or $details.StartName -ne 'NT AUTHORITY\LocalService') { throw 'Service startup or account differs from the installer contract.' }
    $acl = Get-Acl $data
    if (-not $acl.AreAccessRulesProtected) { throw 'Agent data directory inherits permissions.' }
    $permittedSids = @('S-1-5-18', 'S-1-5-19', 'S-1-5-32-544')
    foreach ($rule in $acl.Access) {
        $sid = $rule.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value
        if ($rule.AccessControlType -eq 'Allow' -and $sid -notin $permittedSids) { throw "Unexpected agent-data access for $sid" }
    }
    # Read-only setup command. No credentials are supplied and no paper is printed.
    $pipe = [IO.Pipes.NamedPipeClientStream]::new('.', 'Ekom.Printer.Agent.Setup.v1', [IO.Pipes.PipeDirection]::InOut)
    try {
        $pipe.Connect(5000)
        $request = [Text.Encoding]::UTF8.GetBytes('{"Command":"status"}' + "`n")
        $pipe.Write($request, 0, $request.Length)
        $pipe.Flush()
        $reader = [IO.StreamReader]::new($pipe)
        try { $reply = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
        if (-not $reply.Success -or $null -ne $reply.Settings) { throw 'Fresh service should be unconfigured and paused.' }
        Write-Output "Service setup verified as $($reply.ServiceIdentity)."
    } finally { $pipe.Dispose() }
    if (Test-Path (Join-Path $data 'settings.json')) { throw 'Fresh install unexpectedly contains station credentials.' }
    # Same-version reinstall exercises service replacement and preserves data without claiming orders.
    $probe = Join-Path $data 'retention-probe.txt'
    Set-Content $probe 'Keep this through repair and uninstall.'
    $before = Get-Content (Join-Path $installRoot 'installation.json') -Raw | ConvertFrom-Json
    $repair = Start-Process -FilePath $installer -ArgumentList '/S' -Wait -PassThru
    if ($repair.ExitCode -ne 0) { throw 'Same-version reinstall failed.' }
    $after = Get-Content (Join-Path $installRoot 'installation.json') -Raw | ConvertFrom-Json
    if ($before.Directory -eq $after.Directory) { throw 'Reinstall did not switch to a fresh payload.' }
    $service = Get-Service EkomPrinterAgent
    try { $service.WaitForStatus('Running', [TimeSpan]::FromSeconds(30)) } finally { $service.Dispose() }
    if ((Get-Content $probe) -ne 'Keep this through repair and uninstall.') { throw 'Repair altered retained data.' }
    Write-Output 'Fresh install, automatic startup, protected storage, setup connection and reinstall passed.' 
} catch {
    if (Test-Path $log) { Get-Content -LiteralPath $log | Write-Host }
    Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = (Get-Date).AddMinutes(-10) } -ErrorAction SilentlyContinue | Where-Object { $_.ProviderName -in @('Ekom Printer Agent', '.NET Runtime', 'Application Error') } | Select-Object TimeCreated, ProviderName, Message | Format-List | Out-Host
    throw
} finally {
    if ($installed) {
        # Run a copy so /_? wait semantics do not lock the installed uninstaller itself.
        $uninstallCopy = Join-Path ([IO.Path]::GetTempPath()) ('ekom-uninstall-' + [Guid]::NewGuid().ToString('N') + '.exe')
        Copy-Item (Join-Path $installRoot 'Uninstall.exe') $uninstallCopy
        try { $process = Start-Process -FilePath $uninstallCopy -ArgumentList @('/S', "_?=$installRoot") -Wait -PassThru }
        finally { Remove-Item $uninstallCopy -Force }
        if ($process.ExitCode -ne 0) { throw "Uninstall failed ($($process.ExitCode))." }
        if (Get-Service EkomPrinterAgent -ErrorAction SilentlyContinue) { throw 'Service remains after uninstall.' }
        if (Test-Path (Join-Path $installRoot 'versions')) { throw 'Application payload remains after uninstall.' }
        if (-not (Test-Path $data)) { throw 'Uninstall unexpectedly removed the retained data directory.' }
    }
}

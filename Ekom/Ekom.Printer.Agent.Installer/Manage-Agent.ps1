#Requires -Version 5.1
# Invoked by the elevated NSIS installer; never accepts credentials or customer HTML.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Install', 'Uninstall')][string]$Action,
    [string]$PayloadDirectory,
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version
)
# An indirect launch through NSIS can inherit PowerShell 7 module paths.
# This helper only uses built-in Windows PowerShell modules.
$env:PSModulePath = $PSHOME + '\Modules'
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$serviceName = 'EkomPrinterAgent'
$root = Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'Ekom\Printer Agent'
$data = Join-Path ([Environment]::GetFolderPath('CommonApplicationData')) 'Ekom\PrinterAgent'
$marker = Join-Path $root 'installation.json'
$registry = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\EkomPrinterAgent'
$shortcut = Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'Ekom Printer Agent Setup.lnk'
$mutex = New-Object Threading.Mutex($false, 'Global\EkomPrinterAgentInstaller')
$locked = $false
$logging = $false

function Assert-NoReparse([string]$Path) {
    $current = [IO.Path]::GetFullPath($Path)
    while ($current) {
        if ((Test-Path -LiteralPath $current) -and ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Refusing a reparse-point installation path: $current"
        }
        $parent = Split-Path $current -Parent
        if ($parent -eq $current) { break }
        $current = $parent
    }
}
function Invoke-ServiceCommand([string[]]$Arguments) {
    & "$env:SystemRoot\System32\sc.exe" @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Service command failed ($LASTEXITCODE)." }
}
function Stop-Agent {
    $service = Get-Service $serviceName -ErrorAction SilentlyContinue
    if ($service) {
        try {
            if ($service.Status -ne 'Stopped') {
                $service.Stop()
                $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(90))
            }
        } finally { $service.Dispose() }
    }
}
function Start-Agent {
    $service = Get-Service $serviceName
    try { $service.Start(); $service.WaitForStatus('Running', [TimeSpan]::FromSeconds(30)) }
    finally { $service.Dispose() }
}
function Set-AgentBinary([string]$Binary) {
    # CIM preserves the literal quotes in the stored executable path, including on PowerShell 5.1.
    $service = Get-CimInstance Win32_Service -Filter "Name='$serviceName'"
    $result = Invoke-CimMethod -InputObject $service -MethodName Change -Arguments @{
        PathName = $Binary; StartMode = 'Automatic'; StartName = 'NT AUTHORITY\LocalService'; StartPassword = ''
    }
    if ($result.ReturnValue -ne 0) { throw "Service path update failed ($($result.ReturnValue))." }
}
function Write-Marker($State) {
    $temporary = $marker + '.tmp'
    $State | ConvertTo-Json | Set-Content -LiteralPath $temporary -Encoding UTF8
    Move-Item -LiteralPath $temporary -Destination $marker -Force
}
function Set-Shortcut([string]$Directory) {
    $shell = New-Object -ComObject WScript.Shell
    try {
        $link = $shell.CreateShortcut($shortcut)
        try { $link.TargetPath = Join-Path $Directory 'Setup\Ekom.Printer.Agent.Setup.exe'; $link.Save() }
        finally { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($link) | Out-Null }
    } finally { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null }
}
function Register-Uninstall([string]$DisplayVersion) {
    New-Item $registry -Force | Out-Null
    $properties = @{
        DisplayName = 'Ekom Printer Agent'; DisplayVersion = $DisplayVersion; Publisher = 'Ekom';
        InstallLocation = $root; UninstallString = '"' + (Join-Path $root 'Uninstall.exe') + '"';
        QuietUninstallString = '"' + (Join-Path $root 'Uninstall.exe') + '" /S'
    }
    foreach ($key in $properties.Keys) { New-ItemProperty $registry -Name $key -Value $properties[$key] -PropertyType String -Force | Out-Null }
    foreach ($key in @('NoModify', 'NoRepair')) { New-ItemProperty $registry -Name $key -Value 1 -PropertyType DWord -Force | Out-Null }
}

try {
    if (-not [Environment]::Is64BitProcess) { throw 'Run this installer helper in 64-bit Windows PowerShell.' }
    try { $locked = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $locked = $true }
    if (-not $locked) { throw 'Another Ekom agent installation is in progress.' }
    Assert-NoReparse $root
    Assert-NoReparse $data
    if (-not (Test-Path $data)) {
        New-Item $data -ItemType Directory -Force | Out-Null
        # Preserve the existing owner/group; a blank descriptor can fail Set-Acl on Windows.
        $acl = Get-Acl -LiteralPath $data
        $acl.SetAccessRuleProtection($true, $false)
        foreach ($rule in @($acl.Access)) { $acl.RemoveAccessRuleSpecific($rule) }
        foreach ($sid in @('S-1-5-18', 'S-1-5-19', 'S-1-5-32-544')) {
            $identity = New-Object Security.Principal.SecurityIdentifier($sid)
            $rule = New-Object Security.AccessControl.FileSystemAccessRule($identity, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
            $acl.AddAccessRule($rule)
        }
        Set-Acl -LiteralPath $data -AclObject $acl
    }
    $acl = Get-Acl -LiteralPath $data
    if (-not $acl.AreAccessRulesProtected) { throw 'The existing data directory has unsafe inherited permissions. Resolve its ACL before installation.' }
    foreach ($rule in $acl.Access) {
        if ($rule.AccessControlType -eq 'Allow' -and $rule.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value -notin @('S-1-5-18', 'S-1-5-19', 'S-1-5-32-544')) {
            throw 'The existing data directory grants access outside the service and administrator accounts.'
        }
    }
    Start-Transcript -Path (Join-Path $data 'installer.log') -Append | Out-Null
    $logging = $true
    $previous = $null
    if (Test-Path $marker) {
        $previous = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
        $versionRoot = [IO.Path]::GetFullPath((Join-Path $root 'versions')) + '\'
        if (-not [IO.Path]::GetFullPath($previous.Directory).StartsWith($versionRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid installed payload location.' }
        Assert-NoReparse $previous.Directory
    }
    $existingService = Get-CimInstance Win32_Service -Filter "Name='$serviceName'"
    if ($existingService) {
        if (-not $previous) { throw 'An unmanaged service already uses the Ekom agent name.' }
        $expected = '"' + (Join-Path $previous.Directory 'Agent\Ekom.Printer.Agent.exe') + '"'
        if ($existingService.PathName -ne $expected -or $existingService.StartName -ne 'NT AUTHORITY\LocalService') { throw 'The existing service path or account differs from the managed installation.' }
    }
    if ($Action -eq 'Uninstall') {
        if (-not $previous) { throw 'No managed Ekom agent installation was found.' }
        Stop-Agent
        if ($existingService) { Invoke-ServiceCommand @('delete', $serviceName) }
        Remove-Item -LiteralPath $shortcut -Force -ErrorAction SilentlyContinue
        # Only application-owned version payloads are removed. Never remove ProgramData or its journals.
        Remove-Item -LiteralPath (Join-Path $root 'versions') -Recurse -Force
        Remove-Item -LiteralPath $marker -Force
        Remove-Item -LiteralPath $registry -Recurse -Force -ErrorAction SilentlyContinue
        exit 0
    }
    if (-not $Version -or -not $PayloadDirectory) { throw 'Install requires a version and payload directory.' }
    if ($previous -and [version]$previous.Version -gt [version]$Version) { throw 'A newer agent is already installed. Downgrades are not supported.' }
    foreach ($file in @('Agent\Ekom.Printer.Agent.exe', 'Setup\Ekom.Printer.Agent.Setup.exe')) {
        if (-not (Test-Path -LiteralPath (Join-Path $PayloadDirectory $file))) { throw "Missing payload: $file" }
    }
    $next = Join-Path $root ('versions\' + $Version + '-' + [Guid]::NewGuid().ToString('N'))
    New-Item $next -ItemType Directory -Force | Out-Null
    $wasRunning = $existingService -and $existingService.State -eq 'Running'
    $switched = $false
    $newService = $false
    try {
        Copy-Item -LiteralPath (Join-Path $PayloadDirectory 'Agent'), (Join-Path $PayloadDirectory 'Setup') -Destination $next -Recurse
        $exe = Join-Path $next 'Agent\Ekom.Printer.Agent.exe'
        # Do this before stopping a previous working service. It also proves the new executable can launch.
        & $exe --register-event-source
        if ($LASTEXITCODE -ne 0) { throw 'Agent event-source registration failed.' }
        Stop-Agent
        $binary = '"' + $exe + '"'
        if ($existingService) {
            Set-AgentBinary $binary
        } else {
            $result = Invoke-CimMethod -ClassName Win32_Service -MethodName Create -Arguments @{
                Name = $serviceName; DisplayName = 'Ekom Printer Agent'; PathName = $binary;
                ServiceType = [byte]16; ErrorControl = [byte]1; StartMode = 'Automatic';
                DesktopInteract = $false; StartName = 'NT AUTHORITY\LocalService'; StartPassword = '';
                ServiceDependencies = [string[]]@('Spooler')
            }
            if ($result.ReturnValue -ne 0) { throw "Service creation failed ($($result.ReturnValue))." }
            $newService = $true
        }
        $switched = $true
        Invoke-ServiceCommand @('failure', $serviceName, 'reset=', '86400', 'actions=', 'restart/60000/restart/60000/restart/60000')
        if (-not $existingService -or $wasRunning) { Start-Agent }
        Write-Marker @{ Version = $Version; Directory = $next }
        Set-Shortcut $next
        Register-Uninstall $Version
    } catch {
        $failure = $_
        try {
            if ($switched) { Stop-Agent }
            if ($newService) { Invoke-ServiceCommand @('delete', $serviceName) }
            if ($previous) {
                if ($switched) { Set-AgentBinary ('"' + (Join-Path $previous.Directory 'Agent\Ekom.Printer.Agent.exe') + '"') }
                Write-Marker $previous
                Set-Shortcut $previous.Directory
                Register-Uninstall $previous.Version
                if ($wasRunning -and (Get-CimInstance Win32_Service -Filter "Name='$serviceName'").State -ne 'Running') { Start-Agent }
            } else {
                Remove-Item -LiteralPath $marker, $shortcut -Force -ErrorAction SilentlyContinue
                Remove-Item -LiteralPath $registry -Recurse -Force -ErrorAction SilentlyContinue
            }
            Remove-Item -LiteralPath $next -Recurse -Force
        } catch { Write-Warning "Automatic rollback needs administrator attention: $_" }
        throw $failure
    }
    # Retain the prior payload until the service transition succeeds. Locked setup files can be cleaned on uninstall.
    if ($previous) { Remove-Item -LiteralPath $previous.Directory -Recurse -Force -ErrorAction SilentlyContinue }
    Write-Output "Installed Ekom Printer Agent $Version. Station configuration and recovery records were preserved."
} catch {
    # Preserve failures that occur before the protected ProgramData transcript exists.
    # Use a fresh unpredictable file in the invoking administrator's temp directory.
    $bootstrapLog = Join-Path ([IO.Path]::GetTempPath()) ('EkomPrinterAgent-install-' + [Guid]::NewGuid().ToString('N') + '.log')
    $failureText = $_ | Format-List * -Force | Out-String
    try {
        $stream = [IO.File]::Open($bootstrapLog, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try {
            $bytes = [Text.Encoding]::UTF8.GetBytes($failureText)
            $stream.Write($bytes, 0, $bytes.Length)
        } finally { $stream.Dispose() }
    } catch { Write-Warning "Could not write bootstrap diagnostics: $_" }
    Write-Error $failureText -ErrorAction Continue
    exit 1
} finally {
    if ($logging) { Stop-Transcript | Out-Null }
    if ($locked) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}

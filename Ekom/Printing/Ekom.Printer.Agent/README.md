# Ekom Printer Agent

Windows x64 service for ordinary Windows printers, with a separate administrator setup application.
The service and setup application are self-contained .NET 10 deployments. Clients do not need
the .NET SDK, a browser installation, or an open Ekom session. A Windows printer driver must
already be installed and available to the service account.

## Current validation status

The service and setup application have been cross-compiled and published for Windows x64.
Transport/recovery tests and real Chromium security/pagination tests run on macOS. Windows
service startup, installer lifecycle, setup UI and physical printing still require acceptance
on Windows. Do not treat cross-compilation as proof of hardware compatibility.

## Build

Public downloads are listed under [Printer Agent releases](https://github.com/Vettvangur/Ekom/releases?q=printer-agent-v).
Tags named `printer-agent-v<version>` trigger tested installer builds and draft pre-releases;
maintainers publish them after review. See the [release guide](../../../docs/printer-agent-releases.md)
for signing configuration, checksums, provenance verification and tag commands.

On a Windows x64 build machine, install the .NET 10 SDK, PowerShell 7 and
[NSIS 3](https://nsis.sourceforge.io/Download). Put `makensis.exe` on PATH, or supply
`-MakeNsis` with its full path. Run from the repo root:

```powershell
./build/Build-PrinterAgent.ps1 -Version 0.1.0
```

The script publishes the service and setup app, bundles the pinned Playwright Chromium
headless shell, and builds `artifacts/installers/Ekom.Printer.Agent-<version>-win-x64-setup.exe`. Internet access is
needed on the build machine to restore NuGet dependencies and download Chromium. The client
computer never downloads browser binaries during startup. NSIS packaging is separate
from the normal Ekom NuGet build; it does not use WiX or require its EULA acceptance.

Optional release signing uses a code-signing certificate already installed on the build machine:

```powershell
./build/Build-PrinterAgent.ps1 -Version 0.1.0 `
  -CertificateThumbprint YOUR_CERTIFICATE_THUMBPRINT `
  -TimestampUrl YOUR_RFC3161_TIMESTAMP_SERVICE
```

`signtool.exe` must be on PATH (or supply `-SignTool`). Without these parameters the output
is unsigned and intended for acceptance testing. Keep bundled third-party license notices.

## Configure a warehouse

1. Install as administrator. The service starts automatically but does not claim orders until configured and enabled.
2. Open **Ekom Printer Agent Setup** from the Start menu and allow administrator elevation.
3. Enter the HTTPS website URL, the station alias from `Ekom:Printing:Stations`, and the raw random station credential.
   The website stores the SHA-256 hash; the agent needs the original credential, not that hash.
   Generate both values using the [macOS or Windows instructions](../Ekom.Printer/README.md#create-a-station-token-and-tokensha256).
4. Select a printer from the service's list. Leave **Enable automatic printing** unchecked and save.
5. Click **Test connection**. This checks station authentication/protocol and updates the server heartbeat.
6. Click **Test print** and physically confirm the page. The service submits it using its own Windows identity.
7. Enable automatic printing and save. Confirm the station is online in **Ekom Manager > Printing**.

The default account is `NT AUTHORITY\LocalService`. A printer installed only for the logged-in
user may not be visible. Install a machine-wide printer queue accessible to LocalService;
network printers requiring domain credentials need a separately tested service-account deployment.
Do not silently change the service's account: credentials are encrypted to the original service
identity, and the data directory ACL must also be migrated. This first setup application does
not manage alternate service accounts. Avoid printers that display interactive save-file dialogs.

Setup communicates over an administrator-only local named pipe; there is no inbound TCP listener.
Before sending a credential, setup verifies the pipe belongs to the process registered with the
Windows Service Control Manager. The credential is protected using Windows DPAPI for the service
identity. Configuration and journals live in `%ProgramData%\Ekom\PrinterAgent`, with inherited
permissions disabled and access restricted to SYSTEM, Administrators and LocalService.

## Rendering and delivery

Ekom generates and delivers HTML. Chromium runs headless with JavaScript and service workers
disabled. A CSP and request interception permit embedded images/fonts and inline print CSS,
and block external requests, frames, plugins and local-file resources. The agent uses an
in-memory PDF to paginate HTML, then rasterizes pages at 200 DPI for Windows GDI printing.
This does not require PDF templates or a server-side PDF generator. Physical paper defaults
come from the selected printer; CSS page sizes are honored during layout and fitted to its
printable area. Test barcode readability and any unusually small text on the actual printer.

The Win32 StartDoc result is the real spool job ID. Submission success means Windows accepted
the job, not that paper came out. The journal is flushed before printing, and acknowledgements
retry without resubmitting the physical document. A crash or ambiguous print result goes to
`NeedsReview`. Never delete journals to force another copy. Station identity cannot change
while submission reports remain pending. Test prints are explicit local commands; after a
timeout, inspect the printer before requesting another test.

## Updates and uninstall

Pause automatic printing in setup and allow any in-flight submission/report to finish before
upgrading. Install a newer version over the existing installation. Settings and recovery data
survive upgrades. The installer stages a new version directory, stops the service, switches
its executable path, and restores the previous service on an ordinary installation failure.
An unexpected power loss during the switch can still require administrator recovery; this
is not an MSI transaction. Keep the prior installer and inspect `installer.log` and the
`installation.json` marker before repairing an interrupted installation. Downgrades are
rejected; rerunning the same version reinstalls its application payload. Uninstall removes the service and application but deliberately retains
the protected ProgramData directory. Securely remove that data only during intentional station
decommissioning after unresolved print outcomes have been reconciled.

## Acceptance tests

The NSIS source has been compiled with a complete Windows x64 payload, including Chromium.
The resulting installer is unsigned and has not been executed on Windows here.
The GitHub workflow builds installers on Windows and includes installation, service setup,
reinstall and uninstallation checks on its disposable runner. It does not print physical paper.
Manual branch runs produce Actions artifacts; agent tag runs also create draft pre-releases.
Signing is enabled only when the documented signing settings are configured.

To run Chromium tests directly after installing the pinned browser:

```powershell
dotnet build Ekom/Tests/Ekom.Printer.Agent.Tests/Ekom.Printer.Agent.Tests.csproj -f net10.0
pwsh Ekom/Tests/Ekom.Printer.Agent.Tests/bin/Debug/net10.0/playwright.ps1 install chromium --only-shell
$env:EKOM_RENDER_TESTS = '1'
dotnet test Ekom/Tests/Ekom.Printer.Agent.Tests/Ekom.Printer.Agent.Tests.csproj -f net10.0
```

On a disposable Windows machine verify fresh install, upgrade, failed-upgrade recovery, same-version reinstall,
uninstall, reboot/automatic startup, missing printers, offline website, incompatible protocol,
and a crash before/after spool submission. Check credentials and journals survive an upgrade.
Run hardware tests under the installed service account: multi-page tables, CSS page breaks,
embedded fonts/images, paper margins, barcodes, copies, printer offline/out-of-paper and recovery.
Application event-log diagnostics omit HTML, credentials and raw renderer exception messages.

### Silent deployment and smoke test

```powershell
./Ekom.Printer.Agent-0.1.0-win-x64-setup.exe /S
# On a clean, disposable Windows test machine only:
./build/Test-PrinterAgentInstaller.ps1 -InstallerPath ./artifacts/installers/Ekom.Printer.Agent-0.1.0-win-x64-setup.exe
```

The installer uses the fixed `%ProgramFiles%\Ekom\Printer Agent` location, registers
an Apps & Features uninstall entry and a Start menu setup shortcut, and logs to
`%ProgramData%\Ekom\PrinterAgent\installer.log`. It does not accept station credentials
as command-line arguments. Close the setup window before upgrading or uninstalling.
The smoke test verifies installation, service identity, storage permissions, setup IPC,
same-version reinstall, data preservation and uninstall. It never submits a print job.

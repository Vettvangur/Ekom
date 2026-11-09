# Ekom Printer Agent

Windows x64 service for ordinary Windows printers, with a separate administrator setup application.
The service and setup application are self-contained .NET 10 deployments. Clients do not need
the .NET SDK, a browser installation, or an open Ekom session. A Windows printer driver must
already be installed and available to the service account.

## Current validation status

Windows CI validates service startup, setup communication, installer lifecycle, transport
and recovery behavior, and bundled Chromium rendering. The maintainer has confirmed agent
operation and physical printer output during pre-release testing. Each customer installation
should still run a test print under its service account to verify its driver and printer.

## Build

Public downloads are listed under [Printer Agent releases](https://github.com/Vettvangur/Ekom/releases?q=printer-agent-v).
Tags named `printer-agent-v<version>` trigger tested installer builds and draft pre-releases;
maintainers publish them after review. See the [release guide](../../../docs/printer-agent-releases.md)
for signing configuration, checksums, provenance verification and tag commands.

On a Windows x64 build machine, install the .NET 10 SDK, PowerShell 7 and
[NSIS 3](https://nsis.sourceforge.io/Download). Put `makensis.exe` on PATH, or supply
`-MakeNsis` with its full path. Run from the repo root:

```powershell
./build/Build-PrinterAgent.ps1 -Version 1.0.0
```

The script publishes the service and setup app, bundles the pinned Playwright Chromium
headless shell, and builds `artifacts/installers/Ekom.Printer.Agent-<version>-win-x64-setup.exe`. Internet access is
needed on the build machine to restore NuGet dependencies and download Chromium. The client
computer never downloads browser binaries during startup. NSIS packaging is separate
from the normal Ekom NuGet build; it does not use WiX or require its EULA acceptance.

Optional release signing uses a code-signing certificate already installed on the build machine:

```powershell
./build/Build-PrinterAgent.ps1 -Version 1.0.0 `
  -CertificateThumbprint YOUR_CERTIFICATE_THUMBPRINT `
  -TimestampUrl YOUR_RFC3161_TIMESTAMP_SERVICE
```

`signtool.exe` must be on PATH (or supply `-SignTool`). Without these parameters the output
is unsigned and intended for acceptance testing. Keep bundled third-party license notices.

## Windows security warnings and company-managed PCs

Builds made without a signing certificate are unsigned. Windows may display
**Windows protected your PC** and **Unknown publisher** when launching the installer
or setup application. This is a SmartScreen publisher/reputation warning; by itself,
it is not a malware detection and does not establish whether the application is safe.
See [Microsoft's SmartScreen guidance](https://learn.microsoft.com/windows/apps/package-and-deploy/smartscreen-reputation).

### Identify the block

- **Windows protected your PC / Unknown publisher:** for a verified build you are
  authorized to test, **More info > Run anyway** may be available. Check the download
  source and checksum first. This is a per-launch choice, not a reason to disable antivirus.
- **No Run anyway option:** a company policy may prevent bypassing SmartScreen.
  Local administrator access does not necessarily override that policy. Contact IT
  to identify the blocking control and approve the application through its deployment process.
- **Smart App Control blocked this app:** this is a separate Windows control with
  no per-app override. See the [Smart App Control FAQ](https://support.microsoft.com/en-us/windows/security/threat-malware-protection/smart-app-control-frequently-asked-questions).
- **Threat detected or quarantined:** inspect Windows Security > Virus & threat
  protection > Protection history and provide the detection name to IT and the
  maintainer. Do not assume an antivirus detection is merely an unsigned-file warning.

Do not disable company security controls or add broad antivirus exclusions to install
this agent. Turning off Defender antivirus does not necessarily remove a SmartScreen,
Smart App Control, or application-control policy block.

### Verify the installer and request IT approval

Download the exact version from this repository's release assets or its GitHub Actions
artifact. Actions artifacts may require GitHub sign-in and are not permanent public
release downloads. Extract the artifact ZIP, then calculate the installer EXE's hash:

```powershell
Get-FileHash -LiteralPath '.\Ekom.Printer.Agent-1.0.0-win-x64-setup.exe' -Algorithm SHA256
```

Replace the example filename with the one downloaded. Compare the result with that
build's `SHA256SUMS.txt`, obtained from the trusted GitHub release or artifact. The hash
is for the EXE, not its surrounding ZIP. Different builds can have the same filename
but different hashes. A matching hash verifies file integrity against that manifest;
it does not certify that the program is free of vulnerabilities or establish publisher identity.

Provide IT with:

- The exact GitHub release/artifact URL, installer filename, version and SHA-256 hash.
- The signing status: unsigned, or the verified signing publisher if signing was enabled.
- The full warning or detection name and whether the installer, setup app or service is blocked.
- The deployment details below and the staging acceptance results, when available.

Suggested request:

> Please review and approve this Ekom Printer Agent build for the warehouse PC.
> Windows blocks it without a Run anyway option. The attached download URL and SHA-256
> identify the exact installer. Please identify the blocking policy and deploy or allow
> the application through our approved software process if the review passes.

IT may need to approve the installed service and setup executables as well as the
installer. A hash-based approval generally needs updating for each changed binary.
An internally issued code-signing certificate is another option if IT configures the
appropriate trust and application-control rules; it is not automatically trusted on
other clients' PCs and does not guarantee SmartScreen approval.

### Security properties and signing

The service runs as `NT AUTHORITY\LocalService`; installation and the separate setup
application require administrator access. It connects outbound over HTTPS to the
configured Ekom website, with no inbound agent TCP listener. Local setup uses a
restricted named pipe. Printer/network access also depends on the configured Windows
printer queue. Application files are under `%ProgramFiles%\Ekom\Printer Agent`;
protected credentials and recovery records are under `%ProgramData%\Ekom\PrinterAgent`.
See the configuration and rendering sections below for credential protection and HTML restrictions.

Dependency review, security testing, least-privilege operation, checksums and build
provenance improve release assurance, but do not replace publisher signing or company
approval. Maintainers should review these before distributing customer builds.

For broad customer distribution, use publicly trusted code signing with timestamping
for the installer and installed executables. The current optional signing script signs
the agent, setup application and installer; the generated NSIS uninstaller is not
separately signed. Cloud/hardware-backed signing providers may require adapting the
build integration. Signing identifies the publisher and protects integrity; it does
not guarantee a clean security assessment or immediate SmartScreen reputation.
Self-signed certificates do not establish public trust. See the
[release and signing guide](../../../docs/printer-agent-releases.md).

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
./Ekom.Printer.Agent-1.0.0-win-x64-setup.exe /S
# On a clean, disposable Windows test machine only:
./build/Test-PrinterAgentInstaller.ps1 -InstallerPath ./artifacts/installers/Ekom.Printer.Agent-1.0.0-win-x64-setup.exe
```

The installer uses the fixed `%ProgramFiles%\Ekom\Printer Agent` location, registers
an Apps & Features uninstall entry and a Start menu setup shortcut, and logs to
`%ProgramData%\Ekom\PrinterAgent\installer.log`. It does not accept station credentials
as command-line arguments. Close the setup window before upgrading or uninstalling.
The smoke test verifies installation, service identity, storage permissions, setup IPC,
same-version reinstall, data preservation and uninstall. It never submits a print job.

## Connection diagnostics

Save settings before using **Test connection**. The agent uses the saved configuration.

- **HTTP 404:** verify the final website URL, install Ekom.Printer, enable
  `Ekom:Printing:Enabled`, and map its endpoints on that website.
- **HTTP 401:** check the exact station alias and original station token. The agent needs
  the token; only the website appsettings use its SHA-256 hash.
- **HTTP 403:** check website firewall, proxy and access rules.
- **HTTP 3xx:** save the final HTTPS URL (including www or an application path if needed).
  Redirects deliberately do not receive the station credential.
- **HTTP 426 / incompatible protocol:** install compatible package and agent versions.
- **HTTP 5xx:** check website logs, database connectivity and package migration startup.
- **DNS, HTTPS or timeout:** check connectivity under the Windows service account,
  certificate trust, system clock and proxy access. Do not disable certificate validation.

Agent 1.0.0 and Ekom.Printer 1.0.0 use protocol 1. Update Ekom web assets to 0.2.358
or later for the Manager review-resolution actions. A successful connection test confirms
website access; use **Test print** separately to verify physical output.

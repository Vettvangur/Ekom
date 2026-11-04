# Automatic HTML picking slips - implementation plan

## Agreed architecture

Ekom.Printer is a separate optional NuGet package inside Ekom. It owns all printing
configuration, storage, migrations, rendering, API and retention. A separately
installed Windows agent polls outbound over HTTPS and prints to an ordinary
Windows printer. No browser session is required. Core stays free of printing code;
any necessary durable status hook must be general-purpose.

## Completed foundation

- Package, configuration validation, status filtering and duplicate policies.
- Stored snapshots, replaceable renderer/factory, default encoded HTML template.
- Package-owned schema and background rendering/retention.
- Transactional status capture through a general core hook, including payment release/reset writes.
- Recovery after restart, retryable snapshot creation and replay-safe conversion to print jobs.
- Versioned HTTPS API, station authentication, atomic claims and replay-safe acknowledgements.
- Delivery/health schema upgrade, heartbeat timestamps and delivery retention.
- Agent transport and durable journal with tests for lost responses and uncertain restarts.
- Protocol version and rendering security contract.
- Separate optional Printing tab alongside Analytics in Manager for U13/U17/U18.
- Store-scoped jobs, sandboxed previews, station health, and delivery/action history.
- Authorized and audited generation retry, unclaimed-job cancellation and stored-HTML reprint.
- Package-owned action audit migration and replay-safe manual reprint requests.

## Next: reliable delivery and management

- Never automatically retry an uncertain physical print.
- Add explicit operator reconciliation for uncertain physical outcomes after checking the printer.
- Regenerating updated order content is a separate future action; reprints use stored HTML.

## Windows agent setup and health

Implemented and cross-compiled: service host, administrator setup window, service-account
printer enumeration/test command, encrypted configuration, local setup pipe, heartbeat/poll
loop, restricted Chromium pagination and GDI spool submission. Real Chromium tests pass on
macOS. NSIS installer packaging replaces WiX. Build/signing scripts and Windows CI checks
cover installation, service setup, reinstall, data preservation and uninstall.
The remaining items below require Windows execution and hardware acceptance.

- Validate installer service registration and automatic startup on Windows.
- Printer selection and test print run using the actual Windows service identity.
- Validate the Chromium/PDFium/GDI renderer under the installed unattended service account.
- Enforce no JavaScript, external requests or local-file access; support embedded assets.
- Real printer test: fonts/images, long tables, page breaks, A4 margins and copies.
- Wire the implemented heartbeat client into the Windows host; server records last-seen UTC.
- Ekom displays online/offline using OfflineAfterSeconds; expired heartbeats do not lose jobs.
- Store credentials securely, protect local HTML and remove it after acknowledged completion.

## Compatibility and release

- Surface the implemented compatibility/upgrade errors in the Windows setup UI.
- Validate migrations on SQL Server and SQLite, including simultaneous startup and upgrades.
- Package and installer CI, deployment instructions, and end-to-end Windows acceptance test.
- Release the new core transactional hook and printer package together with the correct minimum dependency version.

## Explicit decisions still needed

- Cancellation policy for queued slips after an order becomes cancelled.
- Safe HTML renderer / Windows service hosting strategy; prove hardware behaviour early.

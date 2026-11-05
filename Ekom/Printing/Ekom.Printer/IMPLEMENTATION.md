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

Implemented: Windows service, administrator setup window, service-account printer
selection/test command, encrypted configuration, protected local setup pipe,
heartbeat/poll loop, restricted Chromium pagination and GDI spool submission.

GitHub Actions on Windows has passed NSIS packaging, protected-storage initialization,
automatic service startup as LocalService, setup communication, same-version reinstall,
data preservation, uninstall, bundled renderer tests and release-file checks.
The installer helper isolates Windows PowerShell modules from inherited PowerShell 7
module paths. Early installer failures are retained in unique temporary diagnostic logs.

Hardware acceptance remains: select the actual printer, print under the service account,
and inspect embedded fonts/images, long tables, page breaks, margins and copies.
See [staging acceptance](../../../docs/printer-staging-acceptance.md).

## Compatibility and release

- GitHub Actions tests and packs Ekom.Printer on .NET 8 and .NET 10.
- NuGet trusted publishing policies have been configured by the maintainer; the first
  authenticated publication remains untested.
- Validate SQL Server migrations, simultaneous website startup and upgrades on a staging database.
- Release the core transactional hook first, then compile and publish Ekom.Printer
  against that published core version. Keep the minimum dependency version aligned.
- Publish an agent release only after reviewing its draft, signing status and acceptance evidence.
- No website or warehouse PC is currently available for the end-to-end acceptance run.

## Remaining product decisions

- Cancellation policy for queued slips after an order becomes cancelled.
- Explicit operator reconciliation for uncertain physical print outcomes. Such jobs
  remain NeedsReview and must not be automatically retried.

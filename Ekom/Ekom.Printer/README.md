# Ekom.Printer

Optional HTML picking-slip printing for Ekom. This branch currently implements the
server queue, rendering and authenticated delivery API; it is not yet an end-to-end printing release.
No printer-specific code or migration is added to Ekom.Core. Core exposes a general
`IOrderStatusPersistenceHandler` hook for transactional integrations.

## Implemented

- .NET 8 and .NET 10 package with opt-in configuration.
- Transactional status capture, store/station filtering, and multiple trigger statuses.
- Durable order snapshots and database-enforced automatic job deduplication.
- Replaceable scoped HTML renderer and snapshot factory.
- Background rendering with immutable stored HTML and visible render-failure state.
- Package-owned SQL Server / SQLite schema migration and version record.
- Retention cleanup for completed/cancelled jobs, preserving unfinished work.
- Versioned HTTPS station API with hashed bearer credentials, atomic claims and replay-safe acknowledgements.
- Package-owned delivery and station-health tables, heartbeat timestamps and offline detection.
- Agent transport and durable submission-journal library in `Ekom.Printer.Agent.Client`.
- Separate Printing tab alongside Analytics in Ekom Manager for Umbraco 13, 17 and 18.

For the first warehouse deployment, follow the [staging acceptance checklist](../../docs/printer-staging-acceptance.md).

## Registration

After registering Ekom's services:

```csharp
builder.Services.AddEkomPrinter(builder.Configuration);
builder.Services.AddScoped<IPickingSlipHtmlRenderer, MyPickingSlipRenderer>();
// Optional: capture customer addresses, shelf locations or other custom fields.
builder.Services.AddScoped<IPickingSlipContextFactory, MyPickingSlipContextFactory>();
// After building the application:
app.MapEkomPrinter();
```

Namespaces: `Ekom.Printer`, `Ekom.Printer.Rendering`, and `Ekom.Printer.Api`.

```json
{
  "Ekom": {
    "Printing": {
      "Enabled": true,
      "HtmlRetentionDays": 30,
      "HistoryRetentionDays": 90,
      "OfflineAfterSeconds": 120,
      "ClaimTimeoutSeconds": 300,
      "Stations": [
        {
          "Alias": "main-warehouse",
          "TokenSha256": "REPLACE_WITH_64_CHARACTER_SHA256_HASH",
          "StoreAliases": [ "main" ],
          "PickingSlip": {
            "Enabled": true,
            "TriggerStatuses": [ "OfflinePayment", "ReadyForDispatch" ],
            "PrintPolicy": "OncePerOrder",
            "Copies": 1
          }
        }
      ]
    }
  }
}
```

Restart the website after changing settings. Printing is disabled by default;
disabled installations do not create printer tables or register a status capture handler.
Each station requires the SHA-256 hash (hex) of a cryptographically random bearer
token of at least 32 characters. Store only the hash in server configuration and
protect the raw token on the agent computer. Never commit production credentials.
The placeholder above intentionally fails validation until replaced. Follow
[Create a station token and TokenSha256](#create-a-station-token-and-tokensha256) below.

`OncePerOrder` creates one automatic job per order/store/station across selected
statuses. `OncePerStatus` creates one per selected status. Repeated notifications
are deduplicated by the job primary key. Changing the policy changes that key and
can allow another job on a later status transition. Enabling the feature does not
backfill old orders. Saves with unchanged status are ignored. New orders initially
inserted in a configured status are eligible as well.

## Create a station token and TokenSha256

Generate one random token per station, then hash the exact token text with SHA-256.
You can generate it on your Mac even though the agent runs on Windows.

### macOS Terminal

Open Terminal and run:

```bash
token="$(openssl rand -base64 32)"
hash="$(printf '%s' "$token" | shasum -a 256 | awk '{print $1}')"

printf 'Agent station credential: %s\n' "$token"
printf 'Website TokenSha256:     %s\n' "$hash"
```

Use `printf '%s'` as shown: it hashes the token without adding a newline.

### Windows PowerShell

Works with Windows PowerShell 5.1 and PowerShell 7:

```powershell
$bytes = New-Object byte[] 32
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$rng.Dispose()

$token = [Convert]::ToBase64String($bytes)
$sha = [Security.Cryptography.SHA256]::Create()
$hash = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($token))
$sha.Dispose()
$tokenSha256 = ([BitConverter]::ToString($hash)).Replace('-', '').ToLowerInvariant()

Write-Host "Agent station credential: $token"
Write-Host "Website TokenSha256:     $tokenSha256"
```

### Where to put each value

| Output | Destination |
| --- | --- |
| Agent station credential | Windows **Ekom Printer Agent Setup > Station credential**. Use the original token, not its hash. |
| Website TokenSha256 | The matching station's `TokenSha256` under `Ekom:Printing:Stations` in website configuration. This is a 64-character hexadecimal hash. |

For example, replace the hash placeholder in your existing station configuration:

```json
"Stations": [
  {
    "Alias": "main-warehouse",
    "TokenSha256": "PASTE_THE_WEBSITE_HASH_HERE",
    "StoreAliases": [ "main" ],
    "PickingSlip": {
      "Enabled": true,
      "TriggerStatuses": [ "ReadyForDispatch" ],
      "PrintPolicy": "OncePerOrder",
      "Copies": 1
    }
  }
]
```

Use the actual store and status aliases from your website. Keep the other printing
settings from the full configuration example above, including `Enabled: true`.
Restart the website after changing its configuration. On the Windows PC, enter the
website URL and matching station alias, save the original token with automatic
printing paused, and run **Test connection**, followed by **Test print**.

These commands print the secret token in your terminal. Save it in a password manager
and transfer it securely to the warehouse administrator; do not paste it into chat,
logs, screenshots, source control or support tickets. The hash cannot recover the
original token. Running the command again creates a different token: to replace an
existing credential, update both the website hash and the agent credential, restart
the website, and test the connection again.

## HTML customization contract

Implement `IPickingSlipHtmlRenderer.RenderAsync` to return a complete HTML document.
Use print CSS (`@page`, margins, page breaks), inline CSS, embedded data-URI images
and fonts. Encode customer-provided values with `HtmlEncoder.Default.Encode`.
The default template does this. Custom renderers are trusted application code.

The agent contract prohibits JavaScript, external network requests and local-file
access. The Windows agent enforces these restrictions. The server stores HTML; it does
not sanitize or execute it. The agent uses an in-memory PDF solely for pagination
and rasterization before submitting pages to the Windows print spooler.

Use `IPickingSlipContextFactory` to add additional order/line data into the
snapshot's `Data` dictionaries. It receives an order reconstructed from the stored
status-transition snapshot, not a mutable current order. This factory runs in the
background; failures retain the capture and retry after one minute. Renderers
should use this persisted snapshot instead of fetching mutable current order data.
Renderers may execute more than once after restarts or on multiple web instances;
keep them free of side effects. The first successful HTML result wins.

## Database ownership and retention

`Migrations/PrinterMigration.cs` owns `EkomPrinterSchema`, `EkomPrinterJob`,
`EkomPrinterDelivery`, `EkomPrinterStation` and `EkomPrinterCapturedStatus` in
the Ekom database. It runs during host startup before serving requests. Schema changes are
transactional, with a SQL Server application lock; SQLite uses database write
locking with bounded retries for busy/locked schema startup. Migration version 2
adds delivery and health tables; version 3 adds transactional status capture.
Neither migration removes existing jobs. Newer unknown
schemas fail startup rather than accepting a downgrade.
Disabling/removing the package does not drop its tables.

HTML and snapshots are cleared after `HtmlRetentionDays`, measured from terminal
completion. After `HistoryRetentionDays`, identifying order/station details are
cleared. A minimal hash tombstone remains to prevent reprinting old orders.
Pending, ready, claimed, failed and needs-review jobs are retained. Manual reprints
after document expiration will require regeneration, to be explicitly confirmed.
Delivery records are removed after history retention for terminal jobs; stale
station health records are also removed. Unresolved delivery records are preserved.
Raw captured order data is deleted once its print-job snapshot has been created.
Failed/unprocessed captures remain available for recovery rather than being expired.

## Durable order-status capture

When enabled, the package registers `IOrderStatusPersistenceHandler`. Ekom invokes
this general hook inside the order-write transaction, including the direct payment
release/reset path. The package writes only a small local capture record there;
custom factories, HTML rendering and network calls never run inside that transaction.
Suppressing the older notification events does not suppress durable capture.

The order change and capture either both commit or both roll back. A capture database
failure therefore fails the order save rather than silently losing the slip. Printer
availability and rendering failures cannot fail the order save. After a restart,
the worker builds jobs from pending captures using the original order and configured
station/policy/copy values. Enqueue is idempotent even if the process stops before
deleting the capture record. Templates may run again and must have no side effects.

Only writes made through Ekom's supported persistence paths are covered. Direct SQL
updates by client integrations bypass this hook. Installations must restart after
changing printing configuration, and all website instances must use matching settings.
Disabling printing pauses existing pending work; it does not delete it.

## Station API, version 1

Call `app.MapEkomPrinter()` explicitly. All requests require HTTPS,
`Authorization: Bearer <station-token>` and `X-Ekom-Printer-Protocol: 1`.
Configure trusted forwarded headers in the host when a reverse proxy terminates
TLS; this package does not trust arbitrary forwarding headers itself. CMS login
cookies do not grant access. Responses use `Cache-Control: no-store`.

Base path: `/ekom/printer/v1/stations/{stationAlias}`.

| Method | Path | Result |
| --- | --- | --- |
| POST | `/claim` | A claimed job with HTML, copies, claim token and expiry; 204 when empty |
| POST | `/jobs/{jobId}/acknowledge` | Submit `claimToken`, `submitted`, `spoolJobId`; 204 on success or identical replay |
| POST | `/heartbeat` | Submit `protocolVersion`, `agentVersion`, `printerName` |
| GET | `/health` | This station's last-seen UTC, online/compatible flags, agent version and printer name |

Wrong credentials or an unknown station return 401; incompatible protocol returns
426 before claiming a job. Invalid acknowledgements return 400; wrong claim tokens,
cross-station jobs and conflicting replay return 409 without changing the job.
Submitted acknowledgements require the actual Windows spool identifier.

Claims are never automatically returned to the ready queue. After
`ClaimTimeoutSeconds` they become `NeedsReview`, because the physical print outcome
may be unknown. The original matching acknowledgement can still reconcile an
expired claim. Reporting a failure/uncertain result also goes to `NeedsReview`.
The timeout sweep runs in the background every five seconds.

The agent health API is station-scoped; Manager displays stations for the user's allowed stores.
Last-seen time is supplied by the server, not the agent clock.
"Online" means a recent valid heartbeat, not confirmation that the printer has paper.

## Ekom Manager

The optional **Printing** tab appears when the package is installed, mapped and enabled.
Deploy the matching Ekom web assets. It lists jobs with state, station and order-number
filters, sandboxed HTML previews, delivery/action history, and station health with last-seen
time. It respects existing Manager access and store restrictions. Configuration is read-only;
continue editing appsettings and selecting the printer through the Windows agent setup.

Administrators can retry failed HTML generation, cancel unclaimed jobs, and reprint
submitted jobs while their HTML is retained. To grant these actions to other backoffice
groups, configure `Ekom:Printing:ManagerGroups` with their group aliases. The default
empty list grants no additional groups. Other permitted Manager users can view jobs.
Actions require the host's backoffice authorization policy and an anti-forgery token.

Reprint creates a new job containing the exact stored HTML and copies setting. Each action
records the operator, time and request ID in the package-owned audit table (schema v4).
Retrying the same request ID returns the same outcome. Audit records follow
`HistoryRetentionDays`; expired HTML is unavailable for preview or reprint.
Claimed or uncertain jobs cannot be cancelled or reprinted here: check the printer first.
An explicit operator reconciliation workflow for uncertain outcomes remains future work.

"Submitted to Windows" records acceptance by the spooler, not physical printing.
Browser previews disable scripts and external resources and remove active HTML and
navigation. The Windows renderer must independently enforce the same security contract.

## Agent library

`Ekom.Printer.Agent.Client` contains the HTTPS client and submission processor. It
shares the protocol source without referencing Ekom.Core or database libraries.
Use `PrinterAgentClient.CreateHttpClient()` to disable redirects; injected HTTP
clients must also disable redirects. The caller owns/disposes the HTTP client.

The processor journals intent before invoking `IHtmlPrinter`, persists the spool
result before acknowledging, and only removes its journal entry after an accepted
acknowledgement. Lost responses retry reporting, never physical printing. After an
uncertain restart it reports `NeedsReview`. Corrupt journals stop processing.
Consumers sharing a journal directory are serialized using an exclusive file lock.
Protect this directory with service-account-only permissions; it contains claim
tokens but never HTML. The Windows host must use a stable directory per station.

The library is now used by the Windows service and administrator setup projects in
`Ekom.Printer.Agent` and `Ekom.Printer.Agent.Setup`. The implementation uses restricted
Chromium rendering and Windows GDI submission with real spool IDs. Setup lists printers
and sends test prints through the service account. See [agent instructions](../Ekom.Printer.Agent/README.md).
NSIS installer packaging is implemented. Windows installation and hardware acceptance
remain pending; this is not yet a production-tested installer release.

## Remaining work before production

See [IMPLEMENTATION.md](IMPLEMENTATION.md) for the agreed implementation plan.
This package requires the core version containing `IOrderStatusPersistenceHandler`.
During development, build against the core project in this branch. Publish a new
matching core/package release together with an updated minimum core dependency;
do not deploy this build with an older published core package.

SQL Server migrations need integration testing against a real server. SQLite
migrations, deduplication, rendering and retention have automated tests.

## Development

```sh
dotnet test Ekom/Tests/Ekom.Printer.Tests/Ekom.Printer.Tests.csproj
dotnet pack Ekom/Ekom.Printer/Ekom.Printer.csproj
```

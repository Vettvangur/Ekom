# Ekom.Printer

Ekom.Printer automatically prints picking slips on a warehouse printer. The website
captures the order and prepares an HTML slip; the Windows agent sends it to the
selected printer.

You can print when an order reaches a selected status, or let your integration request
printing after warehouse processing is complete. Each station uses one of these modes.
You can also customize the slip's content and layout.

## Set up the website

Install the package in your website project:

```sh
dotnet add package Ekom.Printer
```

Explicit capture requires Ekom.Printer 1.1.0 or later. Use the matching Ekom web-assets
release for your Umbraco version to display the Printing tab in Ekom Manager.
Windows Agent 1.0.0 supports both trigger modes.

In `Program.cs`, register printing after Ekom's services and map its endpoints after
building the application:

```csharp
using Ekom.Printer;
using Ekom.Printer.Api;

builder.Services.AddEkomPrinter(builder.Configuration);

var app = builder.Build();
app.MapEkomPrinter();
```

Keep your existing application setup and middleware. The default picking-slip template
works without registering a custom renderer.

## Configure appsettings.json

This example enables printing on order-status changes, using the default
`StatusChange` trigger mode:

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
            "TriggerMode": "StatusChange",
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

This station prints when an order reaches `OfflinePayment` or `ReadyForDispatch`.
With `OncePerOrder`, reaching both statuses still creates only one slip.

To let your integration request printing after warehouse processing instead, replace
`PickingSlip` with:

```json
"PickingSlip": {
  "Enabled": true,
  "TriggerMode": "ExplicitCapture",
  "TriggerStatuses": [],
  "PrintPolicy": "OncePerOrder",
  "Copies": 1
}
```

In this mode, your integration calls `IPickingSlipCapture` after saving warehouse
metadata; order-status changes do not create slips. See
[Explicit capture after warehouse processing](#explicit-capture-after-warehouse-processing)
for the API call, retry behavior and switching existing stations safely.

Choose one mode per station. `StatusChange` remains the default when `TriggerMode` is
omitted. `ExplicitCapture` requires an empty `TriggerStatuses` list and `OncePerOrder`.

Replace `main` with your store alias and `main-warehouse` with the station name you
will enter in the Windows agent. Generate a token using the instructions below and
replace `REPLACE_WITH_64_CHARACTER_SHA256_HASH` with its hash.

Restart the website after changing configuration. Printing is disabled unless
`Ekom:Printing:Enabled` is `true`. All instances of the website must use the same settings.

In status-change mode, `OncePerOrder` prints once across the selected statuses;
`OncePerStatus` allows one slip for each selected status. Repeated notifications do not
create extra jobs. Existing orders are not printed automatically when you enable the
feature; a subsequent qualifying status change is needed. New orders created in a
selected status are eligible too.

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

Keep the original token in a password manager and share it securely with the person
setting up the agent. These commands display it in your terminal. The hash cannot be
used to recover the token.

To rotate a credential, generate a new token, replace the website hash and the agent's
saved credential, restart the website, and test the connection again.

## Connect the Windows agent

Install the [Windows Printer Agent](https://github.com/Vettvangur/Ekom/releases?q=printer-agent-v)
on the warehouse computer. The printer driver must already be installed.

1. Open **Ekom Printer Agent Setup** as administrator.
2. Enter the website's HTTPS URL, the matching station alias and the original token.
3. Select the printer and save with automatic printing paused.
4. Run **Test connection**, then **Test print**, and check the printed page.
5. Enable automatic printing and save again.

The test page uses the Windows service account, whose printer access can differ from
the logged-in user's. See the [agent setup guide](https://github.com/Vettvangur/Ekom/blob/Ekom/Ekom/Printing/Ekom.Printer.Agent/README.md) for
installation, upgrades and connection troubleshooting.

## Explicit capture after warehouse processing

Use `ExplicitCapture` when your integration decides when the order is ready to print.
Order-status changes do not create slips for that station. `AddEkomPrinter` registers
`IPickingSlipCapture` for dependency injection:

```csharp
using Ekom.Models;
using Ekom.Printer.Services;

public sealed class WarehousePrinting(IPickingSlipCapture capture)
{
    public Task<PickingSlipCaptureResult> CaptureSavedOrderAsync(
        OrderData savedOrder, CancellationToken cancellationToken = default)
        => capture.CaptureAsync(savedOrder, "main-warehouse", cancellationToken);
}
```

Call it after saving and committing the warehouse metadata, before marking your
integration's dispatch work complete. Pass the saved `OrderData`, including its updated
`OrderInfo` JSON. Values stored elsewhere must first be copied into the saved order,
for example into `CustomData`.

A successful call means the order has been saved for printing. It does not mean the
slip has finished rendering or physically printed. The result contains `JobId` and
`Created`; `Created` is `false` when a capture or job already exists for that order,
store and station.

Keep dispatch work available for retry until capture succeeds. If capture fails, retry
it. If capture succeeds but completing dispatch work fails, retry with the same order
and station. These retries do not create another slip. The first saved snapshot is
kept, so retrying does not replace it with newer metadata.

Capture fails if printing is disabled, the station uses status-change mode, or the
order's store is not assigned to that station. Database errors and cancellation also
reach the caller; do not mark dispatch complete after a failed capture.

### Switching an existing station

Pause the integration, set `TriggerMode` to `ExplicitCapture`, clear `TriggerStatuses`,
and use `OncePerOrder`. Review existing captures and jobs before resuming: changing
settings does not cancel work already queued.

An earlier `OncePerOrder` slip prevents a new capture for the same order and station,
and its metadata is not replaced. Earlier `OncePerStatus` slips can still print alongside
an explicit capture. Resolve those orders before switching. Use the Manager's review
and reprint actions when another copy is needed.

## Customize the picking slip

Implement `IPickingSlipHtmlRenderer` to return a complete HTML document, then register
it after `AddEkomPrinter`:

```csharp
using Ekom.Printer.Rendering;

builder.Services.AddScoped<IPickingSlipHtmlRenderer, MyPickingSlipRenderer>();
```

Use print CSS for page size, margins and page breaks. Embed images and fonts in the
HTML using data URIs. Encode customer-provided text with `HtmlEncoder.Default.Encode`.
The agent blocks JavaScript, external network requests and local-file access.

To include addresses, shelf locations or warehouse references, implement
`IPickingSlipContextFactory` and register it in the same way:

```csharp
builder.Services.AddScoped<IPickingSlipContextFactory, MyPickingSlipContextFactory>();
```

The factory receives the saved order snapshot. Copy custom values into
`PickingSlipContext.Data` or a line's `Data` dictionary, then read them in the renderer.
Do not fetch the latest order while rendering: the slip should reflect the data captured
when printing was requested.

Factories and renderers run in the background and may run again after a restart. Keep
them free of side effects such as updating orders or sending messages. Factory failures
are retried; rendering failures appear in the Manager for review and retry.

## Manage printing in Ekom Manager

The **Printing** tab shows jobs, previews, history and station connections. You can
filter by order, station or status. Users see only the stores they are allowed to access.
Change station settings in appsettings and select the physical printer in the Windows
agent.

Administrators can retry failed slip generation, cancel jobs that have not been claimed,
and reprint slips while their stored HTML is available. To grant these actions to other
backoffice groups, add their aliases to `Ekom:Printing:ManagerGroups`.

**Submitted to printer** means the Windows print queue accepted the job. It does not
confirm that paper was printed. **Online** means the agent contacted the website recently;
it does not confirm that the printer has paper or is working.

For **Needs review** jobs, check the physical output and the Windows queue before choosing:

- **Mark as printed:** record that you confirmed the output, without printing again.
- **Print again after checking:** close the uncertain job and queue a replacement using
  its saved HTML and copy count. Clear any pending original from the Windows queue first.

Uncertain jobs are not automatically printed again. Review and reprint actions record
who performed them and when. A normal reprint uses the saved slip, not newer order data.

## Storage and retention

The package creates and updates its own tables in the Ekom database when the website
starts with printing enabled. No manual table creation is needed. Disabling or removing
the package does not delete those tables.

`HtmlRetentionDays` controls how long completed slips and their snapshots remain
available. `HistoryRetentionDays` controls how long their identifying history is kept
and must be at least as long as HTML retention. These periods start when a job completes
or is cancelled. After the HTML expires, preview and reprint are unavailable.

Queued, failed and unresolved work is retained for recovery. A minimal record remains
after history cleanup to prevent an old order from printing again.

## Troubleshooting

- **The agent cannot connect:** check the HTTPS URL, station alias and original token.
  Make sure printing is enabled and `app.MapEkomPrinter()` is registered. The agent's
  **Test connection** message explains common configuration and network errors.
- **No slip appears in explicit mode:** confirm your integration calls `CaptureAsync`
  after saving warehouse data. Changing order status alone does not trigger this mode.
- **No slip appears in status-change mode:** check the station's store and trigger
  statuses. Direct SQL updates bypass Ekom's status handling.
- **A slip has old warehouse data:** the first captured snapshot is preserved. Check
  whether an earlier status trigger captured the order before your warehouse work finished.
- **Printer tables are missing:** upgrade to the current package, enable printing and
  restart the website. Check startup logs and database permissions if creation fails.
  Do not drop printer tables or manually change migration records to force a retry.

For custom agent integrations, see the [printer agent protocol reference](https://github.com/Vettvangur/Ekom/blob/Ekom/docs/printer-agent-protocol.md).

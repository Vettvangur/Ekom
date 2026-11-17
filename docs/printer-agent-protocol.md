[Ekom docs](README.md) / [Manager](manager/README.md) / Printer agent protocol

# Printer agent protocol

Ekom.Printer creates picking slips as HTML, and a printer agent collects them from the website and prints them. This page describes the HTTP API between the website and an agent, version 1, and the .NET client library you can build on. Use it when you write your own agent instead of using the supplied Windows agent.

**Who this page is for:** developers who build a custom printer agent. To set up printing with the supplied Windows agent, follow the [Ekom.Printer README](../Ekom/Printing/Ekom.Printer/README.md) and the [Windows agent guide](../Ekom/Printing/Ekom.Printer.Agent/README.md) instead.

On this page:

- [Before you start](#before-you-start)
- [Authenticate every request](#authenticate-every-request)
- [Endpoints](#endpoints)
- [Claim, print and acknowledge a job](#claim-print-and-acknowledge-a-job)
- [Error responses](#error-responses)
- [Use the .NET client library](#use-the-net-client-library)

## Before you start

The website must have Ekom.Printer installed, `Ekom:Printing:Enabled` set to `true`, and a station configured for your agent under `Ekom:Printing:Stations`. A **station** is one agent with its own alias and token. See the [Ekom.Printer README](../Ekom/Printing/Ekom.Printer/README.md) for the settings and for how to create a token and its `TokenSha256` hash.

The endpoints exist only when the site calls `app.MapEkomPrinter()` after building the application. When printing is turned off, every station endpoint returns `404 Not Found`.

## Authenticate every request

Every request must:

- use HTTPS. A plain HTTP request returns `400 Bad Request`. If a reverse proxy ends TLS in front of the site, configure trusted forwarded headers in the host; Ekom.Printer does not trust forwarding headers on its own.
- send `Authorization: Bearer <station-token>`, where the token is the original token (32 to 512 characters), not its hash. Ekom hashes it with SHA-256 and compares the result with the station's `TokenSha256`.
- send `X-Ekom-Printer-Protocol: 1`.

A backoffice login cookie does not give access to these endpoints. Station aliases are compared without regard to upper and lower case. Every response has `Cache-Control: no-store`.

## Endpoints

The base path is `/ekom/printer/v1/stations/{stationAlias}`.

| Method | Path | Request body | Response |
| --- | --- | --- | --- |
| POST | `/claim` | None | `200` with a claimed job, or `204 No Content` when no job is waiting. |
| POST | `/jobs/{jobId}/acknowledge` | `claimToken`, `submitted`, `spoolJobId` | `204` when accepted, or when the same acknowledgement is sent again. |
| POST | `/heartbeat` | `protocolVersion`, `agentVersion`, `printerName` | `200` with `compatible`, `requiredProtocolVersion` and `message`. |
| GET | `/health` | None | This station's `stationAlias`, `lastSeenUtc`, `online`, `compatible`, `agentVersion` and `printerName`. |

A claimed job has these fields:

| Field | Meaning |
| --- | --- |
| `jobId` | 64 hexadecimal characters. |
| `claimToken` | A GUID that proves this claim. Send it back with the acknowledgement. |
| `expiresUtc` | When the claim runs out. Do not start printing after this time. |
| `html` | The complete picking-slip document. |
| `copies` | Number of copies, 1 to 100. |
| `policy` | The rendering rules your agent must enforce: JavaScript, external network requests and local file access are all turned off. |

Send a heartbeat regularly. `agentVersion` (up to 100 characters) and `printerName` (up to 255) are required. The server records the time it received the heartbeat, not the agent's clock. A station counts as **online** when its last valid heartbeat is within `Ekom:Printing:OfflineAfterSeconds` (default 120). Online means the agent is reachable. It does not mean the printer has paper or works.

The Manager's **Printing** view shows the health of the stations of the stores a user has access to.

## Claim, print and acknowledge a job

1. Call `/claim`. On `204`, wait and try again later.
2. Save the `jobId` and `claimToken` somewhere that survives a crash, before you print anything.
3. If `expiresUtc` has passed, do not print. Acknowledge with `submitted: false`.
4. Render the HTML with the `policy` rules and send it to the printer.
5. Save the printer's result.
6. Call `/jobs/{jobId}/acknowledge`:
   - after the print queue accepted the job: `submitted: true` and `spoolJobId` set to the real Windows spool job ID (up to 100 characters),
   - when you cannot tell what happened: `submitted: false`.
7. Remove your saved record only after the acknowledgement is accepted.

If the acknowledgement's response is lost, send the same acknowledgement again. Never print again to recover from a lost response.

**What the website does with the result:**

- `submitted: true` marks the job **Submitted to printer**. This means Windows accepted the job, not that paper came out.
- `submitted: false` moves the job to **Needs review**, so a person checks the printer.
- A claim is never put back in the queue on its own. When `Ekom:Printing:ClaimTimeoutSeconds` (default 300, allowed 30 to 3600) passes without an acknowledgement, the job moves to **Needs review**, because nobody knows whether it printed. Ekom checks for expired claims every 5 seconds.
- The original, matching acknowledgement is still accepted for a job that moved to **Needs review** after its claim expired.

Staff then resolve **Needs review** jobs in the Manager with **Mark as printed** or **Print again after checking**. See [Track picking-slip printing](manager/overview.md#track-picking-slip-printing).

## Error responses

| Response | When |
| --- | --- |
| `400 Bad Request` | The request is not HTTPS, the protocol header is missing or not a number, the acknowledgement is invalid (bad job ID, empty claim token, `spoolJobId` longer than 100 characters, or `submitted: true` without `spoolJobId`), or the heartbeat lacks an agent version or printer name. |
| `401 Unauthorized` | The station alias is unknown or the token is wrong. |
| `404 Not Found` | Printing is turned off on the website, or the endpoints are not mapped. |
| `409 Conflict` | The claim token is wrong, the job belongs to another station, or the acknowledgement differs from one already accepted. The job is not changed. |
| `426 Upgrade Required` | The protocol version is not 1. Returned before any job is claimed, with a body that says which version is required. |

## Use the .NET client library

`Ekom.Printer.Agent.Client` holds an HTTPS client for the station API and a processor that follows the steps above. It shares the protocol types with the website package, and does not reference Ekom.Core or any database library.

```csharp
using Ekom.Printer.Agent.Client;

using HttpClient http = PrinterAgentClient.CreateHttpClient();

var client = new PrinterAgentClient(
    http,
    new Uri("https://shop.example.com"),
    "main-warehouse",
    stationToken);

await client.HeartbeatAsync("1.0.0", "Zebra ZT230", cancellationToken);

var journal = new SubmissionJournal(@"C:\ProgramData\AcmeAgent\main-warehouse");
var processor = new AgentPrintProcessor(client, journal, htmlPrinter, TimeProvider.System);

bool handledJob = await processor.ProcessNextAsync(cancellationToken);
```

`stationToken` is the original station token, and `htmlPrinter` is your implementation of `IHtmlPrinter`.

- **`PrinterAgentClient`** requires an absolute HTTPS URL without user info, query or fragment, and a token of 32 to 512 characters. It throws `ArgumentException` otherwise. It throws `InvalidOperationException` on `426` and `HttpRequestException` on any other error status.
- **`PrinterAgentClient.CreateHttpClient()`** returns an `HttpClient` that does not follow redirects and checks certificate revocation. If you pass your own `HttpClient`, turn off redirects on it too, so the token is never sent to another address. You own the client and must dispose it.
- **`IHtmlPrinter.SubmitAsync(html, copies, policy, ct)`** must enforce the `policy` rules and return the real spool job ID.
- **`AgentPrintProcessor.ProcessNextAsync`** first reports any outcomes left in the journal from an earlier run, then claims one job. It returns `false` when no job is waiting. It writes the job to the journal before it calls your printer, saves the spool result before it acknowledges, and removes the journal entry only after the acknowledgement is accepted. A lost response is retried as a report, never as a new print. After a crash in the middle of a print, it reports the job as uncertain, so it goes to **Needs review**.
- **`SubmissionJournal`** stops processing with an `InvalidOperationException` when a journal file is unreadable or does not match its job. Several processors that share a journal directory take turns, using an exclusive lock file. Use one stable directory per station, and protect it so only the agent's service account can read it: it holds claim tokens, but never the HTML.

## Related pages

- [Ekom.Printer README](../Ekom/Printing/Ekom.Printer/README.md): website setup, stations, tokens and retention.
- [Windows agent guide](../Ekom/Printing/Ekom.Printer.Agent/README.md): the supplied agent, for comparison.
- [Manager overview](manager/overview.md#track-picking-slip-printing): how staff follow and resolve print jobs.
- [Printer agent releases](printer-agent-releases.md): how the Windows agent is built and verified.

[Back to Manager](manager/README.md)

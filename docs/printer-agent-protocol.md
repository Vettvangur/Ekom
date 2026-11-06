# Printer agent protocol reference

This reference is for developers building a custom printer agent. For the supplied
Windows agent, use the [setup guide](../Ekom/Printing/Ekom.Printer.Agent/README.md).

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


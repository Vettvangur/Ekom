# Printing in the Umbraco samples

Both U17 and U18 reference Ekom.Printer, register its services and map its endpoints. Printing is enabled in each site's appsettings.json.

The configured `local-verification` station serves the sample `Store` store, uses one copy, and uses ExplicitCapture with OncePerOrder. This exposes the Printing screen without automatically queuing slips on order-status changes.

The all-zero TokenSha256 is an unusable placeholder, not a usable agent credential. Before connecting an agent, generate a random station token and put its SHA-256 hash in configuration (prefer user secrets or environment variables). Give the original token to the agent. Follow [the printer setup guide](../Ekom/Printing/Ekom.Printer/README.md). Configure the real station name, store aliases and printer as needed.

The screen correctly reports Offline / Agent not connected until a compatible agent sends heartbeats. Capturing and rendering a slip does not prove physical printing. Check real paper output separately before using operational printing.

## Local verification performed

Authenticated U17 and U18 instances were run against isolated SQLite copies, with test jobs only in those copies. Fixtures were labelled LOCAL VERIFICATION and used a synthetic item. The website's real background worker generated HTML, and a malformed snapshot exercised render failure. No physical printer or external shipment provider was used.

Live checks covered section visibility/navigation, queue and station cards, generated preview, failed-generation retry, cancellation, uncertain-job replacement, action history, attention/search filters and empty results. Styled confirmation cancellation preserves the job. Light/dark modes follow Umbraco settings. State-transition and store-access protections also have automated printer tests.

The uncertain-job fixture simulated an unconfirmed delivery; it was not evidence of physical printing. Test fixtures are not installed by the sample application or committed into its databases.

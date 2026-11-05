# Printer staging acceptance

Use this procedure once a staging Ekom website and warehouse Windows PC are available.
CI has validated the installer lifecycle and package tests. It has not validated the
customer's printer, service-account printer access, or an end-to-end warehouse order.

## Prepare the website

1. Deploy the matching core, web assets and Ekom.Printer build from the reviewed feature.
   The package requires the new core transactional status hook. A PR package artifact
   must not be paired with an older public core package lacking that hook.
2. Register `builder.Services.AddEkomPrinter(builder.Configuration)` after Ekom services,
   then `app.MapEkomPrinter()` after building the application. See the
   [package configuration example](../Ekom/Printing/Ekom.Printer/README.md#registration).
3. Enable printing for one test station and test store, with a single trigger status
   using the exact status alias configured on that website. Start with one copy and
   `OncePerOrder`. Use synthetic customer/order information.
4. Generate a cryptographically random station token of at least 32 characters using the
   [macOS or Windows commands](../Ekom/Printing/Ekom.Printer/README.md#create-a-station-token-and-tokensha256)
   or your secret-management process. Store its SHA-256 hexadecimal hash in the website's
   `TokenSha256` setting; deliver the original token securely to the agent administrator.
   Do not put either value in a PR, test report or chat.
5. Restart the website. Confirm package-owned migrations complete and the Printing tab
   appears for an authorized Manager user. For SQL Server, also test an upgrade and
   simultaneous website startup against a disposable database copy.

## Connect and print

1. Install the reviewed Windows agent installer as administrator. Record its version
   and whether it is signed. CI artifacts are unsigned acceptance builds.
2. Open Ekom Printer Agent Setup and enter the HTTPS website URL, station alias and
   original token. Select a printer from the service's list and save with printing paused.
3. Run Test connection and confirm the station's last-seen timestamp in Manager.
4. Run Test print and physically inspect the paper. This runs under LocalService,
   not the logged-in user. A printer missing from the list needs a machine-wide queue
   accessible to that service account; the initial installer does not manage alternate accounts.
5. Enable automatic printing and save. Move a test order into the selected status.
   Check the order identifier, lines, quantities, page layout and copy count on paper.
   A submitted/spooled status alone does not prove that paper was printed successfully.

## Acceptance cases

| Case | Expected result |
| --- | --- |
| Non-trigger status | No new print job. |
| First selected status | One job and the configured number of copies. |
| Save without changing status | No duplicate job. |
| Second selected status with OncePerOrder | No additional automatic job for the same order/store/station. |
| Two selected statuses with OncePerStatus on a new test order | One job for each selected status. |
| Agent stopped before a new order transition | Job waits and is claimed after the agent restarts. |
| Website temporarily unreachable | Agent reconnects; an uncertain submission is not blindly reprinted. |
| Uncertain physical outcome | Job remains NeedsReview; inspect printer and logs before any manual action. Explicit reconciliation UI is not implemented yet. |
| Custom HTML | Embedded fonts/images, long tables, page breaks, margins and multiple copies print correctly. External resources and JavaScript remain blocked. |
| Reinstall | Station configuration and submission journal are preserved. |
| Uninstall | Service and application files are removed; protected recovery data remains. |

Use a new test order when comparing duplicate policies. Changing policies on an existing
order can create a new job identity and an extra print. Do not delete recovery journals
to force a retry or kill a live warehouse print job to simulate failure.

Record website/core/package/agent versions, database type, Windows version, printer
model/driver, service identity, case results and any redacted errors. Keep customer
information and credentials out of the report.

## Release order

1. Review and merge the feature PR after deciding whether remaining acceptance gaps
   are acceptable for a prerelease.
2. Release Ekom.Core containing the status hook through the existing Ekom release process.
3. Set the printer's core dependency to that published version and run its release build.
   Tag `Ekom.Printer-v<version>` to publish through GitHub Actions and NuGet Trusted Publishing.
4. Tag `printer-agent-v<version>` to build a draft agent release. Review the assets,
   signing status and this acceptance evidence before publishing it.

See [release instructions](printer-agent-releases.md). Creating a trusted publisher
policy does not publish packages or prove that the first OIDC exchange succeeds.

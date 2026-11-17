[Ekom docs](README.md) / [Manager](manager/README.md) / Printer staging acceptance

# Printer staging acceptance

This is the test procedure for a new release of picking-slip printing, run on a staging website and a warehouse Windows PC before the release is published. It covers what the automated builds cannot: the real printer, the service account's access to it, and a full warehouse order.

**Who this page is for:** Ekom maintainers who test and release Ekom.Printer and the Windows agent. To set up printing on your own site, follow the [Ekom.Printer README](../Ekom/Printing/Ekom.Printer/README.md) instead.

The automated builds test the installer's install, reinstall and uninstall steps and the package's own tests. They do not test a customer's printer, the service account's printer access, or an order printed end to end in a warehouse.

On this page:

- [Prepare the staging website](#prepare-the-staging-website)
- [Connect the agent and print](#connect-the-agent-and-print)
- [Run the acceptance cases](#run-the-acceptance-cases)
- [Record the results](#record-the-results)
- [Release in this order](#release-in-this-order)

## Prepare the staging website

1. Deploy the matching Ekom core, web-assets and Ekom.Printer builds from the reviewed change. Ekom.Printer needs a core version with the transactional status hook. Do not pair a pull request package artifact with an older public core package that lacks it.
2. Register printing after Ekom's services with `builder.Services.AddEkomPrinter(builder.Configuration)`, and map its endpoints with `app.MapEkomPrinter()` after building the application. See [Set up the website](../Ekom/Printing/Ekom.Printer/README.md#set-up-the-website).
3. Turn printing on for one test station and one test store. Use the `StatusChange` trigger mode with one trigger status, written exactly as the Ekom order status name, for example `ReadyForDispatch`. Start with 1 copy and `OncePerOrder`. Use made-up customer and order data.
4. Create a random station token of at least 32 characters with the [macOS or Windows commands](../Ekom/Printing/Ekom.Printer/README.md#create-a-station-token-and-tokensha256), or your secret-management process. Put its SHA-256 hash in the station's `TokenSha256` setting, and give the original token to the person who sets up the agent through a secure channel. Never put either value in a pull request, test report or chat.
5. Restart the website. Check that the printer package creates its database tables and that the **Printing** view appears in Ekom Manager for an authorized user. On SQL Server, also test an upgrade, and two website instances starting at the same time, against a throwaway copy of the database.

## Connect the agent and print

1. Install the reviewed Windows agent installer as administrator. Write down its version and whether it is signed. Installers from pull request and branch builds are unsigned acceptance builds.
2. Open **Ekom Printer Agent Setup**. Enter the HTTPS website URL, the station alias and the original token. Choose a printer from the service's list and save with automatic printing paused.
3. Run **Test connection**, and check the station's **Last connected** time in the Manager's **Printing** view.
4. Run **Test print** and look at the paper. The test runs as the Windows service account (LocalService), not the signed-in user. If a printer is missing from the list, it needs a machine-wide print queue that this account can use. The installer does not support other service accounts.
5. Turn on automatic printing and save. Move a test order into the trigger status. On the paper, check the order number, lines, quantities, page layout and number of copies. **Submitted to printer** alone does not prove that the paper printed correctly.

## Run the acceptance cases

| Case | Expected result |
| --- | --- |
| The order moves to a status that is not a trigger | No new print job. |
| The order moves to the first trigger status | One job, with the configured number of copies. |
| The order is saved without a status change | No second job. |
| A second trigger status with `OncePerOrder` | No new automatic job for the same order, store and station. |
| Two trigger statuses with `OncePerStatus`, on a new test order | One job for each trigger status. |
| The agent is stopped before a new order reaches the trigger status | The job waits, and is collected when the agent starts again. |
| The website cannot be reached for a while | The agent reconnects. A submission with an unknown outcome is not printed again on its own. |
| The physical outcome is unknown | The job shows **Needs review**. Check the printer and logs, then resolve it with **Mark as printed** or **Print again after checking** in the Printing view. |
| A custom picking-slip template | Embedded fonts and images, long tables, page breaks, margins and several copies print correctly. External resources and JavaScript stay blocked. |
| The agent is reinstalled | The station settings and the submission journal are kept. |
| The agent is uninstalled | The service and program files are removed. The protected recovery data stays. |

Use a new test order each time you compare duplicate policies. Changing the policy for an existing order can create a new job identity and an extra print. Do not delete recovery journals to force a retry, and do not kill a real warehouse print job to simulate a failure.

## Record the results

Write down the website, core, package and agent versions, the database type, the Windows version, the printer model and driver, the service account, the result of each case, and any errors with personal data removed. Keep customer data and credentials out of the report.

## Release in this order

1. Review and merge the change. Decide whether any acceptance gaps that remain are acceptable for a prerelease.
2. If the printer needs a newer Ekom.Core, release that core version first through the normal Ekom release.
3. Set the printer's `EkomPackageVersion` to that published core version. Merge the Release Please pull request: it creates the `Ekom.Printer-v<version>` tag, which builds and publishes the package through GitHub Actions and NuGet Trusted Publishing.
4. Tag `printer-agent-v<version>` to build a draft agent release. Check its files, its signing status and this acceptance evidence before you publish it.

See [Printer agent releases](printer-agent-releases.md) for the details. Creating the NuGet Trusted Publishing policy does not publish a package, and does not prove that the first sign-in from the workflow works.

## Related pages

- [Printer agent releases](printer-agent-releases.md): build, sign and publish the agent and the package.
- [Ekom.Printer README](../Ekom/Printing/Ekom.Printer/README.md): website settings, trigger modes and troubleshooting.
- [Windows agent guide](../Ekom/Printing/Ekom.Printer.Agent/README.md): agent setup, updates and diagnostics.
- [Manager overview](manager/overview.md#track-picking-slip-printing): the Printing view used in these tests.

[Back to Manager](manager/README.md)

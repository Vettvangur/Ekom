using Microsoft.Playwright;
using System.Globalization;

namespace Ekom.Printer.Agent;

internal static class HtmlDocumentRenderer
{
    internal static async Task<byte[]> RenderAsync(string html, double widthInches, double heightInches, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(60));
        using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true, ChromiumSandbox = true, Timeout = 30000,
        }).ConfigureAwait(false);
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            JavaScriptEnabled = false, ServiceWorkers = ServiceWorkerPolicy.Block, AcceptDownloads = false,
        }).ConfigureAwait(false);
        const string documentUrl = "https://ekom-print.invalid/slip";
        var served = false;
        await context.RouteAsync("**/*", async route =>
        {
            if (!served && route.Request.Url == documentUrl)
            {
                served = true;
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    ContentType = "text/html; charset=utf-8", Body = html,
                    Headers = new Dictionary<string, string>
                    {
                        ["Content-Security-Policy"] = "default-src 'none'; script-src 'none'; style-src 'unsafe-inline'; img-src data:; font-src data:; frame-src 'none'; object-src 'none'; connect-src 'none'; base-uri 'none'; form-action 'none'",
                    },
                }).ConfigureAwait(false);
            }
            else await route.AbortAsync().ConfigureAwait(false);
        }).ConfigureAwait(false);
        var page = await context.NewPageAsync().ConfigureAwait(false);
        page.SetDefaultTimeout(30000);
        await page.GotoAsync(documentUrl, new PageGotoOptions { WaitUntil = WaitUntilState.Load }).WaitAsync(budget.Token).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        // The server still supplies HTML. PDF is an in-memory pagination intermediate only.
        var pdf = await page.PdfAsync(new PagePdfOptions
        {
            Width = (widthInches).ToString(CultureInfo.InvariantCulture) + "in",
            Height = (heightInches).ToString(CultureInfo.InvariantCulture) + "in",
            PreferCSSPageSize = true, PrintBackground = true,
        }).WaitAsync(budget.Token).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return pdf;
    }
}

using Ekom.Printer.Agent.Client;
using Ekom.Printer.Protocol;
using System.Drawing.Printing;

namespace Ekom.Printer.Agent;

internal sealed class WindowsHtmlPrinter(string printerName) : IHtmlPrinter
{
    public async Task<string> SubmitAsync(string html, int copies, HtmlPrintPolicy policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (copies is < 1 or > 100 || string.IsNullOrWhiteSpace(html) || html.Length > 10 * 1024 * 1024)
            throw new ArgumentException("Invalid print document or copy count.");
        cancellationToken.ThrowIfCancellationRequested();
        var settings = new PrinterSettings { PrinterName = printerName, Copies = 1 };
        if (!settings.IsValid) throw new InvalidOperationException("The configured printer is unavailable to the service account.");
        var paper = settings.DefaultPageSettings.PaperSize;
        var pdf = await HtmlDocumentRenderer.RenderAsync(html, paper.Width / 100d, paper.Height / 100d, cancellationToken).ConfigureAwait(false);
        return GdiSpoolPrinter.Print(pdf, settings, copies, cancellationToken);
    }
}

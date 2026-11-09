using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;

namespace Ekom.Printer.Rendering;

public sealed class DefaultPickingSlipHtmlRenderer : IPickingSlipHtmlRenderer
{
    public Task<string> RenderAsync(PickingSlipContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var html = new StringBuilder("""
            <!doctype html><html lang="en"><head><meta charset="utf-8">
            <title>Picking slip</title><style>
            @page { size: A4; margin: 15mm; }
            body { font-family: sans-serif; color: #000; }
            table { width: 100%; border-collapse: collapse; }
            th, td { text-align: left; padding: 6px; border-bottom: 1px solid #ccc; }
            thead { display: table-header-group; } tr { break-inside: avoid; }
            </style></head><body><h1>Picking slip</h1><p>Order:
            """);
        html.Append(HtmlEncoder.Default.Encode(context.OrderNumber));
        html.Append("</p><p>Status: ").Append(context.TriggerStatus).Append("</p>");
        html.Append("<table><thead><tr><th>SKU</th><th>Product</th><th>Variant</th><th>Quantity</th></tr></thead><tbody>");
        foreach (var line in context.Lines)
        {
            html.Append("<tr><td>").Append(HtmlEncoder.Default.Encode(line.Sku));
            html.Append("</td><td>").Append(HtmlEncoder.Default.Encode(line.Product));
            html.Append("</td><td>").Append(HtmlEncoder.Default.Encode(line.Variant ?? string.Empty));
            html.Append("</td><td>").Append(line.Quantity.ToString(CultureInfo.InvariantCulture)).Append("</td></tr>");
        }
        html.Append("</tbody></table></body></html>");
        return Task.FromResult(html.ToString());
    }
}

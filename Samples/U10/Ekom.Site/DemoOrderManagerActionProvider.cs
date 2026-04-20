using System.Text;
using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Services;

namespace Ekom.Site;

public sealed class DemoOrderManagerActionProvider : IOrderManagerActionProvider
{
    private const string DemoRefreshActionKey = "demo-refresh-order";
    private const string DemoPdfActionKey = "demo-open-pdf";

    public Task<IReadOnlyCollection<OrderManagerAction>> GetActionsAsync(IOrderInfo orderInfo, CancellationToken ct = default)
    {
        IReadOnlyCollection<OrderManagerAction> actions = new[]
        {
            new OrderManagerAction
            {
                Key = DemoRefreshActionKey,
                Label = "Demo Refresh",
                Look = "primary",
                ConfirmMessage = "Run the demo refresh action for this order?",
                SortOrder = 10
            },
            new OrderManagerAction
            {
                Key = DemoPdfActionKey,
                Label = "Demo PDF",
                Look = "outline",
                SortOrder = 20
            }
        };

        return Task.FromResult(actions);
    }

    public Task<OrderManagerActionExecutionResult?> ExecuteAsync(IOrderInfo orderInfo, string actionKey, string? userName = null, CancellationToken ct = default)
    {
        OrderManagerActionExecutionResult? result = actionKey switch
        {
            DemoRefreshActionKey => new OrderManagerActionSuccessResult
            {
                Message = $"Demo refresh completed for order {orderInfo.ReferenceId}."
            },
            DemoPdfActionKey => new OrderManagerActionFileResult
            {
                Content = CreateDemoPdf(orderInfo),
                ContentType = "application/pdf",
                FileName = $"ekom-demo-order-{orderInfo.ReferenceId}.pdf",
                Message = "Demo PDF generated."
            },
            _ => null
        };

        return Task.FromResult(result);
    }

    private static byte[] CreateDemoPdf(IOrderInfo orderInfo)
    {
        var title = EscapePdfText($"Ekom demo action for order {orderInfo.ReferenceId}");
        var orderNumber = EscapePdfText($"Order number: {orderInfo.OrderNumber}");
        var uniqueId = EscapePdfText($"Unique Id: {orderInfo.UniqueId}");
        var streamContent = $"BT\n/F1 18 Tf\n50 780 Td\n({title}) Tj\n0 -28 Td\n/F1 12 Tf\n({orderNumber}) Tj\n0 -20 Td\n({uniqueId}) Tj\nET\n";
        var streamLength = Encoding.ASCII.GetByteCount(streamContent);

        var offsets = new List<int>();
        var builder = new StringBuilder();

        void AppendObject(string value)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(value);
        }

        AppendObject("%PDF-1.4\n");
        AppendObject("1 0 obj<< /Type /Catalog /Pages 2 0 R >>endobj\n");
        AppendObject("2 0 obj<< /Type /Pages /Count 1 /Kids [3 0 R] >>endobj\n");
        AppendObject("3 0 obj<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>endobj\n");
        AppendObject($"4 0 obj<< /Length {streamLength} >>stream\n{streamContent}endstream\nendobj\n");
        AppendObject("5 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>endobj\n");

        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());

        builder.Append("xref\n0 6\n");
        builder.Append("0000000000 65535 f \n");

        foreach (var offset in offsets)
        {
            builder.Append(offset.ToString("D10"));
            builder.Append(" 00000 n \n");
        }

        builder.Append("trailer<< /Size 6 /Root 1 0 R >>\n");
        builder.Append("startxref\n");
        builder.Append(xrefOffset);
        builder.Append("\n%%EOF");

        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static string EscapePdfText(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }
}

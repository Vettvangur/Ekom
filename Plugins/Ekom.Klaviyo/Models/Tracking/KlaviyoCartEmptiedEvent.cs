using Ekom.Klaviyo.Models.Orders;

namespace Ekom.Klaviyo.Models.Tracking;

public sealed record KlaviyoCartEmptiedEvent
{
    public string StoreAlias { get; set; } = default!;
    public DateTimeOffset OccurredAt { get; set; }
    public KlaviyoOrderProfile Customer { get; set; } = default!;
    public string? EventId { get; set; }
    public string? OrderId { get; set; }
    public string? OrderNumber { get; set; }
    public string? Currency { get; set; }
    public Dictionary<string, object?> CustomProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

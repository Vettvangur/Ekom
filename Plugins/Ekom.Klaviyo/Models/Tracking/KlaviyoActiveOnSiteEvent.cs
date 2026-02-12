using Ekom.Klaviyo.Models.Orders;

namespace Ekom.Klaviyo.Models.Tracking;

public sealed record KlaviyoActiveOnSiteEvent
{
    public string StoreAlias { get; set; } = default!;
    public DateTimeOffset OccurredAt { get; set; }
    public KlaviyoOrderProfile Customer { get; set; } = default!;
    public string? EventId { get; set; }

    public string? Url { get; set; }
    public string? Referrer { get; set; }
    public int? DurationSeconds { get; set; }

    public Dictionary<string, object?> CustomProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

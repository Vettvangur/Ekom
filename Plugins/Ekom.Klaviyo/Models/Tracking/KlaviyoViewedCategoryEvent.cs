using Ekom.Klaviyo.Models.Orders;

namespace Ekom.Klaviyo.Models.Tracking;

public sealed record KlaviyoViewedCategoryEvent
{
    public string StoreAlias { get; set; } = default!;
    public DateTimeOffset OccurredAt { get; set; }
    public KlaviyoOrderProfile Customer { get; set; } = default!;
    public string? EventId { get; set; }

    public string? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryUrl { get; set; }

    public Dictionary<string, object?> CustomProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

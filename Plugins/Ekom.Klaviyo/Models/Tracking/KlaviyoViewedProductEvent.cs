using Ekom.Klaviyo.Models.Orders;

namespace Ekom.Klaviyo.Models.Tracking;

public sealed record KlaviyoViewedProductEvent
{
    public string StoreAlias { get; set; } = default!;
    public DateTimeOffset OccurredAt { get; set; }
    public KlaviyoOrderProfile Customer { get; set; } = default!;
    public string? EventId { get; set; }

    public string? ProductId { get; set; }
    public string? Sku { get; set; }
    public string? ProductName { get; set; }
    public decimal? Price { get; set; }
    public string? PriceFormatted { get; set; }
    public string? Currency { get; set; }
    public string? ProductUrl { get; set; }
    public string? ImageUrl { get; set; }

    public Dictionary<string, object?> CustomProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

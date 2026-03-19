using Ekom.Klaviyo.Models.Orders;

namespace Ekom.Klaviyo.Models.Tracking;

public sealed record KlaviyoStartedCheckoutEvent
{
    public string StoreAlias { get; set; } = default!;
    public DateTimeOffset OccurredAt { get; set; }
    public KlaviyoOrderProfile Customer { get; set; } = default!;
    public string? EventId { get; set; }

    public string? ListId { get; set; }

    public string? OrderId { get; set; }
    public string? OrderNumber { get; set; }
    public decimal? Value { get; set; }
    public string? ValueFormatted { get; set; }
    public string? Currency { get; set; }
    public string? CheckoutUrl { get; set; }
    public IReadOnlyList<KlaviyoOrderLine> Items { get; set; } = [];

    public Dictionary<string, object?> CustomProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

namespace Ekom.Klaviyo.Models;

public sealed record KlaviyoPlacedOrder
{
    public string OrderId { get; set; } = default!;
    public string OrderNumber { get; set; } = default!;
    public DateTimeOffset PlacedAt { get; set; }
    public decimal Value { get; set; }
    public string Currency { get; set; } = default!;
    public KlaviyoProfile Customer { get; set; } = default!;
    public KlaviyoShipTo? ShipTo { get; set; }
    public IReadOnlyList<KlaviyoOrderLine> Items { get; set; } = [];
    public string StoreAlias { get; set; } = default!;
    public string? CheckoutUrl { get; set; }
    public decimal? DiscountValue { get; set; }
    public string? PaymentProviderName { get; set; }
    public decimal? PaymentProviderValue { get; set; }
    public string? ShippingProviderName { get; set; }
    public decimal? ShippingProviderValue { get; set; }
    public decimal? TaxValue { get; set; }
    public Dictionary<string, object?> CustomProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

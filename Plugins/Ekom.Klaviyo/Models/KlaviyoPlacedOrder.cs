namespace Ekom.Klaviyo.Models;

public sealed record KlaviyoPlacedOrder
{
    public string OrderId { get; init; } = default!;
    public DateTimeOffset PlacedAt { get; init; }
    public decimal Value { get; init; }
    public string Currency { get; init; } = default!;
    public KlaviyoProfile Customer { get; init; } = default!;
    public IReadOnlyList<KlaviyoOrderLine> Items { get; init; } = [];
    public string StoreAlias { get; init; } = default!;
    public string? CheckoutUrl { get; init; }
    public decimal? DiscountValue { get; init; }
    public string? PaymentProviderName { get; init; }
    public decimal? PaymentProviderValue { get; init; }
    public string? ShippingProviderName { get; init; }
    public decimal? ShippingProviderValue { get; init; }
    public decimal? TaxValue { get; init; }
    public Dictionary<string, object?> CustomProperties { get; } = new(StringComparer.OrdinalIgnoreCase);
}

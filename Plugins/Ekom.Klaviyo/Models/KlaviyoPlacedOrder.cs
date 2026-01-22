namespace Ekom.Klaviyo.Models;

public sealed record KlaviyoPlacedOrder
{
    public string OrderId { get; init; } = default!;
    public DateTimeOffset PlacedAt { get; init; }
    public decimal Value { get; init; }
    public string Currency { get; init; } = default!;
    public KlaviyoCustomerIdentity Customer { get; init; } = default!;
    public IReadOnlyList<KlaviyoOrderLine> Items { get; init; } = [];
    public string StoreAlias { get; init; } = default!;
    public string? CheckoutUrl { get; init; }
    public string? PaymentMethod { get; init; }
    public decimal? DiscountValue { get; init; }
    public decimal? ShippingValue { get; init; }
    public decimal? TaxValue { get; init; }
}

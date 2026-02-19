using Ekom.Klaviyo.Models.Profiles;

namespace Ekom.Klaviyo.Models.Orders;

public sealed record KlaviyoPlacedOrder
{
    public string OrderId { get; set; } = default!;
    public string OrderNumber { get; set; } = default!;
    public DateTimeOffset PlacedAt { get; set; }
    public decimal Value { get; set; }
    public string ValueFormatted { get; set; } = default!;
    public string Currency { get; set; } = default!;
    public KlaviyoOrderProfile Customer { get; set; } = default!;
    public KlaviyoShipTo? ShipTo { get; set; }
    public IReadOnlyList<KlaviyoOrderLine> Items { get; set; } = [];
    public string StoreAlias { get; set; } = default!;
    public string? CheckoutUrl { get; set; }
    public decimal? DiscountValue { get; set; }
    public KlaviyoShippingProvider? ShippingProvider { get; set; } = null;
    public KlaviyoPaymentProvider? PaymentProvider { get; set; } = null;
    public decimal? TaxValue { get; set; }
    public Dictionary<string, object?> CustomProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public KlaviyoProfileConsentRequest? Consent { get; set; }
}

public sealed record KlaviyoShippingProvider
{
    public string? Title { get; set; } = default!;
    public decimal? Value { get; set; } = default!;
    public string? ValueFormatted { get; set; } = default!;
    public string Method { get; set; } = default!;
}

public sealed record KlaviyoPaymentProvider
{
    public string? Title { get; set; } = default!;
    public decimal? Value { get; set; } = default!;
    public string? ValueFormatted { get; set; } = default!;
}

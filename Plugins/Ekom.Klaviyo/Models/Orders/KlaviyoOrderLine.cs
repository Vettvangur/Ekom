namespace Ekom.Klaviyo.Models.Orders;
public sealed record KlaviyoOrderLine
{
    public string? ProductExternalId { get; set; }
    public string? Sku { get; set; }
    public required string Name { get; set; }
    public decimal UnitPrice { get; set; }
    public string UnitPriceFormatted { get; set; } = default!;
    public decimal LineTotal { get; set; }
    public string LineTotalFormatted { get; set; } = default!;
    public decimal UnitPriceWithOutVat { get; set; }
    public string UnitPriceWithOutVatFormatted { get; set; } = default!;
    public decimal LineTotalWithOutVat { get; set; }
    public string LineTotalWithOutVatFormatted { get; set; } = default!;
    public decimal VatValue { get; set; }
    public string VatValueFormatted { get; set; } = default!;
    public string VatPercentage { get; set; } = default!;
    public decimal Discount { get; set; }
    public string DiscountFormatted { get; set; } = default!;
    public decimal Quantity { get; set; }
    public string? ProductUrl { get; set; } = default!;
    public string? ImageUrl { get; set; } = default!;
    public KlaviyoVariantOrderLine? Variant { get; set; } = null;
    public IReadOnlyList<string>? Categories { get; set; }
    public Dictionary<string, object?> CustomProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record KlaviyoVariantOrderLine
{
    public string? Sku { get; set; }
    public required string Name { get; set; }
    public string? ImageUrl { get; set; } = default!;
}

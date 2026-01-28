namespace Ekom.Klaviyo.Models;
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
    public decimal Quantity { get; set; }
    public string? ProductUrl { get; set; }
    public string? ImageUrl { get; set; }
    public IReadOnlyList<string>? Categories { get; set; }
    public Dictionary<string, object?> CustomProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

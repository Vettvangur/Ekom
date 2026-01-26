namespace Ekom.Klaviyo.Models;
public sealed record KlaviyoOrderLine
{
    public string? ProductExternalId { get; init; }
    public string? Sku { get; init; }
    public required string Name { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal LineTotal { get; init; }
    public decimal Quantity { get; init; }
    public string? ProductUrl { get; init; }
    public string? ImageUrl { get; init; }
    public IReadOnlyList<string>? Categories { get; init; }
    public Dictionary<string, object?> CustomProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

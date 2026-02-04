namespace Ekom.Klaviyo.Models.Catalog;

public sealed class KlaviyoProductItem
{
    // store-aware catalog id
    public required string StoreAlias { get; set; }
    public required string Sku { get; set; }
    public required Guid Id { get; set; }

    public required string Title { get; set; }
    public decimal? Price { get; set; }              // list/base only
    public string? Currency { get; set; }            // optional
    public string? Url { get; set; }
    public string? ImageFullUrl { get; set; }
    public bool Published { get; init; } = true;
    public required string Description { get; set; }
    public string Summary { get; set; } = string.Empty;

    public IReadOnlyCollection<string>? Categories { get; set; }
    public Dictionary<string, object?>? CustomMetadata { get; set; }

    public string ExternalId => $"{StoreAlias}:{Id}";
}

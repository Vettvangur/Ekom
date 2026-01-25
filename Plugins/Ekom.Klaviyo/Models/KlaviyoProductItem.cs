namespace Ekom.Klaviyo.Models;

public sealed class KlaviyoProductItem
{
    // store-aware catalog id
    public required string StoreAlias { get; init; }
    public required string Sku { get; init; }
    public required Guid Id { get; init; }

    public required string Title { get; init; }
    public decimal? Price { get; init; }              // list/base only
    public string? Currency { get; init; }            // optional
    public string? Url { get; init; }
    public string? ImageFullUrl { get; init; }
    public bool Published { get; init; } = true;
    public required string Description { get; init; }
    public string Summary { get; init; } = string.Empty;

    public IReadOnlyCollection<string>? Categories { get; init; }
    public Dictionary<string, object?>? CustomMetadata { get; init; }

    public string ExternalId => $"{StoreAlias}:{Id}";
}

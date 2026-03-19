using Ekom.Klaviyo.Models.Catalog;

public sealed class KlaviyoProductEnrichmentContext
{
    public required string StoreAlias { get; init; }
    public required Guid ProductKey { get; init; }
    public KlaviyoProductItem? Product { get; init; }
    public bool IsPublished { get; init; }
    public bool IsFirstPublish { get; init; }
    public Dictionary<string, object?> CustomMetaData { get; } = new(StringComparer.OrdinalIgnoreCase);
}

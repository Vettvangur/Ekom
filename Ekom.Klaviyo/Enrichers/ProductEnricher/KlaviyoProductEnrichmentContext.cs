using Ekom.Models;

internal sealed class KlaviyoProductEnrichmentContext
{
    public required string StoreAlias { get; init; }
    public required Guid ProductKey { get; init; }
    public IProduct? SourceProduct { get; init; }
    public bool IsPublished { get; init; }
}

using Algolia.Search.Models.Search;

namespace Ekom.Algolia.Models.Search;

public sealed class AlgoliaSearchRequest
{
    public required string StoreAlias { get; init; }
    public string? Locale { get; init; }
    public string? Currency { get; init; }
    public bool BypassCache { get; init; }
    public required SearchForHits Query { get; init; }
}

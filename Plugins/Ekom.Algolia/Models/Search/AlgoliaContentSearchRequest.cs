using Algolia.Search.Models.Search;

namespace Ekom.Algolia.Models.Search;

public sealed class AlgoliaContentSearchRequest
{
    public required string IndexName { get; init; }
    public required string Culture { get; init; }
    public bool BypassCache { get; init; }
    public required SearchForHits Query { get; init; }
}

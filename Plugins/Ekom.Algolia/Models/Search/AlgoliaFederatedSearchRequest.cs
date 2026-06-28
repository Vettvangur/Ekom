using Algolia.Search.Models.Search;
using Ekom.Algolia.Models.Indexing;

namespace Ekom.Algolia.Models.Search;

public sealed class AlgoliaFederatedSearchRequest
{
    public required string StoreAlias { get; init; }
    public string? Locale { get; init; }
    public string? Currency { get; init; }
    public bool BypassCache { get; init; }
    public IReadOnlyCollection<AlgoliaFederatedSearchTarget> Targets { get; init; } = [];
}

public sealed class AlgoliaFederatedSearchTarget
{
    public required string Key { get; init; }
    public AlgoliaFederatedSearchTargetKind Kind { get; init; }
    public required SearchForHits Query { get; init; }
    public string? ContentIndexName { get; init; }
    public string? Culture { get; init; }
}

public enum AlgoliaFederatedSearchTargetKind
{
    Products,
    Categories,
    QuerySuggestions,
    Content
}

public sealed class AlgoliaFederatedSearchResponse
{
    public AlgoliaSearchResponse<AlgoliaQuerySuggestionRecord>? Suggestions { get; init; }
    public AlgoliaSearchResponse<AlgoliaProductRecord>? Products { get; init; }
    public AlgoliaSearchResponse<AlgoliaCategoryRecord>? Categories { get; init; }
    public IReadOnlyDictionary<string, AlgoliaSearchResponse<AlgoliaContentRecord>> Content { get; init; } = new Dictionary<string, AlgoliaSearchResponse<AlgoliaContentRecord>>();
}

using System.Text.Json;

namespace Ekom.Algolia.Models.Search;

public sealed class AlgoliaSearchResponse<THit>
{
    public IReadOnlyList<THit> Hits { get; init; } = [];
    public int Page { get; init; }
    public int HitsPerPage { get; init; }
    public int TotalHits { get; init; }
    public int TotalPages { get; init; }
    public int ProcessingTimeMs { get; init; }
    public string? Query { get; init; }
    public IReadOnlyDictionary<string, JsonElement>? Facets { get; init; }
}

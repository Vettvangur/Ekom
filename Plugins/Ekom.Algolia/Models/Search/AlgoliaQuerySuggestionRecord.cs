using System.Text.Json.Serialization;

namespace Ekom.Algolia.Models.Search;

public sealed class AlgoliaQuerySuggestionRecord
{
    [JsonPropertyName("objectID")]
    public string? ObjectId { get; init; }

    public string? Query { get; init; }

    [JsonExtensionData]
    public Dictionary<string, object?> Data { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

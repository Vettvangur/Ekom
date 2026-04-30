using System.Text.Json.Serialization;

namespace Ekom.Algolia.Models.Indexing;

public sealed class AlgoliaCategoryRecord
{
    [JsonPropertyName("objectID")]
    public required string ObjectId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NodeName { get; init; }

    public required string Title { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Slug { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StoreAlias { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Locale { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParentId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? CreatedAt { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? UpdatedAt { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SortOrder { get; init; }

    [JsonExtensionData]
    public Dictionary<string, object?> Data { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

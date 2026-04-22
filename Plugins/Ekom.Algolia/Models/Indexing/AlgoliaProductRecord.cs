using System.Text.Json.Serialization;

namespace Ekom.Algolia.Models.Indexing;

public sealed class AlgoliaProductRecord
{
    [JsonPropertyName("objectID")]
    public required string ObjectId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Sku { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NodeName { get; init; }

    public required string Title { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Summary { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; init; }

    [JsonPropertyName("image_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ImageUrl { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? ImageUrls { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Price { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? PriceWithVat { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? PriceWithoutVat { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Currency { get; init; }

    public bool Available { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Stock { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StoreAlias { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Locale { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? CreatedAt { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? UpdatedAt { get; init; }

    [JsonExtensionData]
    public Dictionary<string, object?> Data { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

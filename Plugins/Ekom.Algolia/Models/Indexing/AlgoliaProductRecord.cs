using System.Text.Json.Serialization;

namespace Ekom.Algolia.Models.Indexing;

public sealed class AlgoliaProductRecord
{
    [JsonPropertyName("objectID")]
    public required string ObjectId { get; init; }

    public string? Sku { get; init; }
    public string? Name { get; init; }
    public string? Summary { get; init; }
    public string? Description { get; init; }
    public string? Url { get; init; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; init; }

    public IReadOnlyList<string> ImageUrls { get; init; } = [];

    public decimal? Price { get; init; }
    public decimal? PriceWithVat { get; init; }
    public decimal? PriceWithoutVat { get; init; }
    public string? Currency { get; init; }

    public bool Available { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Stock { get; init; }

    public string? StoreAlias { get; init; }
    public string? Locale { get; init; }

    public IReadOnlyList<string> CategoryPageIdentifier { get; init; } = [];

    public long? CreatedAt { get; init; }
    public long? UpdatedAt { get; init; }

    [JsonExtensionData]
    public Dictionary<string, object?> Data { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

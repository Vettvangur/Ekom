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
    public IReadOnlyList<string> Urls { get; init; } = [];
    public IReadOnlyList<string> ImageUrls { get; init; } = [];

    public decimal? Price { get; init; }
    public decimal? OriginalPrice { get; init; }
    public string? Currency { get; init; }

    public bool Available { get; init; }
    public decimal? Stock { get; init; }
    public bool Backorder { get; init; }

    public string? StoreAlias { get; init; }
    public string? Locale { get; init; }

    public IReadOnlyList<string> CategoryNames { get; init; } = [];
    public IReadOnlyList<string> CategoryKeys { get; init; } = [];
    public IReadOnlyList<string> CategoryAncestors { get; init; } = [];

    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    [JsonExtensionData]
    public Dictionary<string, object?> Data { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

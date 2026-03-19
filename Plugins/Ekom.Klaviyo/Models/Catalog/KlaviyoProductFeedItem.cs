using System.Text.Json.Serialization;

namespace Ekom.Klaviyo.Models.Catalog;

internal sealed record KlaviyoProductFeedItem(
    [property: JsonPropertyName("id")]
    string Id,

    [property: JsonPropertyName("title")]
    string Title,

    [property: JsonPropertyName("link")]
    string Link,

    [property: JsonPropertyName("description")]
    string? Description,

    [property: JsonPropertyName("price")]
    decimal? Price,

    [property: JsonPropertyName("image_link")]
    string? ImageLink,

    [property: JsonPropertyName("categories")]
    IReadOnlyList<string>? Categories,

    [property: JsonPropertyName("inventory_quantity")]
    decimal? InventoryQuantity,

    [property: JsonPropertyName("inventory_policy")]
    int? InventoryPolicy,

    // Arbitrary additional fields
    [property: JsonPropertyName("custom_attributes")]
    IReadOnlyDictionary<string, object?>? CustomAttributes
);

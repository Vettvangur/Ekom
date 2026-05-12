using System.Text.Json.Serialization;

namespace Ekom.Klaviyo.Models.Catalog;

public sealed class KlaviyoProductFeedItem
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("link")]
    public required string Link { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("price")]
    public decimal? Price { get; set; }
    [JsonPropertyName("vat")]
    public decimal? Vat { get; set; }

    [JsonPropertyName("image_link")]
    public string? ImageLink { get; set; }

    [JsonPropertyName("categories")]
    public IReadOnlyList<string>? Categories { get; set; }

    [JsonPropertyName("inventory_quantity")]
    public decimal? InventoryQuantity { get; set; }

    [JsonPropertyName("inventory_policy")]
    public int? InventoryPolicy { get; set; }

    // Arbitrary additional fields
    [JsonPropertyName("custom_attributes")]
    public Dictionary<string, object?>? CustomAttributes { get; set; }
}

using Ekom.Klaviyo.Models.Catalog;
using Ekom.Models;

namespace Ekom.Klaviyo.Enrichers.ProductFeedEnricher;

public sealed class KlaviyoProductFeedEnrichmentContext
{
    public required string StoreAlias { get; init; }
    public required string Culture { get; init; }
    public required IProduct Product { get; init; }
    public required KlaviyoProductFeedItem FeedItem { get; init; }
    public required KlaviyoOptions Options { get; init; }
}

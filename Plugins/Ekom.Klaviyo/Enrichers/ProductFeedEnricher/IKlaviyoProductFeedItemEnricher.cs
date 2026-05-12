using Ekom.Klaviyo.Models.Catalog;

namespace Ekom.Klaviyo.Enrichers.ProductFeedEnricher;

public interface IKlaviyoProductFeedItemEnricher
{
    ValueTask EnrichAsync(KlaviyoProductFeedItem item, KlaviyoProductFeedEnrichmentContext ctx, CancellationToken ct);
}

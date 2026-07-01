using Ekom.Klaviyo.Models.Catalog;

namespace Ekom.Klaviyo.Enrichers.ProductFeedEnricher;

internal sealed class KlaviyoProductFeedEnrichmentPipeline
{
    private readonly IReadOnlyList<IKlaviyoProductFeedItemEnricher> _enrichers;

    public KlaviyoProductFeedEnrichmentPipeline(IEnumerable<IKlaviyoProductFeedItemEnricher> enrichers)
        => _enrichers = enrichers.ToList();

    public async ValueTask ApplyAsync(KlaviyoProductFeedItem item, KlaviyoProductFeedEnrichmentContext ctx, CancellationToken ct)
    {
        foreach (var enricher in _enrichers)
            await enricher.EnrichAsync(item, ctx, ct);
    }
}

using Ekom.Klaviyo.Models.Catalog;

namespace Ekom.Klaviyo.Enrichers.ProductEnricher;

internal sealed class KlaviyoProductEnrichmentPipeline
{
    private readonly IReadOnlyList<IKlaviyoProductItemEnricher> _enrichers;

    public KlaviyoProductEnrichmentPipeline(IEnumerable<IKlaviyoProductItemEnricher> enrichers)
        => _enrichers = enrichers.ToList();

    public async ValueTask ApplyAsync(KlaviyoProductItem item, KlaviyoProductEnrichmentContext ctx, CancellationToken ct)
    {
        foreach (var enricher in _enrichers)
            await enricher.EnrichAsync(item, ctx, ct);
    }
}

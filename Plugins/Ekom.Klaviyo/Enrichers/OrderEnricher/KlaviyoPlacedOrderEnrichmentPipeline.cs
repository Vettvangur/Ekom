using Ekom.Klaviyo.Models;

namespace Ekom.Klaviyo.Enrichers.OrderEnricher;

internal sealed class KlaviyoPlacedOrderEnrichmentPipeline
{
    private readonly IReadOnlyList<IKlaviyoPlacedOrderEnricher> _enrichers;

    public KlaviyoPlacedOrderEnrichmentPipeline(IEnumerable<IKlaviyoPlacedOrderEnricher> enrichers)
        => _enrichers = enrichers.ToList();

    public async ValueTask ApplyAsync(KlaviyoPlacedOrder order, CancellationToken ct)
    {
        foreach (var enricher in _enrichers)
            await enricher.EnrichAsync(order, ct);
    }
}

namespace Ekom.Klaviyo.Enrichers.TrackingEnricher;

public sealed class KlaviyoTrackingEnrichmentPipeline
{
    private readonly IEnumerable<IKlaviyoTrackingEnricher> _enrichers;

    public KlaviyoTrackingEnrichmentPipeline(IEnumerable<IKlaviyoTrackingEnricher> enrichers)
    {
        _enrichers = enrichers;
    }

    public async ValueTask ApplyAsync(KlaviyoTrackingEnrichmentContext context, CancellationToken ct = default)
    {
        foreach (var enricher in _enrichers)
            await enricher.EnrichAsync(context, ct);
    }
}

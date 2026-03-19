namespace Ekom.Klaviyo.Enrichers.TrackingEnricher;

public interface IKlaviyoTrackingEnricher
{
    ValueTask EnrichAsync(KlaviyoTrackingEnrichmentContext context, CancellationToken ct = default);
}

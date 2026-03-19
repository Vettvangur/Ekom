namespace Ekom.Klaviyo.Enrichers.TrackingEnricher;

public interface IKlaviyoTrackingEnricherRunner
{
    ValueTask ApplyAsync(
        Models.Tracking.KlaviyoTrackingEventType type,
        object payload,
        string storeAlias,
        CancellationToken ct = default);
}

internal sealed class KlaviyoTrackingEnricherRunner : IKlaviyoTrackingEnricherRunner
{
    private readonly KlaviyoTrackingEnrichmentPipeline _pipeline;

    public KlaviyoTrackingEnricherRunner(KlaviyoTrackingEnrichmentPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public ValueTask ApplyAsync(
        Models.Tracking.KlaviyoTrackingEventType type,
        object payload,
        string storeAlias,
        CancellationToken ct = default)
        => _pipeline.ApplyAsync(new KlaviyoTrackingEnrichmentContext(type, payload, storeAlias), ct);
}

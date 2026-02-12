using Ekom.Klaviyo.Models.Tracking;

namespace Ekom.Klaviyo.Enrichers.TrackingEnricher;

public sealed class KlaviyoTrackingEnrichmentContext
{
    public KlaviyoTrackingEventType Type { get; }
    public object Payload { get; }
    public string StoreAlias { get; }

    public KlaviyoTrackingEnrichmentContext(
        KlaviyoTrackingEventType type,
        object payload,
        string storeAlias)
    {
        Type = type;
        Payload = payload;
        StoreAlias = storeAlias;
    }
}

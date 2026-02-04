using Ekom.Klaviyo.Models.Subscriptions;

namespace Ekom.Klaviyo.Enrichers.SubscriptionsEnricher;

internal sealed class KlaviyoSubscriptionsEnrichmentPipeline
{
    private readonly IReadOnlyList<IKlaviyoSubscriptionsEnricher> _enrichers;

    public KlaviyoSubscriptionsEnrichmentPipeline(IEnumerable<IKlaviyoSubscriptionsEnricher> enrichers)
        => _enrichers = enrichers.ToList();

    public async ValueTask ApplyAsync(KlaviyoProfile profile, IReadOnlyList<KlaviyoConsentChange>? consents, CancellationToken ct)
    {
        foreach (var enricher in _enrichers)
            await enricher.EnrichAsync(profile, consents, ct);
    }
}

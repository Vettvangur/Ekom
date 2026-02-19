using Ekom.Klaviyo.Models.Profiles;

namespace Ekom.Klaviyo.Enrichers.ProfilesEnricher;

internal sealed class KlaviyoProfilesEnrichmentPipeline
{
    private readonly IReadOnlyList<IKlaviyoProfilesEnricher> _enrichers;

    public KlaviyoProfilesEnrichmentPipeline(IEnumerable<IKlaviyoProfilesEnricher> enrichers)
        => _enrichers = enrichers.ToList();

    public async ValueTask ApplyAsync(string email, IReadOnlyList<KlaviyoProfileConsentChange>? consents, CancellationToken ct)
    {
        foreach (var enricher in _enrichers)
            await enricher.EnrichAsync(email, consents, ct);
    }
}

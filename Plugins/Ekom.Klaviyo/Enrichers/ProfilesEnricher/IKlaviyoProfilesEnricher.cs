using Ekom.Klaviyo.Models.Profiles;

namespace Ekom.Klaviyo.Enrichers.ProfilesEnricher;

public interface IKlaviyoProfilesEnricher
{
    ValueTask EnrichAsync(string email, IReadOnlyList<KlaviyoProfileConsentChange>? consents, CancellationToken ct);
}

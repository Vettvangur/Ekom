using Ekom.Klaviyo.Models.Subscriptions;

namespace Ekom.Klaviyo.Enrichers.SubscriptionsEnricher;

public interface IKlaviyoSubscriptionsEnricher
{
    ValueTask EnrichAsync(KlaviyoProfile profile, IReadOnlyList<KlaviyoConsentChange>? consents, CancellationToken ct);
}

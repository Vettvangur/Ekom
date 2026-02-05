using Ekom.Klaviyo.Models.Orders;
using Ekom.Klaviyo.Models.Subscriptions;

namespace Ekom.Klaviyo.Enrichers.SubscriptionsEnricher;

public interface IKlaviyoSubscriptionsEnricherRunner
{
    ValueTask ApplyAsync(KlaviyoSubscriptionUpdate update, CancellationToken ct);
    ValueTask ApplyAsync(KlaviyoProfileUpdate update, CancellationToken ct);
    ValueTask ApplyAsync(KlaviyoConsentUpdate update, CancellationToken ct);
}

internal sealed class KlaviyoSubscriptionsEnricherRunner : IKlaviyoSubscriptionsEnricherRunner
{
    private readonly KlaviyoSubscriptionsEnrichmentPipeline _pipeline;

    public KlaviyoSubscriptionsEnricherRunner(KlaviyoSubscriptionsEnrichmentPipeline pipeline)
        => _pipeline = pipeline;

    public ValueTask ApplyAsync(KlaviyoSubscriptionUpdate update, CancellationToken ct)
        => _pipeline.ApplyAsync(update.Profile, update.Consents, ct);

    public ValueTask ApplyAsync(KlaviyoProfileUpdate update, CancellationToken ct)
        => _pipeline.ApplyAsync(update.Profile, consents: null, ct);

    public ValueTask ApplyAsync(KlaviyoConsentUpdate update, CancellationToken ct)
        => _pipeline.ApplyAsync(update.Profile, update.Consents, ct);
}

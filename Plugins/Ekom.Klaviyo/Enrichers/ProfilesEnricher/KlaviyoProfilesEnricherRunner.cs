using Ekom.Klaviyo.Models.Profiles;

namespace Ekom.Klaviyo.Enrichers.ProfilesEnricher;

public interface IKlaviyoProfilesEnricherRunner
{
    ValueTask ApplyAsync(KlaviyoProfileSubscriptionUpdate update, CancellationToken ct);
    ValueTask ApplyAsync(KlaviyoProfileUpdate update, CancellationToken ct);
    ValueTask ApplyAsync(KlaviyoProfileSubscribeRequest update, CancellationToken ct);
}

internal sealed class KlaviyoProfilesEnricherRunner : IKlaviyoProfilesEnricherRunner
{
    private readonly KlaviyoProfilesEnrichmentPipeline _pipeline;

    public KlaviyoProfilesEnricherRunner(KlaviyoProfilesEnrichmentPipeline pipeline)
        => _pipeline = pipeline;

    public ValueTask ApplyAsync(KlaviyoProfileSubscriptionUpdate update, CancellationToken ct)
        => _pipeline.ApplyAsync(update.Profile.Customer.Email ?? string.Empty, update.Consents, ct);

    public ValueTask ApplyAsync(KlaviyoProfileUpdate update, CancellationToken ct)
        => _pipeline.ApplyAsync(update.Profile.Customer.Email ?? string.Empty, consents: null, ct);

    public ValueTask ApplyAsync(KlaviyoProfileSubscribeRequest update, CancellationToken ct)
        => _pipeline.ApplyAsync(update.Email, update.Consents, ct);
}

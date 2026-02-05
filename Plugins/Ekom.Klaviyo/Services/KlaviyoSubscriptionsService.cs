using Ekom.Klaviyo.Dispatching.Subscriptions;
using Ekom.Klaviyo.Enrichers.SubscriptionsEnricher;
using Ekom.Klaviyo.Helpers;
using Ekom.Klaviyo.Mappers;
using Ekom.Klaviyo.Models.Subscriptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Klaviyo.Services;

public interface IKlaviyoSubscriptionsService
{
    ValueTask UpsertProfileAsync(KlaviyoProfileUpdate payload, CancellationToken ct = default);

    ValueTask SubscribeAsync(KlaviyoConsentUpdate payload, CancellationToken ct = default);
    ValueTask UnsubscribeAsync(KlaviyoConsentUpdate payload, CancellationToken ct = default);
}

public sealed class KlaviyoSubscriptionsService : IKlaviyoSubscriptionsService
{
    private readonly KlaviyoOptions _opt;
    private readonly ILogger<KlaviyoSubscriptionsService> _logger;
    private readonly IKlaviyoSubscriptionsDispatcher _dispatcher;
    private readonly IKlaviyoSubscriptionsEnricherRunner? _enrichers;

    public KlaviyoSubscriptionsService(
        IOptions<KlaviyoOptions> opt,
        ILogger<KlaviyoSubscriptionsService> logger,
        IKlaviyoSubscriptionsDispatcher dispatcher,
        IKlaviyoSubscriptionsEnricherRunner? enrichers = null)
    {
        _opt = opt.Value;
        _logger = logger;
        _dispatcher = dispatcher;
        _enrichers = enrichers;
    }

    public async ValueTask UpsertProfileAsync(KlaviyoProfileUpdate payload, CancellationToken ct = default)
    {
        if (!IsEnabled()) return;

        if (!payload.Profile.Customer.HasIdentifier)
        {
            _logger.LogWarning(
                "Klaviyo: skipping Profile Upsert because no customer identifier was provided. Store={StoreAlias}",
                payload.StoreAlias);
            return;
        }

        if (_enrichers is not null)
            await _enrichers.ApplyAsync(payload, ct);

        var work = new KlaviyoSubscriptionsWork(
            Type: KlaviyoSubscriptionsEventType.ProfileUpsert,
            Payload: payload.ToProfileImportRequest(),
            OccurredAt: DateTimeOffset.UtcNow,
            StoreAlias: payload.StoreAlias,
            CustomerIdentifier: payload.Profile.Customer.IdentifierForLogs());

        await _dispatcher.EnqueueAsync(work, ct);
    }

    public async ValueTask SubscribeAsync(KlaviyoConsentUpdate payload, CancellationToken ct = default)
    {
        await SendConsentJobAsync(payload, KlaviyoSubscriptionsEventType.Subscribe, ct);
    }

    public async ValueTask UnsubscribeAsync(KlaviyoConsentUpdate payload, CancellationToken ct = default)
    {
        await SendConsentJobAsync(payload, KlaviyoSubscriptionsEventType.Unsubscribe, ct);
    }

    private async ValueTask SendConsentJobAsync(KlaviyoConsentUpdate payload, KlaviyoSubscriptionsEventType type, CancellationToken ct)
    {
        if (!IsEnabled()) return;

        if (!payload.Profile.Customer.HasIdentifier)
        {
            _logger.LogWarning(
                "Klaviyo: skipping {Type} because no customer identifier was provided. Store={StoreAlias}",
                type, payload.StoreAlias);
            return;
        }

        if (payload.Consents is null || payload.Consents.Count == 0)
        {
            _logger.LogDebug(
                "Klaviyo: skipping {Type} because no consent changes were provided. Store={StoreAlias}",
                type, payload.StoreAlias);
            return;
        }

        if (_enrichers is not null)
            await _enrichers.ApplyAsync(payload, ct);

        var request = type switch
        {
            KlaviyoSubscriptionsEventType.Subscribe => payload.ToBulkSubscribeJobRequest(),
            KlaviyoSubscriptionsEventType.Unsubscribe => payload.ToBulkUnsubscribeJobRequest(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unexpected subscriptions event type")
        };

        var work = new KlaviyoSubscriptionsWork(
            Type: type,
            Payload: request,
            OccurredAt: DateTimeOffset.UtcNow,
            StoreAlias: payload.StoreAlias,
            CustomerIdentifier: payload.Profile.Customer.IdentifierForLogs());

        await _dispatcher.EnqueueAsync(work, ct);
    }

    private bool IsEnabled() => _opt.Enabled && _opt.Subscriptions.Enabled;
}

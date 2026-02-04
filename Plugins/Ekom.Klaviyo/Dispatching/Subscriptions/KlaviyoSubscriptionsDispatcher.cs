using Ekom.Klaviyo.Clients;
using Ekom.Klaviyo.Models.Subscriptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Klaviyo.Dispatching.Subscriptions;

public interface IKlaviyoSubscriptionsDispatcher
{
    ValueTask EnqueueAsync(KlaviyoSubscriptionsWork work, CancellationToken ct = default);
}

public sealed record KlaviyoSubscriptionsWork(
    KlaviyoSubscriptionsEventType Type,
    object Payload,
    DateTimeOffset OccurredAt,
    string StoreAlias,
    string CustomerIdentifier);

internal sealed class KlaviyoSubscriptionsDispatcher
    : BatchingChannelDispatcher<KlaviyoSubscriptionsWork>, IKlaviyoSubscriptionsDispatcher
{
    private readonly IKlaviyoSubscriptionsClient _client;
    private readonly KlaviyoOptions _opt;
    private readonly ILogger<KlaviyoSubscriptionsDispatcher> _logger;

    public KlaviyoSubscriptionsDispatcher(
        IKlaviyoSubscriptionsClient client,
        IOptions<KlaviyoOptions> options,
        ILogger<KlaviyoSubscriptionsDispatcher> logger)
        : base(
            name: "SubscriptionsDispatcher",
            dispatch: options.Value.Subscriptions.Dispatching,
            logger: logger)
    {
        _client = client;
        _opt = options.Value;
        _logger = logger;
    }

    public ValueTask EnqueueAsync(KlaviyoSubscriptionsWork work, CancellationToken ct = default)
        => base.EnqueueAsync(work, ct);

    protected override Task<List<KlaviyoSubscriptionsWork>> PrepareBatchAsync(List<KlaviyoSubscriptionsWork> drained, CancellationToken ct)
    {
        if (!_opt.Enabled || !_opt.Subscriptions.Enabled)
            return Task.FromResult(new List<KlaviyoSubscriptionsWork>(0));

        return Task.FromResult(drained);
    }

    protected override async Task HandleChunkAsync(KlaviyoSubscriptionsWork[] chunk, CancellationToken ct)
    {
        if (chunk.Length == 0) return;

        var maxConcurrency = Math.Max(1, _opt.Subscriptions.Dispatching.MaxConcurrency);
        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var tasks = chunk.Select(async work =>
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                switch (work.Type)
                {
                    case KlaviyoSubscriptionsEventType.ProfileUpsert:
                        await _client.UpsertProfileAsync(work.Payload, work.StoreAlias, ct).ConfigureAwait(false);
                        break;

                    case KlaviyoSubscriptionsEventType.Subscribe:
                        await _client.BulkSubscribeAsync(work.Payload, work.StoreAlias, ct).ConfigureAwait(false);
                        break;

                    case KlaviyoSubscriptionsEventType.Unsubscribe:
                        await _client.BulkUnsubscribeAsync(work.Payload, work.StoreAlias, ct).ConfigureAwait(false);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(work.Type), work.Type, "Unknown subscriptions work type");
                }

                _logger.LogDebug(
                    "Klaviyo SubscriptionsDispatcher sent {Type} CustomerIdentifier={CustomerIdentifier} OccurredAt={OccurredAt} StoreAlias={StoreAlias} Testing={Testing}.",
                    work.Type, work.CustomerIdentifier, work.OccurredAt, work.StoreAlias, _opt.Testing);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        _logger.LogDebug("Klaviyo SubscriptionsDispatcher sent {Count} subscriptions jobs in chunk.", chunk.Length);
    }
}

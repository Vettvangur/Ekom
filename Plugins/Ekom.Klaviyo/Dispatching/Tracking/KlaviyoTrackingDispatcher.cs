using Ekom.Klaviyo.Clients;
using Ekom.Klaviyo.Models.Tracking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Klaviyo.Dispatching.Tracking;

public interface IKlaviyoTrackingDispatcher
{
    ValueTask EnqueueAsync(KlaviyoTrackingWork work, CancellationToken ct = default);
}

public sealed record KlaviyoTrackingWork(
    KlaviyoTrackingEventType Type,
    object EventPayload,
    DateTimeOffset OccurredAt,
    string StoreAlias,
    string EventId);

internal sealed class KlaviyoTrackingDispatcher
    : BatchingChannelDispatcher<KlaviyoTrackingWork>, IKlaviyoTrackingDispatcher
{
    private readonly IKlaviyoTrackingClient _trackingClient;
    private readonly KlaviyoOptions _opt;
    private readonly ILogger<KlaviyoTrackingDispatcher> _logger;

    public KlaviyoTrackingDispatcher(
        IKlaviyoTrackingClient trackingClient,
        IOptions<KlaviyoOptions> options,
        ILogger<KlaviyoTrackingDispatcher> logger)
        : base(
            name: "TrackingDispatcher",
            dispatch: options.Value.Tracking.Dispatching,
            logger: logger)
    {
        _trackingClient = trackingClient;
        _opt = options.Value;
        _logger = logger;
    }

    public ValueTask EnqueueAsync(KlaviyoTrackingWork work, CancellationToken ct = default)
        => base.EnqueueAsync(work, ct);

    protected override Task<List<KlaviyoTrackingWork>> PrepareBatchAsync(List<KlaviyoTrackingWork> drained, CancellationToken ct)
    {
        if (!_opt.Enabled || !_opt.Tracking.Enabled)
            return Task.FromResult(new List<KlaviyoTrackingWork>(0));

        return Task.FromResult(drained);
    }

    protected override async Task HandleChunkAsync(KlaviyoTrackingWork[] chunk, CancellationToken ct)
    {
        if (chunk.Length == 0) return;

        var maxConcurrency = Math.Max(1, _opt.Tracking.Dispatching.MaxConcurrency);

        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var tasks = chunk.Select(async work =>
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await _trackingClient.TrackEventAsync(work.EventPayload, work.StoreAlias, ct).ConfigureAwait(false);

                _logger.LogDebug(
                    "Klaviyo TrackingDispatcher sent {Type} EventId={EventId} OccurredAt={OccurredAt} StoreAlias={StoreAlias} Testing={Testing}.",
                    work.Type, work.EventId, work.OccurredAt, work.StoreAlias, _opt.Testing);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        _logger.LogDebug("Klaviyo TrackingDispatcher sent {Count} tracking events in chunk.", chunk.Length);
    }
}

using Ekom.Klaviyo.Clients;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Klaviyo.Dispatching.Events;

public interface IKlaviyoEventsDispatcher
{
    ValueTask EnqueueAsync(KlaviyoEventWork work, CancellationToken ct = default);
}

/// <summary>
/// A single queued event work item.
/// Payload is already mapped to Klaviyo Events API schema (the inner { type, attributes } object).
/// </summary>
public sealed record KlaviyoEventWork(
    string Name,
    object Payload,
    DateTimeOffset OccurredAt,
    string? StoreAlias = null);

internal sealed class KlaviyoEventsDispatcher
    : BatchingChannelDispatcher<KlaviyoEventWork>, IKlaviyoEventsDispatcher
{
    private readonly IKlaviyoEventsClient _eventsClient;
    private readonly KlaviyoOptions _opt;
    private readonly ILogger<KlaviyoEventsDispatcher> _logger;

    public KlaviyoEventsDispatcher(
        IKlaviyoEventsClient eventsClient,
        IOptions<KlaviyoOptions> options,
        ILogger<KlaviyoEventsDispatcher> logger)
        : base(
            name: "EventsDispatcher",
            dispatch: options.Value.Events.Dispatching,
            logger: logger)
    {
        _eventsClient = eventsClient;
        _opt = options.Value;
        _logger = logger;
    }

    public ValueTask EnqueueAsync(KlaviyoEventWork work, CancellationToken ct = default)
        => base.EnqueueAsync(work, ct);

    protected override Task<List<KlaviyoEventWork>> PrepareBatchAsync(
        List<KlaviyoEventWork> drained,
        CancellationToken ct)
    {
        if (!_opt.Enabled || !_opt.Events.Enabled)
            return Task.FromResult(new List<KlaviyoEventWork>(0));

        return Task.FromResult(drained);
    }

    protected override async Task HandleChunkAsync(KlaviyoEventWork[] chunk, CancellationToken ct)
    {
        if (chunk.Length == 0)
            return;

        // /api/events expects a single event object at /data, so we send one request per event.
        // Use bounded concurrency to preserve throughput.
        var maxConcurrency = Math.Max(1, _opt.Events.Dispatching.MaxConcurrency); // Ensure >= 1

        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var tasks = chunk.Select(async work =>
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var storeAlias = work.StoreAlias ?? string.Empty;

                await _eventsClient.TrackEventAsync(work.Payload, storeAlias, ct).ConfigureAwait(false);

                _logger.LogDebug(
                    "Klaviyo EventsDispatcher sent event {Name} (OccurredAt={OccurredAt}, StoreAlias={StoreAlias}).",
                    work.Name,
                    work.OccurredAt,
                    storeAlias);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);

        _logger.LogDebug("Klaviyo EventsDispatcher sent {Count} events in chunk.", chunk.Length);
    }
}

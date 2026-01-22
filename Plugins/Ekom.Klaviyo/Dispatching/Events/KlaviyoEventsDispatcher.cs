using Ekom.Klaviyo.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Klaviyo.Dispatching.Events;

public interface IKlaviyoEventsDispatcher
{
    ValueTask EnqueueAsync(KlaviyoEventWork work, CancellationToken ct = default);
}

/// <summary>
/// A single queued event work item. Payload is the domain object you later map to Klaviyo.
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

        // Optional store filtering (only applies if your event work has StoreAlias set)
        if (_opt.Stores is { Count: > 0 })
        {
            drained = drained
                .Where(x =>
                    x.StoreAlias is null || // keep global events
                    _opt.Stores.Contains(x.StoreAlias, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        return Task.FromResult(drained);
    }

    protected override async Task HandleChunkAsync(KlaviyoEventWork[] chunk, CancellationToken ct)
    {
        // Mirror the catalog dispatcher approach: build the outbound payload list
        // and let the HTTP client throw/log; dispatcher continues on subsequent chunks.
        var payloads = new List<object>(chunk.Length);

        foreach (var e in chunk)
        {
            payloads.Add(new
            {
                name = e.Name,
                occurred_at = e.OccurredAt,
                store = e.StoreAlias,
                payload = e.Payload
            });
        }

        await _eventsClient.TrackEventsAsync(payloads, ct);

        _logger.LogDebug("Klaviyo EventsDispatcher sent {Count} events.", chunk.Length);
    }
}

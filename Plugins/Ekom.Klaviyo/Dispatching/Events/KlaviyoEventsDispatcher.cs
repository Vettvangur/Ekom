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
/// Payload is already mapped to Klaviyo Events API schema.
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
        var events = chunk.Select(x => x.Payload).ToList();

        await _eventsClient.TrackEventsAsync(events, chunk.FirstOrDefault()?.StoreAlias ?? "", ct);

        _logger.LogDebug("Klaviyo EventsDispatcher sent {Count} events.", chunk.Length);
    }
}

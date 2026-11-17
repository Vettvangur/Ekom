using Ekom.Algolia.Models.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Ekom.Algolia.Services;

/// <summary>Accepts Insights events without calling Algolia, so the caller's request is not held up.</summary>
internal sealed class AlgoliaInsightsQueue : IAlgoliaInsightsClient
{
    internal const int DefaultCapacity = 10_000;

    private readonly Channel<AlgoliaInsightsEvent> _channel;
    private readonly ILogger<AlgoliaInsightsQueue> _logger;

    public AlgoliaInsightsQueue(ILogger<AlgoliaInsightsQueue> logger, int capacity = DefaultCapacity)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<AlgoliaInsightsEvent>(new BoundedChannelOptions(capacity > 0 ? capacity : DefaultCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
    }

    public ChannelReader<AlgoliaInsightsEvent> Reader => _channel.Reader;

    public Task SendEventsAsync(IReadOnlyCollection<AlgoliaInsightsEvent> events, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var dropped = 0;
        foreach (var evt in events)
        {
            // The event is sent later, so it keeps the time it happened.
            if (!_channel.Writer.TryWrite(evt.Timestamp == null ? WithTimestamp(evt, now) : evt))
                dropped++;
        }

        if (dropped > 0)
            _logger.LogWarning("Algolia Insights queue is full; {Count} events were dropped.", dropped);

        return Task.CompletedTask;
    }

    private static AlgoliaInsightsEvent WithTimestamp(AlgoliaInsightsEvent evt, DateTimeOffset timestamp) => new()
    {
        EventType = evt.EventType,
        EventName = evt.EventName,
        UserToken = evt.UserToken,
        Index = evt.Index,
        ObjectIds = evt.ObjectIds,
        EventSubtype = evt.EventSubtype,
        QueryId = evt.QueryId,
        Currency = evt.Currency,
        Timestamp = timestamp,
        ObjectData = evt.ObjectData,
    };
}

/// <summary>Sends queued Insights events to Algolia in the background.</summary>
internal sealed class AlgoliaInsightsWorker(
    AlgoliaInsightsQueue queue,
    AlgoliaInsightsClient client,
    ILogger<AlgoliaInsightsWorker> logger) : BackgroundService
{
    /// <summary>The Insights API accepts at most 1,000 events per request.</summary>
    internal const int MaxEventsPerRequest = 1000;

    private static readonly TimeSpan ShutdownFlushTimeout = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await queue.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
                await SendPendingAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        // Events accepted just before shutdown, such as a purchase, still get one short attempt.
        using var flush = new CancellationTokenSource(ShutdownFlushTimeout);
        try
        {
            await SendPendingAsync(flush.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Algolia Insights events were still queued when the application stopped.");
        }
    }

    internal async Task SendPendingAsync(CancellationToken ct)
    {
        var batch = new List<AlgoliaInsightsEvent>();
        while (queue.Reader.TryRead(out var evt))
        {
            batch.Add(evt);
            if (batch.Count < MaxEventsPerRequest)
                continue;

            await SendAsync(batch, ct).ConfigureAwait(false);
            batch = new List<AlgoliaInsightsEvent>();
        }

        if (batch.Count > 0)
            await SendAsync(batch, ct).ConfigureAwait(false);
    }

    private async Task SendAsync(List<AlgoliaInsightsEvent> batch, CancellationToken ct)
    {
        try
        {
            await client.SendEventsAsync(batch, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Algolia Insights request failed; {Count} events were dropped.", batch.Count);
        }
    }
}

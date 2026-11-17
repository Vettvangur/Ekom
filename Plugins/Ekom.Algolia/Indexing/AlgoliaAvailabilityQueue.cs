using Ekom.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace Ekom.Algolia.Indexing;

/// <summary>Holds stock changes until the worker updates the indexed availability, in the order they happened.</summary>
internal sealed class AlgoliaAvailabilityQueue
{
    private readonly Channel<StockChangedEventArgs> _channel;

    public AlgoliaAvailabilityQueue(IOptions<AlgoliaOptions> options)
    {
        var capacity = options.Value.Indexing.Dispatching.MaxQueueSize;
        _channel = Channel.CreateBounded<StockChangedEventArgs>(new BoundedChannelOptions(capacity > 0 ? capacity : 10_000)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
    }

    public ChannelReader<StockChangedEventArgs> Reader => _channel.Reader;

    public bool TryEnqueue(StockChangedEventArgs change) => _channel.Writer.TryWrite(change);
}

/// <summary>Applies queued stock changes to Algolia in the background.</summary>
internal sealed class AlgoliaAvailabilityWorker(
    AlgoliaAvailabilityQueue queue,
    IAlgoliaAvailabilityUpdateService service,
    ILogger<AlgoliaAvailabilityWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await queue.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
                await ProcessPendingAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    internal async Task ProcessPendingAsync(CancellationToken ct)
    {
        while (queue.Reader.TryRead(out var change))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await service.UpdateAsync(change, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Algolia availability update failed for stock item {Key}.", change.Key);
            }
        }
    }
}

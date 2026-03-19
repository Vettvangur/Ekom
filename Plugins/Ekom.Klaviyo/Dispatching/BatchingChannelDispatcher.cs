using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Ekom.Klaviyo.Dispatching;

/// <summary>
/// Generic background dispatcher using a bounded Channel with batching and limited concurrency.
/// Supports optional coalescing (last-write-wins) via a key selector.
/// </summary>
internal abstract class BatchingChannelDispatcher<TWork> : BackgroundService
{
    private readonly ILogger _logger;
    private readonly Channel<TWork> _channel;
    private readonly KlaviyoDispatcherOptions _dispatch;
    private readonly string _name;

    protected BatchingChannelDispatcher(
        string name,
        KlaviyoDispatcherOptions dispatch,
        ILogger logger)
    {
        _name = name;
        _dispatch = dispatch;
        _logger = logger;

        _channel = Channel.CreateBounded<TWork>(new BoundedChannelOptions(_dispatch.MaxQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ValueTask EnqueueAsync(TWork work, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(work, ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var flushDelay = TimeSpan.FromSeconds(Math.Max(1, _dispatch.FlushIntervalSeconds));
        var maxBatch = _dispatch.MaxBatchSize <= 0 ? 100 : _dispatch.MaxBatchSize;
        var maxConcurrency = _dispatch.MaxConcurrency <= 0 ? 1 : _dispatch.MaxConcurrency;

        _logger.LogInformation(
            "Klaviyo {Dispatcher} started. MaxQueueSize={MaxQueueSize}, FlushIntervalSeconds={FlushIntervalSeconds}, MaxBatchSize={MaxBatchSize}, MaxConcurrency={MaxConcurrency}",
            _name, _dispatch.MaxQueueSize, _dispatch.FlushIntervalSeconds, maxBatch, maxConcurrency);

        // Semaphore to limit concurrent sends
        using var throttler = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var hasItem = await _channel.Reader.WaitToReadAsync(stoppingToken);
                if (!hasItem)
                    continue;

                // Coalesce burst into a batch window
                await Task.Delay(flushDelay, stoppingToken);

                // Drain items into list (optionally coalesced)
                var drained = Drain(maxBatch * 10); // allow a burst window
                if (drained.Count == 0)
                    continue;

                // Transform (e.g., map/fetch) and filter
                var ready = await PrepareBatchAsync(drained, stoppingToken);
                if (ready.Count == 0)
                    continue;

                foreach (var chunk in ready.Chunk(maxBatch))
                {
                    await throttler.WaitAsync(stoppingToken);

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await HandleChunkAsync(chunk, stoppingToken);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                            // ignore
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Klaviyo {Dispatcher}: failed processing chunk size {ChunkSize}", _name, chunk.Length);
                        }
                        finally
                        {
                            throttler.Release();
                        }
                    }, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Klaviyo {Dispatcher}: loop crashed; retrying in 2 seconds.", _name);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }

        _logger.LogInformation("Klaviyo {Dispatcher} stopped.", _name);
    }

    private List<TWork> Drain(int maxDrain)
    {
        var list = new List<TWork>(Math.Min(maxDrain, 1024));

        while (list.Count < maxDrain && _channel.Reader.TryRead(out var w))
        {
            list.Add(w);
        }

        return list;
    }

    /// <summary>
    /// Optional pre-processing stage: coalesce/fetch/map/filter.
    /// Default: return input as-is.
    /// </summary>
    protected virtual Task<List<TWork>> PrepareBatchAsync(List<TWork> drained, CancellationToken ct)
        => Task.FromResult(drained);

    /// <summary>
    /// Called per-chunk (already batched). Implement the actual Klaviyo API calls here.
    /// </summary>
    protected abstract Task HandleChunkAsync(TWork[] chunk, CancellationToken ct);
}

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Algolia.Indexing;

internal sealed class AlgoliaProductIndexWorker : BackgroundService
{
    private readonly IAlgoliaProductIndexQueue _queue;
    private readonly AlgoliaDispatcherOptions _dispatch;
    private readonly AlgoliaProductIndexExecutor _executor;
    private readonly ILogger<AlgoliaProductIndexWorker> _logger;

    public AlgoliaProductIndexWorker(
        IAlgoliaProductIndexQueue queue,
        IOptions<AlgoliaOptions> options,
        AlgoliaProductIndexExecutor executor,
        ILogger<AlgoliaProductIndexWorker> logger)
    {
        _queue = queue;
        _dispatch = options.Value.Indexing.Dispatching;
        _executor = executor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var flushDelay = TimeSpan.FromSeconds(Math.Max(1, _dispatch.FlushIntervalSeconds));
        var maxBatch = _dispatch.MaxBatchSize <= 0 ? 100 : _dispatch.MaxBatchSize;
        var maxConcurrency = _dispatch.MaxConcurrency <= 0 ? 1 : _dispatch.MaxConcurrency;

        _logger.LogInformation(
            "Algolia Product Indexer started. MaxQueueSize={MaxQueueSize}, FlushIntervalSeconds={FlushIntervalSeconds}, MaxBatchSize={MaxBatchSize}, MaxConcurrency={MaxConcurrency}",
            _dispatch.MaxQueueSize,
            _dispatch.FlushIntervalSeconds,
            maxBatch,
            maxConcurrency);

        using var throttler = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var hasItem = await _queue.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false);
                if (!hasItem)
                    continue;

                await Task.Delay(flushDelay, stoppingToken).ConfigureAwait(false);

                var drained = Drain(maxBatch * 10);
                if (drained.Count == 0)
                    continue;

                foreach (var chunk in drained.Chunk(maxBatch))
                {
                    await throttler.WaitAsync(stoppingToken).ConfigureAwait(false);

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _executor.HandleAsync(chunk, stoppingToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Algolia Product Indexer failed processing chunk size {ChunkSize}", chunk.Length);
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
                _logger.LogError(ex, "Algolia Product Indexer loop crashed; retrying in 2 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("Algolia Product Indexer stopped.");
    }

    private List<AlgoliaProductIndexJob> Drain(int maxDrain)
    {
        var list = new List<AlgoliaProductIndexJob>(Math.Min(maxDrain, 1024));

        while (list.Count < maxDrain && _queue.Reader.TryRead(out var job))
        {
            list.Add(job);
        }

        return list;
    }
}

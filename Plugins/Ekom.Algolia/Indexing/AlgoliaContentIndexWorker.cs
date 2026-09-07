using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Algolia.Indexing;

internal sealed class AlgoliaContentIndexWorker : BackgroundService
{
    private readonly IAlgoliaContentIndexQueue _queue;
    private readonly AlgoliaDispatcherOptions _dispatch;
    private readonly AlgoliaContentIndexExecutor _executor;
    private readonly ILogger<AlgoliaContentIndexWorker> _logger;

    public AlgoliaContentIndexWorker(
        IAlgoliaContentIndexQueue queue,
        IOptions<AlgoliaOptions> options,
        AlgoliaContentIndexExecutor executor,
        ILogger<AlgoliaContentIndexWorker> logger)
    {
        _queue = queue;
        _dispatch = options.Value.ContentIndexing.Dispatching;
        _executor = executor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var flushDelay = TimeSpan.FromSeconds(Math.Max(1, _dispatch.FlushIntervalSeconds));
        var maxBatch = _dispatch.MaxBatchSize <= 0 ? 100 : _dispatch.MaxBatchSize;

        _logger.LogInformation("Algolia Content Indexer started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var hasItem = await _queue.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false);
                if (!hasItem)
                    continue;

                await Task.Delay(flushDelay, stoppingToken).ConfigureAwait(false);

                var drained = Drain(maxBatch * 10);
                foreach (var chunk in drained.Chunk(maxBatch))
                {
                    try
                    {
                        await _executor.HandleAsync(chunk, stoppingToken).ConfigureAwait(false);
                        CompleteJobs(chunk);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        CompleteJobs(chunk, canceled: true);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        CompleteJobs(chunk, exception: ex);
                        throw;
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Algolia Content Indexer loop crashed; retrying in 2 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("Algolia Content Indexer stopped.");
    }

    private List<AlgoliaContentIndexJob> Drain(int maxDrain)
    {
        var list = new List<AlgoliaContentIndexJob>(Math.Min(maxDrain, 1024));
        while (list.Count < maxDrain && _queue.Reader.TryRead(out var job))
            list.Add(job);

        return list;
    }

    private static void CompleteJobs(
        IEnumerable<AlgoliaContentIndexJob> jobs,
        Exception? exception = null,
        bool canceled = false)
    {
        foreach (var job in jobs)
        {
            if (canceled)
            {
                job.Completion?.TrySetCanceled();
            }
            else if (exception is not null)
            {
                job.Completion?.TrySetException(exception);
            }
            else
            {
                job.Completion?.TrySetResult();
            }
        }
    }
}

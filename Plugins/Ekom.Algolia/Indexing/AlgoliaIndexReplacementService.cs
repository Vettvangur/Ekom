using Algolia.Search.Clients;
using Algolia.Search.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Ekom.Algolia.Indexing;

internal sealed class AlgoliaIndexReplacementService
{
    private const int DefaultMaxRetries = 800;

    private readonly ISearchClient _client;
    private readonly AlgoliaOptions _options;
    private readonly ILogger<AlgoliaIndexReplacementService> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _indexLocks = new(StringComparer.Ordinal);

    public AlgoliaIndexReplacementService(
        ISearchClient client,
        IOptions<AlgoliaOptions> options,
        ILogger<AlgoliaIndexReplacementService> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ReplaceAllAsync<T>(
        string indexName,
        IReadOnlyCollection<T> records,
        int batchSize,
        CancellationToken ct)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
        ArgumentNullException.ThrowIfNull(records);

        var indexLock = _indexLocks.GetOrAdd(indexName, static _ => new SemaphoreSlim(1, 1));
        await indexLock.WaitAsync(ct).ConfigureAwait(false);

        var stopwatch = Stopwatch.StartNew();
        var maxRetries = _options.Replacement.MaxRetries > 0
            ? _options.Replacement.MaxRetries
            : DefaultMaxRetries;
        var effectiveBatchSize = batchSize > 0 ? batchSize : 1000;
        var chunkedOptions = new ChunkedHelperOptions { MaxRetries = maxRetries };

        try
        {
            var indexExists = await _client.IndexExistsAsync(indexName, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Algolia {Operation} index {IndexName}. Records={RecordCount} BatchSize={BatchSize} MaxRetries={MaxRetries}",
                indexExists ? "replacing" : "creating",
                indexName,
                records.Count,
                effectiveBatchSize,
                maxRetries);

            if (indexExists)
            {
                await _client.ReplaceAllObjectsAsync(
                    indexName: indexName,
                    objects: records,
                    batchSize: effectiveBatchSize,
                    scopes: null,
                    options: null,
                    cancellationToken: ct,
                    chunkedOptions: chunkedOptions).ConfigureAwait(false);
            }
            else
            {
                await _client.SaveObjectsAsync(
                    indexName: indexName,
                    objects: records,
                    waitForTasks: true,
                    batchSize: effectiveBatchSize,
                    options: null,
                    cancellationToken: ct,
                    chunkedOptions: chunkedOptions).ConfigureAwait(false);
            }

            _logger.LogInformation(
                "Algolia index {IndexName} {Operation} completed in {DurationSeconds:F2} seconds. Records={RecordCount}",
                indexName,
                indexExists ? "replacement" : "creation",
                stopwatch.Elapsed.TotalSeconds,
                records.Count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Algolia index operation failed for {IndexName} after {DurationSeconds:F2} seconds. Records={RecordCount} BatchSize={BatchSize} MaxRetries={MaxRetries}",
                indexName,
                stopwatch.Elapsed.TotalSeconds,
                records.Count,
                effectiveBatchSize,
                maxRetries);
            throw;
        }
        finally
        {
            indexLock.Release();
        }
    }
}

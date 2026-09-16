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
    private readonly IAlgoliaTransformationWriteService _transformationWrites;
    private readonly AlgoliaOptions _options;
    private readonly ILogger<AlgoliaIndexReplacementService> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _indexLocks = new(StringComparer.Ordinal);

    public AlgoliaIndexReplacementService(
        ISearchClient client,
        IAlgoliaTransformationWriteService transformationWrites,
        IOptions<AlgoliaOptions> options,
        ILogger<AlgoliaIndexReplacementService> logger)
    {
        _client = client;
        _transformationWrites = transformationWrites;
        _options = options.Value;
        _logger = logger;
    }

    public Task ReplaceAllAsync<T>(
        string indexName,
        IReadOnlyCollection<T> records,
        int batchSize,
        CancellationToken ct)
        where T : class
        => ReplaceAllAsync(indexName, records, batchSize, useTransformation: false, ct);

    public async Task ReplaceAllAsync<T>(
        string indexName,
        IReadOnlyCollection<T> records,
        int batchSize,
        bool useTransformation,
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
        var operationBatchSize = useTransformation
            ? _transformationWrites.GetEffectiveBatchSize(effectiveBatchSize)
            : effectiveBatchSize;
        var chunkedOptions = new ChunkedHelperOptions { MaxRetries = maxRetries };

        try
        {
            var indexExists = await _client.IndexExistsAsync(indexName, ct).ConfigureAwait(false);

            _logger.LogDebug(
                "Algolia index record replacement started. Operation={Operation} IndexName={IndexName} Records={RecordCount} BatchSize={BatchSize} MaxRetries={MaxRetries}",
                indexExists ? "replacing" : "creating",
                indexName,
                records.Count,
                operationBatchSize,
                maxRetries);

            if (indexExists)
            {
                if (useTransformation)
                {
                    await _transformationWrites.ReplaceAllAsync(
                        indexName,
                        records,
                        effectiveBatchSize,
                        chunkedOptions,
                        ct).ConfigureAwait(false);
                }
                else
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
            }
            else
            {
                if (useTransformation)
                {
                    await _transformationWrites.SaveAsync(
                        indexName,
                        records,
                        effectiveBatchSize,
                        chunkedOptions,
                        ct).ConfigureAwait(false);
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
            }

            _logger.LogDebug(
                "Algolia index record replacement completed. Operation={Operation} IndexName={IndexName} Records={RecordCount} DurationMilliseconds={DurationMilliseconds}",
                indexExists ? "replacement" : "creation",
                indexName,
                records.Count,
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Algolia index record replacement failed. IndexName={IndexName} DurationSeconds={DurationSeconds:F2} Records={RecordCount} BatchSize={BatchSize} MaxRetries={MaxRetries}",
                indexName,
                stopwatch.Elapsed.TotalSeconds,
                records.Count,
                operationBatchSize,
                maxRetries);
            throw;
        }
        finally
        {
            indexLock.Release();
        }
    }
}

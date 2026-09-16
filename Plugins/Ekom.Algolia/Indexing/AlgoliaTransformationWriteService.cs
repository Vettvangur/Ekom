using Algolia.Search.Clients;
using Algolia.Search.Exceptions;
using Algolia.Search.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Sockets;

namespace Ekom.Algolia.Indexing;

internal interface IAlgoliaTransformationWriteService
{
    int GetEffectiveBatchSize(int configuredBatchSize);

    Task SaveAsync<T>(
        string indexName,
        IReadOnlyCollection<T> records,
        int batchSize,
        ChunkedHelperOptions? chunkedOptions,
        CancellationToken ct)
        where T : class;

    Task ReplaceAllAsync<T>(
        string indexName,
        IReadOnlyCollection<T> records,
        int batchSize,
        ChunkedHelperOptions chunkedOptions,
        CancellationToken ct)
        where T : class;
}

internal sealed class AlgoliaTransformationWriteService : IAlgoliaTransformationWriteService
{
    private const int DefaultMaxBatchSize = 250;
    private const int DefaultMaxAttempts = 3;
    private const int DefaultRetryBaseDelayMilliseconds = 1000;
    private const int MaximumRetryDelayMilliseconds = 30_000;

    private readonly ISearchClient _client;
    private readonly AlgoliaTransformationWriteOptions _options;
    private readonly ILogger<AlgoliaTransformationWriteService> _logger;

    public AlgoliaTransformationWriteService(
        ISearchClient client,
        IOptions<AlgoliaOptions> options,
        ILogger<AlgoliaTransformationWriteService> logger)
    {
        _client = client;
        _options = options.Value.Transformation;
        _logger = logger;
    }

    public int GetEffectiveBatchSize(int configuredBatchSize)
    {
        var batchSize = configuredBatchSize > 0 ? configuredBatchSize : 1000;
        var maximumBatchSize = _options.MaxBatchSize > 0
            ? _options.MaxBatchSize
            : DefaultMaxBatchSize;

        return Math.Min(batchSize, maximumBatchSize);
    }

    public async Task SaveAsync<T>(
        string indexName,
        IReadOnlyCollection<T> records,
        int batchSize,
        ChunkedHelperOptions? chunkedOptions,
        CancellationToken ct)
        where T : class
    {
        var effectiveBatchSize = GetEffectiveBatchSize(batchSize);

        foreach (var batch in records.Chunk(effectiveBatchSize))
        {
            await ExecuteWithRetryAsync(
                indexName,
                "save",
                batch.Length,
                effectiveBatchSize,
                () => _client.SaveObjectsWithTransformationAsync(
                    indexName: indexName,
                    objects: batch,
                    waitForTasks: true,
                    batchSize: effectiveBatchSize,
                    options: null,
                    cancellationToken: ct,
                    chunkedOptions: chunkedOptions),
                ct).ConfigureAwait(false);
        }
    }

    public Task ReplaceAllAsync<T>(
        string indexName,
        IReadOnlyCollection<T> records,
        int batchSize,
        ChunkedHelperOptions chunkedOptions,
        CancellationToken ct)
        where T : class
    {
        var effectiveBatchSize = GetEffectiveBatchSize(batchSize);
        return ExecuteWithRetryAsync(
            indexName,
            "replace",
            records.Count,
            effectiveBatchSize,
            () => _client.ReplaceAllObjectsWithTransformationAsync(
                indexName: indexName,
                objects: records,
                batchSize: effectiveBatchSize,
                scopes: null,
                options: null,
                cancellationToken: ct,
                chunkedOptions: chunkedOptions),
            ct);
    }

    private async Task ExecuteWithRetryAsync(
        string indexName,
        string operation,
        int recordCount,
        int batchSize,
        Func<Task> action,
        CancellationToken ct)
    {
        var maxAttempts = _options.MaxAttempts > 0
            ? _options.MaxAttempts
            : DefaultMaxAttempts;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await action().ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransient(ex))
            {
                var delay = GetRetryDelay(attempt);
                _logger.LogWarning(
                    "Transient Algolia transformation {Operation} failure for {IndexName}. Attempt={Attempt}/{MaxAttempts} Records={RecordCount} BatchSize={BatchSize} RetryDelayMilliseconds={RetryDelayMilliseconds} ExceptionType={ExceptionType}",
                    operation,
                    indexName,
                    attempt,
                    maxAttempts,
                    recordCount,
                    batchSize,
                    delay.TotalMilliseconds,
                    ex.GetType().Name);

                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
    }

    private TimeSpan GetRetryDelay(int failedAttempt)
    {
        var baseDelay = _options.RetryBaseDelayMilliseconds >= 0
            ? _options.RetryBaseDelayMilliseconds
            : DefaultRetryBaseDelayMilliseconds;
        var multiplier = Math.Pow(2, failedAttempt - 1);
        var delay = Math.Min(baseDelay * multiplier, MaximumRetryDelayMilliseconds);
        return TimeSpan.FromMilliseconds(delay);
    }

    private static bool IsTransient(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is AlgoliaUnreachableHostException
                or HttpRequestException
                or IOException
                or SocketException)
            {
                return true;
            }
        }

        return false;
    }
}

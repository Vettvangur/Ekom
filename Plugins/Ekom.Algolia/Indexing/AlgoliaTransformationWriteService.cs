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
    private readonly AlgoliaOptions _options;
    private readonly ILogger<AlgoliaTransformationWriteService> _logger;

    public AlgoliaTransformationWriteService(
        ISearchClient client,
        IOptions<AlgoliaOptions> options,
        ILogger<AlgoliaTransformationWriteService> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public int GetEffectiveBatchSize(int configuredBatchSize)
    {
        var batchSize = configuredBatchSize > 0 ? configuredBatchSize : 1000;
        var maximumBatchSize = _options.Transformation.MaxBatchSize > 0
            ? _options.Transformation.MaxBatchSize
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
        var completedBatches = 0;
        var outcomeUncertain = false;

        foreach (var batch in records.Chunk(effectiveBatchSize))
        {
            try
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
                    () => outcomeUncertain = true,
                    ct).ConfigureAwait(false);

                completedBatches++;
            }
            catch (AlgoliaApiException ex) when (completedBatches == 0
                && !outcomeUncertain
                && IsMissingTransformationTask(ex, indexName))
            {
                LogSearchApiFallback(indexName, "save", records.Count, effectiveBatchSize);
                await _client.SaveObjectsAsync(
                    indexName: indexName,
                    objects: records,
                    waitForTasks: true,
                    batchSize: effectiveBatchSize,
                    options: null,
                    cancellationToken: ct,
                    chunkedOptions: chunkedOptions).ConfigureAwait(false);
                return;
            }
        }
    }

    public async Task ReplaceAllAsync<T>(
        string indexName,
        IReadOnlyCollection<T> records,
        int batchSize,
        ChunkedHelperOptions chunkedOptions,
        CancellationToken ct)
        where T : class
    {
        var effectiveBatchSize = GetEffectiveBatchSize(batchSize);
        var outcomeUncertain = false;

        try
        {
            await ExecuteWithRetryAsync(
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
                () => outcomeUncertain = true,
                ct).ConfigureAwait(false);
        }
        catch (AlgoliaApiException ex) when (!outcomeUncertain && IsMissingTransformationTask(ex, indexName))
        {
            LogSearchApiFallback(indexName, "replace", records.Count, effectiveBatchSize);
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

    internal static bool IsMissingTransformationTask(AlgoliaApiException exception, string indexName)
    {
        if (exception.HttpErrorCode != 404 || string.IsNullOrWhiteSpace(indexName))
            return false;

        var response = exception.ResponseBody ?? exception.Message;
        if (string.IsNullOrWhiteSpace(response))
            return false;

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(response);
            var root = document.RootElement;
            var code = TryGetErrorCode(root);
            var message = root.TryGetProperty("message", out var messageElement)
                && messageElement.ValueKind == System.Text.Json.JsonValueKind.String
                    ? messageElement.GetString()
                    : null;

            return string.Equals(code, "resource_not_found", StringComparison.OrdinalIgnoreCase)
                && IsMissingTaskMessage(message, indexName);
        }
        catch (System.Text.Json.JsonException)
        {
            return response.Contains("resource_not_found", StringComparison.OrdinalIgnoreCase)
                && IsMissingTaskMessage(response, indexName);
        }
    }

    private static string? TryGetErrorCode(System.Text.Json.JsonElement root)
    {
        if (root.TryGetProperty("code", out var code)
            && code.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            return code.GetString();
        }

        if (root.TryGetProperty("error", out var error)
            && error.ValueKind == System.Text.Json.JsonValueKind.Object
            && error.TryGetProperty("code", out code)
            && code.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            return code.GetString();
        }

        return null;
    }

    private static bool IsMissingTaskMessage(string? message, string indexName)
        => string.Equals(
            message?.Trim(),
            $"cannot find task {indexName}",
            StringComparison.Ordinal);

    private void LogSearchApiFallback(string indexName, string operation, int recordCount, int batchSize)
        => _logger.LogWarning(
            "Algolia Collections task was not found for index {IndexName}; using the Search API for this {Operation} operation. Records={RecordCount} BatchSize={BatchSize} TransformationRegion={TransformationRegion}. If Collections are configured for this index, verify the Algolia application and transformation region.",
            indexName,
            operation,
            recordCount,
            batchSize,
            _options.TransformationRegion);

    private async Task ExecuteWithRetryAsync(
        string indexName,
        string operation,
        int recordCount,
        int batchSize,
        Func<Task> action,
        Action onTransientFailure,
        CancellationToken ct)
    {
        var maxAttempts = _options.Transformation.MaxAttempts > 0
            ? _options.Transformation.MaxAttempts
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
                onTransientFailure();
                var delay = GetRetryDelay(attempt);
                _logger.LogWarning(
                    "Transient Algolia transformation {Operation} failure for index {IndexName}. Attempt={Attempt}/{MaxAttempts} Records={RecordCount} BatchSize={BatchSize} RetryDelayMilliseconds={RetryDelayMilliseconds} ExceptionType={ExceptionType}",
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
        var baseDelay = _options.Transformation.RetryBaseDelayMilliseconds >= 0
            ? _options.Transformation.RetryBaseDelayMilliseconds
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

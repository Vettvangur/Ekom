using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Algolia.Indexing;

public interface IAlgoliaProductIndexService
{
    Task EnqueueProductAsync(string storeAlias, Guid productKey, bool isPublished, CancellationToken ct = default);
    Task EnqueueProductsAsync(string storeAlias, IReadOnlyCollection<Guid> productKeys, bool isPublished, CancellationToken ct = default);
    Task RebuildStoreAsync(string storeAlias, CancellationToken ct = default);
    Task RebuildAllAsync(CancellationToken ct = default);
}

internal sealed class AlgoliaProductIndexService : IAlgoliaProductIndexService
{
    private readonly IAlgoliaProductIndexQueue _queue;
    private readonly AlgoliaOptions _options;
    private readonly ILogger<AlgoliaProductIndexService> _logger;

    public AlgoliaProductIndexService(
        IAlgoliaProductIndexQueue queue,
        IOptions<AlgoliaOptions> options,
        ILogger<AlgoliaProductIndexService> logger)
    {
        _queue = queue;
        _options = options.Value;
        _logger = logger;
    }

    public Task EnqueueProductAsync(string storeAlias, Guid productKey, bool isPublished, CancellationToken ct = default)
        => EnqueueProductsAsync(storeAlias, new[] { productKey }, isPublished, ct);

    public Task EnqueueProductsAsync(string storeAlias, IReadOnlyCollection<Guid> productKeys, bool isPublished, CancellationToken ct = default)
    {
        if (!_options.Enabled || !_options.Indexing.Enabled || !_options.Indexing.Products)
        {
            _logger.LogDebug(
                "Algolia enqueue skipped because indexing is disabled. Store={Store}, Published={IsPublished}, Keys={Count}",
                storeAlias,
                isPublished,
                productKeys.Count);
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(storeAlias) || productKeys.Count == 0)
        {
            _logger.LogDebug(
                "Algolia enqueue skipped because store alias or product keys were missing. Store={Store}, Published={IsPublished}, Keys={Count}",
                storeAlias,
                isPublished,
                productKeys.Count);
            return Task.CompletedTask;
        }

        var type = isPublished ? AlgoliaProductIndexJobType.Upsert : AlgoliaProductIndexJobType.Delete;
        var job = new AlgoliaProductIndexJob(type, storeAlias, productKeys);

        _logger.LogDebug(
            "Algolia queueing {Type} job for store {Store} with {Count} product keys.",
            type,
            storeAlias,
            productKeys.Count);

        if (_queue.TryEnqueue(job))
        {
            _logger.LogDebug(
                "Algolia queued {Type} job for store {Store} with {Count} product keys using TryEnqueue.",
                type,
                storeAlias,
                productKeys.Count);
            return Task.CompletedTask;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogDebug(
                    "Algolia queue full for immediate write; waiting to enqueue {Type} job for store {Store} with {Count} product keys.",
                    type,
                    storeAlias,
                    productKeys.Count);

                await _queue.EnqueueAsync(job, ct).ConfigureAwait(false);

                _logger.LogDebug(
                    "Algolia queued {Type} job for store {Store} with {Count} product keys after waiting.",
                    type,
                    storeAlias,
                    productKeys.Count);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Algolia enqueue failed for store {Store}.", storeAlias);
            }
        }, CancellationToken.None);

        return Task.CompletedTask;
    }

    public Task RebuildStoreAsync(string storeAlias, CancellationToken ct = default)
    {
        if (!_options.Enabled || !_options.Indexing.Enabled || !_options.Indexing.Products)
            return Task.CompletedTask;

        if (string.IsNullOrWhiteSpace(storeAlias))
            return Task.CompletedTask;

        var job = new AlgoliaProductIndexJob(AlgoliaProductIndexJobType.RebuildStore, storeAlias, Array.Empty<Guid>());

        _logger.LogDebug("Algolia queueing rebuild job for store {Store}.", storeAlias);

        if (_queue.TryEnqueue(job))
            return Task.CompletedTask;

        _ = Task.Run(async () =>
        {
            try
            {
                await _queue.EnqueueAsync(job, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Algolia enqueue rebuild failed for store {Store}.", storeAlias);
            }
        }, CancellationToken.None);

        return Task.CompletedTask;
    }

    public Task RebuildAllAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled || !_options.Indexing.Enabled || !_options.Indexing.Products)
            return Task.CompletedTask;

        foreach (var store in _options.Stores)
        {
            if (string.IsNullOrWhiteSpace(store.Alias))
                continue;

            var job = new AlgoliaProductIndexJob(AlgoliaProductIndexJobType.RebuildStore, store.Alias, Array.Empty<Guid>());
            if (_queue.TryEnqueue(job))
                continue;

            _ = Task.Run(() => _queue.EnqueueAsync(job, ct).AsTask(), CancellationToken.None);
        }

        return Task.CompletedTask;
    }
}

using Ekom.Algolia.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Algolia.Indexing;

public interface IAlgoliaCategoryIndexService
{
    Task EnqueueCategoryAsync(string storeAlias, Guid categoryKey, bool isPublished, CancellationToken ct = default);
    Task EnqueueCategoriesAsync(string storeAlias, IReadOnlyCollection<Guid> categoryKeys, bool isPublished, CancellationToken ct = default);
    Task RebuildStoreAsync(string storeAlias, CancellationToken ct = default);
    Task RebuildStoreAndWaitAsync(string storeAlias, CancellationToken ct = default);
    Task RebuildAllAsync(CancellationToken ct = default);
    Task RebuildAllAndWaitAsync(CancellationToken ct = default);
}

internal sealed class AlgoliaCategoryIndexService : IAlgoliaCategoryIndexService
{
    private readonly IAlgoliaCategoryIndexQueue _queue;
    private readonly AlgoliaOptions _options;
    private readonly AlgoliaStoreResolver _storeResolver;
    private readonly ILogger<AlgoliaCategoryIndexService> _logger;

    public AlgoliaCategoryIndexService(
        IAlgoliaCategoryIndexQueue queue,
        IOptions<AlgoliaOptions> options,
        AlgoliaStoreResolver storeResolver,
        ILogger<AlgoliaCategoryIndexService> logger)
    {
        _queue = queue;
        _options = options.Value;
        _storeResolver = storeResolver;
        _logger = logger;
    }

    public Task EnqueueCategoryAsync(string storeAlias, Guid categoryKey, bool isPublished, CancellationToken ct = default)
        => EnqueueCategoriesAsync(storeAlias, new[] { categoryKey }, isPublished, ct);

    public Task EnqueueCategoriesAsync(string storeAlias, IReadOnlyCollection<Guid> categoryKeys, bool isPublished, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Task.CompletedTask;

        if (string.IsNullOrWhiteSpace(storeAlias) || categoryKeys.Count == 0)
            return Task.CompletedTask;

        var indexing = _storeResolver.Resolve(storeAlias).Indexing;
        if (!indexing.Enabled || !indexing.Categories)
            return Task.CompletedTask;

        var type = isPublished ? AlgoliaCategoryIndexJobType.Upsert : AlgoliaCategoryIndexJobType.Delete;
        var job = new AlgoliaCategoryIndexJob(type, storeAlias, categoryKeys);

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
                _logger.LogError(ex, "Algolia enqueue failed for category store {Store}.", storeAlias);
            }
        }, CancellationToken.None);

        return Task.CompletedTask;
    }

    public Task RebuildStoreAsync(string storeAlias, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Task.CompletedTask;

        if (string.IsNullOrWhiteSpace(storeAlias))
            return Task.CompletedTask;

        var indexing = _storeResolver.Resolve(storeAlias).Indexing;
        if (!indexing.Enabled || !indexing.Categories)
            return Task.CompletedTask;

        var job = new AlgoliaCategoryIndexJob(AlgoliaCategoryIndexJobType.RebuildStore, storeAlias, Array.Empty<Guid>());
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
                _logger.LogError(ex, "Algolia enqueue rebuild failed for category store {Store}.", storeAlias);
            }
        }, CancellationToken.None);

        return Task.CompletedTask;
    }

    public async Task RebuildStoreAndWaitAsync(string storeAlias, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(storeAlias))
            return;

        var indexing = _storeResolver.Resolve(storeAlias).Indexing;
        if (!indexing.Enabled || !indexing.Categories)
            return;

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var job = new AlgoliaCategoryIndexJob(
            AlgoliaCategoryIndexJobType.RebuildStore,
            storeAlias,
            Array.Empty<Guid>(),
            completion);

        await _queue.EnqueueAsync(job, ct).ConfigureAwait(false);
        await completion.Task.ConfigureAwait(false);
    }

    public Task RebuildAllAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Task.CompletedTask;

        foreach (var store in _options.Stores)
        {
            if (string.IsNullOrWhiteSpace(store.Alias))
                continue;

            var indexing = _storeResolver.Resolve(store.Alias).Indexing;
            if (!indexing.Enabled || !indexing.Categories)
                continue;

            var job = new AlgoliaCategoryIndexJob(AlgoliaCategoryIndexJobType.RebuildStore, store.Alias, Array.Empty<Guid>());
            if (_queue.TryEnqueue(job))
                continue;

            _ = Task.Run(() => _queue.EnqueueAsync(job, ct).AsTask(), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public async Task RebuildAllAndWaitAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return;

        var completions = new List<Task>();
        foreach (var store in _options.Stores)
        {
            if (string.IsNullOrWhiteSpace(store.Alias))
                continue;

            var indexing = _storeResolver.Resolve(store.Alias).Indexing;
            if (!indexing.Enabled || !indexing.Categories)
                continue;

            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var job = new AlgoliaCategoryIndexJob(
                AlgoliaCategoryIndexJobType.RebuildStore,
                store.Alias,
                Array.Empty<Guid>(),
                completion);

            await _queue.EnqueueAsync(job, ct).ConfigureAwait(false);
            completions.Add(completion.Task);
        }

        await Task.WhenAll(completions).ConfigureAwait(false);
    }
}

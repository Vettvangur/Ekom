using Ekom.Algolia.Indexing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using Xunit;

namespace Ekom.Tests.Tests;

public class AlgoliaFullIndexRebuildCoordinatorTests
{
    [Fact]
    public async Task TryStart_RejectsDuplicateRunUntilCurrentRunCompletes()
    {
        var productService = new BlockingProductIndexService();
        var categoryService = new BlockingCategoryIndexService();
        var contentService = new BlockingContentIndexService();
        var coordinator = new AlgoliaFullIndexRebuildCoordinator(
            productService,
            categoryService,
            contentService,
            NullLogger<AlgoliaFullIndexRebuildCoordinator>.Instance);

        Assert.True(coordinator.TryStart());
        Assert.False(coordinator.TryStart());

        productService.Complete();
        categoryService.Complete();
        contentService.Complete();

        Assert.True(await WaitForStartAsync(coordinator));
    }

    [Fact]
    public async Task TryStart_AllowsAnotherRunAfterCurrentRunFails()
    {
        var productService = new BlockingProductIndexService();
        var categoryService = new BlockingCategoryIndexService();
        var contentService = new BlockingContentIndexService();
        var coordinator = new AlgoliaFullIndexRebuildCoordinator(
            productService,
            categoryService,
            contentService,
            NullLogger<AlgoliaFullIndexRebuildCoordinator>.Instance);

        Assert.True(coordinator.TryStart());

        productService.Fail();
        categoryService.Complete();
        contentService.Complete();

        Assert.True(await WaitForStartAsync(coordinator));
    }

    [Fact]
    public async Task Full_Rebuild_Runs_Stages_Sequentially()
    {
        var calls = new ConcurrentQueue<string>();
        var productService = new BlockingProductIndexService(calls);
        var categoryService = new BlockingCategoryIndexService(calls);
        var contentService = new BlockingContentIndexService(calls);
        var coordinator = CreateCoordinator(productService, categoryService, contentService);

        Assert.True(coordinator.TryStart());
        Assert.True(await WaitForConditionAsync(() => calls.Count == 1));
        Assert.Equal(["products"], calls);

        productService.Complete();
        Assert.True(await WaitForConditionAsync(() => calls.Count == 2));
        Assert.Equal(["products", "categories"], calls);

        categoryService.Complete();
        Assert.True(await WaitForConditionAsync(() => calls.Count == 3));
        Assert.Equal(["products", "categories", "content"], calls);

        contentService.Complete();
    }

    [Fact]
    public async Task Full_Rebuild_Logs_Stage_And_Run_Lifecycle()
    {
        var productService = new BlockingProductIndexService();
        var categoryService = new BlockingCategoryIndexService();
        var contentService = new BlockingContentIndexService();
        var logger = new RecordingLogger<AlgoliaFullIndexRebuildCoordinator>();
        var coordinator = new AlgoliaFullIndexRebuildCoordinator(
            productService,
            categoryService,
            contentService,
            logger);

        productService.Complete();
        categoryService.Complete();
        contentService.Complete();
        Assert.True(coordinator.TryStart());

        Assert.True(await WaitForConditionAsync(
            () => logger.Messages.Any(x => x.Contains("Algolia full rebuild completed", StringComparison.Ordinal))));
        Assert.Contains(logger.Messages, x => x.Contains("Stage=products", StringComparison.Ordinal) && x.Contains("stage started", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, x => x.Contains("Stage=products", StringComparison.Ordinal) && x.Contains("stage completed", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, x => x.Contains("Stage=categories", StringComparison.Ordinal) && x.Contains("stage completed", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, x => x.Contains("Stage=content", StringComparison.Ordinal) && x.Contains("stage completed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Full_Rebuild_Continues_Remaining_Stages_After_Failure()
    {
        var calls = new ConcurrentQueue<string>();
        var productService = new BlockingProductIndexService(calls);
        var categoryService = new BlockingCategoryIndexService(calls);
        var contentService = new BlockingContentIndexService(calls);
        var coordinator = CreateCoordinator(productService, categoryService, contentService);

        Assert.True(coordinator.TryStart());
        Assert.True(await WaitForConditionAsync(() => calls.Count == 1));
        productService.Fail();

        Assert.True(await WaitForConditionAsync(() => calls.Count == 2));
        categoryService.Complete();

        Assert.True(await WaitForConditionAsync(() => calls.Count == 3));
        contentService.Complete();

        Assert.Equal(["products", "categories", "content"], calls);
    }

    [Fact]
    public async Task Full_Rebuild_Does_Not_Continue_Remaining_Stages_After_Cancellation()
    {
        var calls = new ConcurrentQueue<string>();
        var productService = new BlockingProductIndexService(calls);
        var categoryService = new BlockingCategoryIndexService(calls);
        var contentService = new BlockingContentIndexService(calls);
        var coordinator = CreateCoordinator(productService, categoryService, contentService);

        Assert.True(coordinator.TryStart());
        Assert.True(await WaitForConditionAsync(() => calls.Count == 1));
        productService.Cancel();

        await Task.Delay(50);
        Assert.Equal(["products"], calls);
        Assert.True(await WaitForStartAsync(coordinator));
    }

    [Fact]
    public async Task TryStartStore_RejectsDuplicateStoreUntilCurrentRunCompletes()
    {
        var productService = new BlockingProductIndexService();
        var categoryService = new BlockingCategoryIndexService();
        var coordinator = CreateCoordinator(productService, categoryService);

        Assert.True(coordinator.TryStartStore("Store"));
        Assert.False(coordinator.TryStartStore("store"));

        productService.CompleteStore("Store");
        categoryService.CompleteStore("Store");

        Assert.True(await WaitForStartStoreAsync(coordinator, "Store"));
        productService.CompleteStore("Store");
        categoryService.CompleteStore("Store");
    }

    [Fact]
    public async Task TryStartStore_AllowsDifferentStoresToRunConcurrently()
    {
        var productService = new BlockingProductIndexService();
        var categoryService = new BlockingCategoryIndexService();
        var coordinator = CreateCoordinator(productService, categoryService);

        Assert.True(coordinator.TryStartStore("StoreA"));
        Assert.True(coordinator.TryStartStore("StoreB"));

        productService.CompleteStore("StoreA");
        categoryService.CompleteStore("StoreA");
        productService.CompleteStore("StoreB");
        categoryService.CompleteStore("StoreB");

        Assert.True(await WaitForStartStoreAsync(coordinator, "StoreA"));
        productService.CompleteStore("StoreA");
        categoryService.CompleteStore("StoreA");
    }

    [Fact]
    public async Task Store_Rebuild_Runs_Stages_Sequentially()
    {
        var calls = new ConcurrentQueue<string>();
        var productService = new BlockingProductIndexService(calls);
        var categoryService = new BlockingCategoryIndexService(calls);
        var coordinator = CreateCoordinator(productService, categoryService);

        Assert.True(coordinator.TryStartStore("Store"));
        Assert.True(await WaitForConditionAsync(() => calls.Count == 1));
        Assert.Equal(["products:Store"], calls);

        productService.CompleteStore("Store");
        Assert.True(await WaitForConditionAsync(() => calls.Count == 2));
        Assert.Equal(["products:Store", "categories:Store"], calls);

        categoryService.CompleteStore("Store");
    }

    [Fact]
    public async Task TryStart_RejectsFullRebuildWhileStoreRebuildIsActive()
    {
        var productService = new BlockingProductIndexService();
        var categoryService = new BlockingCategoryIndexService();
        var contentService = new BlockingContentIndexService();
        var coordinator = CreateCoordinator(productService, categoryService, contentService);

        Assert.True(coordinator.TryStartStore("Store"));
        Assert.False(coordinator.TryStart());

        productService.CompleteStore("Store");
        categoryService.CompleteStore("Store");

        Assert.True(await WaitForStartAsync(coordinator));
        productService.Complete();
        categoryService.Complete();
        contentService.Complete();
    }

    [Fact]
    public void TryStartStore_RejectsStoreRebuildWhileFullRebuildIsActive()
    {
        var productService = new BlockingProductIndexService();
        var categoryService = new BlockingCategoryIndexService();
        var contentService = new BlockingContentIndexService();
        var coordinator = CreateCoordinator(productService, categoryService, contentService);

        Assert.True(coordinator.TryStart());
        Assert.False(coordinator.TryStartStore("Store"));

        productService.Complete();
        categoryService.Complete();
        contentService.Complete();
    }

    private static AlgoliaFullIndexRebuildCoordinator CreateCoordinator(
        IAlgoliaProductIndexService productService,
        IAlgoliaCategoryIndexService categoryService,
        IAlgoliaContentIndexService? contentService = null)
        => new(
            productService,
            categoryService,
            contentService ?? new BlockingContentIndexService(),
            NullLogger<AlgoliaFullIndexRebuildCoordinator>.Instance);

    private static async Task<bool> WaitForStartAsync(IAlgoliaFullIndexRebuildCoordinator coordinator)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (coordinator.TryStart())
            {
                return true;
            }

            await Task.Delay(10);
        }

        return false;
    }

    private static async Task<bool> WaitForStartStoreAsync(IAlgoliaFullIndexRebuildCoordinator coordinator, string storeAlias)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (coordinator.TryStartStore(storeAlias))
            {
                return true;
            }

            await Task.Delay(10);
        }

        return false;
    }

    private static async Task<bool> WaitForConditionAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (condition())
                return true;

            await Task.Delay(10);
        }

        return false;
    }

    private sealed class BlockingProductIndexService : IAlgoliaProductIndexService
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentQueue<string>? _calls;

        public BlockingProductIndexService(ConcurrentQueue<string>? calls = null)
        {
            _calls = calls;
        }

        public Task EnqueueProductAsync(string storeAlias, Guid productKey, bool isPublished, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnqueueProductsAsync(string storeAlias, IReadOnlyCollection<Guid> productKeys, bool isPublished, CancellationToken ct = default) => Task.CompletedTask;
        public Task RebuildStoreAsync(string storeAlias, CancellationToken ct = default) => Task.CompletedTask;
        public Task RebuildStoreAndWaitAsync(string storeAlias, CancellationToken ct = default)
        {
            _calls?.Enqueue($"products:{storeAlias}");
            return GetStoreCompletion(storeAlias).Task;
        }
        public Task RebuildAllAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RebuildAllAndWaitAsync(CancellationToken ct = default)
        {
            _calls?.Enqueue("products");
            return _completion.Task;
        }
        public void Complete() => _completion.TrySetResult();
        public void Cancel() => _completion.TrySetCanceled();
        public void CompleteStore(string storeAlias) => GetStoreCompletion(storeAlias).TrySetResult();
        public void Fail() => _completion.TrySetException(new InvalidOperationException());

        private readonly Dictionary<string, TaskCompletionSource> _storeCompletions = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _storeCompletionsLock = new();

        private TaskCompletionSource GetStoreCompletion(string storeAlias)
        {
            lock (_storeCompletionsLock)
            {
                if (_storeCompletions.TryGetValue(storeAlias, out var completion))
                    return completion;

                completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _storeCompletions[storeAlias] = completion;
                return completion;
            }
        }
    }

    private sealed class BlockingCategoryIndexService : IAlgoliaCategoryIndexService
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentQueue<string>? _calls;

        public BlockingCategoryIndexService(ConcurrentQueue<string>? calls = null)
        {
            _calls = calls;
        }

        public Task EnqueueCategoryAsync(string storeAlias, Guid categoryKey, bool isPublished, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnqueueCategoriesAsync(string storeAlias, IReadOnlyCollection<Guid> categoryKeys, bool isPublished, CancellationToken ct = default) => Task.CompletedTask;
        public Task RebuildStoreAsync(string storeAlias, CancellationToken ct = default) => Task.CompletedTask;
        public Task RebuildStoreAndWaitAsync(string storeAlias, CancellationToken ct = default)
        {
            _calls?.Enqueue($"categories:{storeAlias}");
            return GetStoreCompletion(storeAlias).Task;
        }
        public Task RebuildAllAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RebuildAllAndWaitAsync(CancellationToken ct = default)
        {
            _calls?.Enqueue("categories");
            return _completion.Task;
        }
        public void Complete() => _completion.TrySetResult();
        public void CompleteStore(string storeAlias) => GetStoreCompletion(storeAlias).TrySetResult();

        private readonly Dictionary<string, TaskCompletionSource> _storeCompletions = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _storeCompletionsLock = new();

        private TaskCompletionSource GetStoreCompletion(string storeAlias)
        {
            lock (_storeCompletionsLock)
            {
                if (_storeCompletions.TryGetValue(storeAlias, out var completion))
                    return completion;

                completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _storeCompletions[storeAlias] = completion;
                return completion;
            }
        }
    }

    private sealed class BlockingContentIndexService : IAlgoliaContentIndexService
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentQueue<string>? _calls;

        public BlockingContentIndexService(ConcurrentQueue<string>? calls = null)
        {
            _calls = calls;
        }

        public Task UpdateByIdsAsync(IReadOnlyCollection<int> nodeIds, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteByKeysAsync(IReadOnlyCollection<Guid> nodeKeys, CancellationToken ct = default) => Task.CompletedTask;
        public Task RebuildAsync(string? indexName = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task RebuildAndWaitAsync(string? indexName = null, CancellationToken ct = default)
        {
            _calls?.Enqueue("content");
            return _completion.Task;
        }
        public void Complete() => _completion.TrySetResult();
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Enqueue(formatter(state, exception));
    }
}

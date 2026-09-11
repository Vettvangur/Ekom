using Ekom.Algolia.Indexing;
using Microsoft.Extensions.Logging.Abstractions;
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

    private sealed class BlockingProductIndexService : IAlgoliaProductIndexService
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task EnqueueProductAsync(string storeAlias, Guid productKey, bool isPublished, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnqueueProductsAsync(string storeAlias, IReadOnlyCollection<Guid> productKeys, bool isPublished, CancellationToken ct = default) => Task.CompletedTask;
        public Task RebuildStoreAsync(string storeAlias, CancellationToken ct = default) => Task.CompletedTask;
        public Task RebuildStoreAndWaitAsync(string storeAlias, CancellationToken ct = default) => GetStoreCompletion(storeAlias).Task;
        public Task RebuildAllAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RebuildAllAndWaitAsync(CancellationToken ct = default) => _completion.Task;
        public void Complete() => _completion.TrySetResult();
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

        public Task EnqueueCategoryAsync(string storeAlias, Guid categoryKey, bool isPublished, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnqueueCategoriesAsync(string storeAlias, IReadOnlyCollection<Guid> categoryKeys, bool isPublished, CancellationToken ct = default) => Task.CompletedTask;
        public Task RebuildStoreAsync(string storeAlias, CancellationToken ct = default) => Task.CompletedTask;
        public Task RebuildStoreAndWaitAsync(string storeAlias, CancellationToken ct = default) => GetStoreCompletion(storeAlias).Task;
        public Task RebuildAllAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RebuildAllAndWaitAsync(CancellationToken ct = default) => _completion.Task;
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

        public Task UpdateByIdsAsync(IReadOnlyCollection<int> nodeIds, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteByKeysAsync(IReadOnlyCollection<Guid> nodeKeys, CancellationToken ct = default) => Task.CompletedTask;
        public Task RebuildAsync(string? indexName = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task RebuildAndWaitAsync(string? indexName = null, CancellationToken ct = default) => _completion.Task;
        public void Complete() => _completion.TrySetResult();
    }
}

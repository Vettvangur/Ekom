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

    private sealed class BlockingProductIndexService : IAlgoliaProductIndexService
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task EnqueueProductAsync(string storeAlias, Guid productKey, bool isPublished, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnqueueProductsAsync(string storeAlias, IReadOnlyCollection<Guid> productKeys, bool isPublished, CancellationToken ct = default) => Task.CompletedTask;
        public Task RebuildStoreAsync(string storeAlias, CancellationToken ct = default) => Task.CompletedTask;
        public Task RebuildAllAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RebuildAllAndWaitAsync(CancellationToken ct = default) => _completion.Task;
        public void Complete() => _completion.TrySetResult();
        public void Fail() => _completion.TrySetException(new InvalidOperationException());
    }

    private sealed class BlockingCategoryIndexService : IAlgoliaCategoryIndexService
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task EnqueueCategoryAsync(string storeAlias, Guid categoryKey, bool isPublished, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnqueueCategoriesAsync(string storeAlias, IReadOnlyCollection<Guid> categoryKeys, bool isPublished, CancellationToken ct = default) => Task.CompletedTask;
        public Task RebuildStoreAsync(string storeAlias, CancellationToken ct = default) => Task.CompletedTask;
        public Task RebuildAllAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RebuildAllAndWaitAsync(CancellationToken ct = default) => _completion.Task;
        public void Complete() => _completion.TrySetResult();
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

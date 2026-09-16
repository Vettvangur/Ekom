using Ekom.Algolia;
using Ekom.Algolia.Indexing;
using Ekom.Algolia.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Threading.Channels;
using Xunit;

namespace Ekom.Tests.Tests;

public class AlgoliaIndexServiceTests
{
    [Fact]
    public async Task Product_Store_Can_Enable_Indexing_When_Global_Indexing_Is_Disabled()
    {
        var options = CreateOptions(enabled: true);
        var queue = new ProductQueue();
        var service = new AlgoliaProductIndexService(
            queue,
            Options.Create(options),
            CreateResolver(options),
            NullLogger<AlgoliaProductIndexService>.Instance);
        var key = Guid.NewGuid();

        await service.EnqueueProductAsync("Store", key, isPublished: true);

        var job = Assert.Single(queue.Jobs);
        Assert.Equal("Store", job.StoreAlias);
        Assert.Equal(AlgoliaProductIndexJobType.Upsert, job.Type);
        Assert.Equal([key], job.ProductKeys);
    }

    [Fact]
    public async Task Category_Store_Can_Enable_Indexing_When_Global_Indexing_Is_Disabled()
    {
        var options = CreateOptions(enabled: true);
        var queue = new CategoryQueue();
        var service = new AlgoliaCategoryIndexService(
            queue,
            Options.Create(options),
            CreateResolver(options),
            NullLogger<AlgoliaCategoryIndexService>.Instance);
        var key = Guid.NewGuid();

        await service.EnqueueCategoryAsync("Store", key, isPublished: true);

        var job = Assert.Single(queue.Jobs);
        Assert.Equal("Store", job.StoreAlias);
        Assert.Equal(AlgoliaCategoryIndexJobType.Upsert, job.Type);
        Assert.Equal([key], job.CategoryKeys);
    }

    [Fact]
    public async Task Plugin_Master_Switch_Blocks_Store_Indexing()
    {
        var options = CreateOptions(enabled: false);
        var queue = new ProductQueue();
        var service = new AlgoliaProductIndexService(
            queue,
            Options.Create(options),
            CreateResolver(options),
            NullLogger<AlgoliaProductIndexService>.Instance);

        await service.EnqueueProductAsync("Store", Guid.NewGuid(), isPublished: true);

        Assert.Empty(queue.Jobs);
    }

    private static AlgoliaOptions CreateOptions(bool enabled)
        => new()
        {
            Enabled = enabled,
            ApplicationId = "app",
            AdminApiKey = "admin",
            SearchApiKey = "search",
            Indexing = new AlgoliaIndexingOptions { Enabled = false },
            Stores =
            [
                new AlgoliaStoreOptions
                {
                    Alias = "Store",
                    Indexing = new AlgoliaStoreIndexingOptions
                    {
                        Enabled = true,
                        Products = true,
                        Categories = true,
                    },
                },
            ],
        };

    private static AlgoliaStoreResolver CreateResolver(AlgoliaOptions options)
        => new(
            Options.Create(options),
            Mock.Of<IServiceProvider>(),
            NullLogger<AlgoliaStoreResolver>.Instance);

    private sealed class ProductQueue : IAlgoliaProductIndexQueue
    {
        private readonly Channel<AlgoliaProductIndexJob> _channel = Channel.CreateUnbounded<AlgoliaProductIndexJob>();

        public List<AlgoliaProductIndexJob> Jobs { get; } = [];
        public ChannelReader<AlgoliaProductIndexJob> Reader => _channel.Reader;

        public bool TryEnqueue(AlgoliaProductIndexJob job)
        {
            Jobs.Add(job);
            return true;
        }

        public ValueTask EnqueueAsync(AlgoliaProductIndexJob job, CancellationToken ct = default)
        {
            Jobs.Add(job);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CategoryQueue : IAlgoliaCategoryIndexQueue
    {
        private readonly Channel<AlgoliaCategoryIndexJob> _channel = Channel.CreateUnbounded<AlgoliaCategoryIndexJob>();

        public List<AlgoliaCategoryIndexJob> Jobs { get; } = [];
        public ChannelReader<AlgoliaCategoryIndexJob> Reader => _channel.Reader;

        public bool TryEnqueue(AlgoliaCategoryIndexJob job)
        {
            Jobs.Add(job);
            return true;
        }

        public ValueTask EnqueueAsync(AlgoliaCategoryIndexJob job, CancellationToken ct = default)
        {
            Jobs.Add(job);
            return ValueTask.CompletedTask;
        }
    }
}

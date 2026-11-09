using Ekom.Algolia;
using Ekom.Algolia.Indexing;
using Ekom.Algolia.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class AlgoliaPriceIndexQueueTests
{
    [Fact]
    public void QueueCoalescesPendingChangesWithoutLosingUpdatesAfterDrain()
    {
        var options = Microsoft.Extensions.Options.Options.Create(Options(1));
        var queue = new AlgoliaPriceIndexQueue(options);
        var key = Guid.NewGuid();
        for (var i = 0; i < 100; i++) Assert.True(queue.TryEnqueue(key, i % 2 == 0 ? "Web" : "WEB"));
        Assert.False(queue.TryEnqueue(Guid.NewGuid(), "Web"));
        Assert.Equal(key, Assert.Single(queue.Drain(100)).NodeKey);
        Assert.True(queue.TryEnqueue(key, "Web"));
        Assert.Single(queue.Drain(100));
        Assert.Empty(queue.Drain(100));
    }

    [Fact]
    public async Task WorkerResolvesSiblingVariantsOnceAndQueuesOneParentPerStore()
    {
        var options = Microsoft.Extensions.Options.Options.Create(Options());
        var queue = new AlgoliaPriceIndexQueue(options);
        var product = Content(1, -1, "ekmProduct");
        var group = Content(2, 1, "ekmProductVariantGroup");
        var first = Content(3, 2, "ekmProductVariant");
        var second = Content(4, 2, "ekmProductVariant");
        var contentService = new Mock<IContentService>();
        contentService.Setup(x => x.GetById(first.Key)).Returns(first);
        contentService.Setup(x => x.GetById(second.Key)).Returns(second);
        contentService.Setup(x => x.GetById(2)).Returns(group);
        contentService.Setup(x => x.GetById(1)).Returns(product);
        for (var i = 0; i < 50; i++)
        {
            Assert.True(queue.TryEnqueue(first.Key, "Web"));
            Assert.True(queue.TryEnqueue(second.Key, "Web"));
        }
        Assert.Empty(contentService.Invocations);
        var output = new AlgoliaProductIndexQueue(NullLogger<AlgoliaProductIndexQueue>.Instance, options);
        using var provider = new ServiceCollection().AddSingleton(contentService.Object).AddSingleton(options)
            .AddSingleton<IAlgoliaProductIndexQueue>(output).AddSingleton<AlgoliaStoreResolver>()
            .AddSingleton<ILogger<AlgoliaStoreResolver>>(NullLogger<AlgoliaStoreResolver>.Instance).BuildServiceProvider();
        using var worker = new AlgoliaPriceIndexWorker(queue, provider.GetRequiredService<IServiceScopeFactory>(), options,
            NullLogger<AlgoliaPriceIndexWorker>.Instance);
        await worker.DispatchAsync(queue.Drain(100), default);
        Assert.True(output.Reader.TryRead(out var job));
        Assert.Equal(product.Key, Assert.Single(job!.ProductKeys));
        Assert.False(output.Reader.TryRead(out _));
        Assert.Equal(4, contentService.Invocations.Count);
        contentService.Verify(x => x.GetById(2), Times.Once);
        contentService.Verify(x => x.GetById(1), Times.Once);
    }

    private static AlgoliaOptions Options(int capacity = 100) => new()
    {
        Enabled = true, ApplicationId = "test", AdminApiKey = "test", SearchApiKey = "test",
        Stores = [new AlgoliaStoreOptions { Alias = "Web" }],
        Indexing = new AlgoliaIndexingOptions { Enabled = true, Products = true, Dispatching = new AlgoliaDispatcherOptions { MaxQueueSize = capacity } },
    };

    private static IContent Content(int id, int parentId, string alias)
    {
        var key = Guid.NewGuid();
        return Mock.Of<IContent>(content => content.Id == id && content.ParentId == parentId && content.Key == key
            && content.Published && content.ContentType == Mock.Of<ISimpleContentType>(type => type.Alias == alias));
    }
}

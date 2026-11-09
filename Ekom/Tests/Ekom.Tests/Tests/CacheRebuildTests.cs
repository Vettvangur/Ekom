using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Collections.Concurrent;
using Xunit;

namespace Ekom.Tests.Tests;

public class CacheRebuildTests
{
    [Fact]
    public async Task Rebuild_KeepsLiveStoresAvailableUntilReplacementIsComplete()
    {
        using var fixture = new StoreFixture();
        var old = fixture.Seed(1);
        var replacement = fixture.ConfigureNode(2);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        fixture.Factory.Setup(x => x.Create(It.IsAny<UmbracoContent>())).Returns(() =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            return replacement;
        });

        var rebuilding = Task.Run(() =>
        {
            using var rebuild = fixture.Config.CacheRebuild.Begin();
            fixture.Cache.ClearCache();
            fixture.Cache.FillCache();
            rebuild.Commit();
        });
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.Same(old, Assert.Single(fixture.Cache.Cache).Value);
            Assert.True(fixture.Cache.TryGetById(1, out var byId));
            Assert.Same(old, byId);
        }
        finally
        {
            release.Set();
            await rebuilding;
        }

        Assert.Same(replacement, Assert.Single(fixture.Cache.Cache).Value);
        Assert.False(fixture.Cache.TryGetById(1, out _));
        Assert.True(fixture.Cache.TryGetById(2, out var newById));
        Assert.Same(replacement, newById);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rebuild_EmptyOrUnmappableStoresPreservePreviousCache(bool mappingFailure)
    {
        using var fixture = new StoreFixture();
        var old = fixture.Seed(1);
        var originalDictionary = fixture.Cache.Cache;
        if (mappingFailure)
        {
            fixture.ConfigureNode(2);
            fixture.Factory.Setup(x => x.Create(It.IsAny<UmbracoContent>()))
                .Throws(new InvalidOperationException("Mapping failed"));
        }
        else
            fixture.Nodes.Setup(x => x.NodesByTypes("ekmStore")).Returns(Array.Empty<UmbracoContent>());

        using (fixture.Config.CacheRebuild.Begin())
        {
            fixture.Cache.ClearCache();
            Assert.Throws<InvalidOperationException>(() => fixture.Cache.FillCache());
        }

        Assert.Same(originalDictionary, fixture.Cache.Cache);
        Assert.Same(old, Assert.Single(fixture.Cache.Cache).Value);
        Assert.True(fixture.Cache.TryGetById(1, out _));
        // A failed rebuild must release its guard for a later retry.
        using var retry = fixture.Config.CacheRebuild.Begin();
    }

    [Fact]
    public async Task Rebuild_RejectsConcurrentStoreRemovalInsteadOfResurrectingIt()
    {
        using var fixture = new StoreFixture();
        var old = fixture.Seed(1);
        fixture.ConfigureNode(2);
        using (var rebuild = fixture.Config.CacheRebuild.Begin())
        {
            fixture.Cache.ClearCache();
            fixture.Cache.FillCache();
            await OutsideRebuild(() => fixture.Cache.Remove(old.Key));
            Assert.Throws<InvalidOperationException>(() => rebuild.Commit());
        }
        Assert.Empty(fixture.Cache.Cache);
        Assert.False(fixture.Cache.TryGetById(1, out _));
    }

    [Fact]
    public async Task Rebuild_PublishesMultipleDictionariesTogetherAndDiscardsFailedAttempts()
    {
        var coordinator = new CacheRebuildCoordinator();
        var owner = new object();
        ConcurrentDictionary<int, string> Stores() => coordinator.Get(owner, "stores", static () => new ConcurrentDictionary<int, string>());
        ConcurrentDictionary<int, string> Products() => coordinator.Get(owner, "products", static () => new ConcurrentDictionary<int, string>());
        Stores()[1] = "old store";
        Products()[1] = "old product";

        using (coordinator.Begin())
        {
            Stores()[2] = "incomplete store";
            Assert.Empty(Products());
            await OutsideRebuild(() =>
            {
                Assert.Equal("old store", Stores()[1]);
                Assert.Equal("old product", Products()[1]);
            });
            // Disposal without Commit models a later cache/repository throwing.
        }
        Assert.Equal("old store", Stores()[1]);
        Assert.Equal("old product", Products()[1]);

        using (var rebuild = coordinator.Begin())
        {
            Stores()[2] = "new store";
            Products()[2] = "new product";
            rebuild.Commit();
        }
        Assert.Equal("new store", Assert.Single(Stores()).Value);
        Assert.Equal("new product", Assert.Single(Products()).Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rebuild_RejectsCompletedAndStillRunningLiveStockUpdates(bool keepUpdateOpen)
    {
        var coordinator = new CacheRebuildCoordinator();
        var owner = new object();
        ConcurrentDictionary<int, decimal> Stock() => coordinator.Get(owner, "stock", static () => new ConcurrentDictionary<int, decimal>());
        Stock()[1] = 10;
        IDisposable? update = null;
        try
        {
            using (var rebuild = coordinator.Begin())
            {
                Stock()[1] = 10;
                await OutsideRebuild(() =>
                {
                    update = coordinator.Update();
                    Stock()[1] = 9;
                    if (!keepUpdateOpen) update.Dispose();
                });
                Assert.Throws<InvalidOperationException>(() => rebuild.Commit());
            }
            Assert.Equal(9, Stock()[1]);
        }
        finally { update?.Dispose(); }
    }

    [Fact]
    public async Task Read_PinsPrimaryAndSecondaryIndexesAcrossPublication()
    {
        var coordinator = new CacheRebuildCoordinator();
        var owner = new object();
        ConcurrentDictionary<int, string> Items() => coordinator.Get(owner, "items", static () => new ConcurrentDictionary<int, string>());
        ConcurrentDictionary<string, int> Index() => coordinator.Get(owner, "index", static () => new ConcurrentDictionary<string, int>());
        Items()[1] = "old";
        Index()["key"] = 1;
        using (coordinator.Read())
        {
            var key = Index()["key"];
            await OutsideRebuild(() =>
            {
                using var rebuild = coordinator.Begin();
                Items()[2] = "new";
                Index()["key"] = 2;
                rebuild.Commit();
            });
            Assert.Equal("old", Items()[key]);
        }
        Assert.Equal("new", Items()[Index()["key"]]);
    }

    [Fact]
    public async Task Rebuild_RejectsOverlappingAttempts()
    {
        var coordinator = new CacheRebuildCoordinator();
        using var rebuild = coordinator.Begin();
        await OutsideRebuild(() => Assert.Throws<InvalidOperationException>(() => coordinator.Begin()));
        rebuild.Commit();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProductRebuild_KeepsPrimaryAndAllIndexesOnFailureAndReplacesThemOnSuccess(bool fail)
    {
        var config = new Configuration(new ConfigurationBuilder().Build());
        var store = Mock.Of<IStore>(x => x.Alias == "store");
        var stores = new Mock<IBaseCache<IStore>>();
        stores.SetupGet(x => x.Cache).Returns(new ConcurrentDictionary<Guid, IStore>(
            new[] { new KeyValuePair<Guid, IStore>(Guid.NewGuid(), store) }));
        var nodes = new Mock<INodeService>();
        var factory = new Mock<IPerStoreFactory<IProduct>>();
        using var provider = new ServiceCollection().AddSingleton(nodes.Object).BuildServiceProvider();
        var cache = new ProductCache(config, NullLogger<IPerStoreIndexedCache<IProduct>>.Instance,
            stores.Object, factory.Object, provider);

        void Configure(int id, string sku)
        {
            var node = new UmbracoContent(new Dictionary<string, string>(), new Dictionary<string, string>())
            { Id = id, Key = Guid.NewGuid() };
            var category = Mock.Of<ICategory>(x => x.Id == id);
            var product = new Mock<IProduct>();
            product.SetupGet(x => x.Id).Returns(id);
            product.SetupGet(x => x.SKU).Returns(sku);
            product.SetupGet(x => x.Urls).Returns(new[] { "/" + sku });
            product.SetupGet(x => x.Categories).Returns(new[] { category });
            nodes.Setup(x => x.NodesByTypes("ekmProduct")).Returns(new[] { node });
            factory.Setup(x => x.Create(It.IsAny<UmbracoContent>(), store)).Returns(product.Object);
        }

        Configure(1, "old");
        cache.FillCache();
        var original = cache.Cache;
        Configure(2, "new");
        using (var rebuild = config.CacheRebuild.Begin())
        {
            cache.ClearCache();
            if (fail)
            {
                factory.Setup(x => x.Create(It.IsAny<UmbracoContent>(), store))
                    .Throws(new InvalidOperationException("Mapping failed"));
                Assert.Throws<InvalidOperationException>(() => cache.FillCache());
            }
            else
            {
                cache.FillCache();
                rebuild.Commit();
            }
        }

        var expectedId = fail ? 1 : 2;
        var expectedSku = fail ? "old" : "new";
        if (fail) Assert.Same(original, cache.Cache);
        Assert.True(cache.TryGetById("store", expectedId, out var item));
        Assert.True(cache.TryGetBySku("store", expectedSku, out var bySku));
        Assert.True(cache.TryGetByRoute("store", "/" + expectedSku, out var byRoute));
        Assert.Same(item, bySku);
        Assert.Same(item, byRoute);
        Assert.Single(cache.GetByAnyCategoryIds("store", new[] { expectedId }));
        Assert.False(cache.TryGetById("store", fail ? 2 : 1, out _));
        Assert.False(cache.TryGetBySku("store", fail ? "new" : "old", out _));
        Assert.False(cache.TryGetByRoute("store", fail ? "/new" : "/old", out _));
        Assert.Empty(cache.GetByAnyCategoryIds("store", new[] { fail ? 2 : 1 }));
    }

    private static Task OutsideRebuild(Action action)
    {
        using (ExecutionContext.SuppressFlow()) return Task.Run(action);
    }

    private sealed class StoreFixture : IDisposable
    {
        public Configuration Config { get; } = new(new ConfigurationBuilder().Build());
        public Mock<INodeService> Nodes { get; } = new();
        public Mock<IObjectFactory<IStore>> Factory { get; } = new();
        public StoreCache Cache { get; }
        private readonly ServiceProvider _provider;

        public StoreFixture()
        {
            _provider = new ServiceCollection().AddSingleton(Nodes.Object).BuildServiceProvider();
            Cache = new StoreCache(Config, NullLogger<BaseCache<IStore>>.Instance, Factory.Object, _provider);
        }

        public IStore ConfigureNode(int id)
        {
            var key = Guid.NewGuid();
            var store = Mock.Of<IStore>(x => x.Id == id && x.Key == key && x.Alias == "store");
            Nodes.Setup(x => x.NodesByTypes("ekmStore")).Returns(new[] { new UmbracoContent { Id = id, Key = key } });
            Factory.Setup(x => x.Create(It.IsAny<UmbracoContent>())).Returns(store);
            return store;
        }

        public IStore Seed(int id)
        {
            var store = ConfigureNode(id);
            Cache.FillCache();
            return store;
        }

        public void Dispose() => _provider.Dispose();
    }
}

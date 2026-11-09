using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using Ekom.Umb;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
#if NET10_0
using Ekom.Umb.Services;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Services.Navigation;
#endif

namespace Ekom.Tests.Tests;

[CollectionDefinition("Cache rebuild initialization", DisableParallelization = true)]
public sealed class CacheRebuildInitializationCollection { }

[Collection("Cache rebuild initialization")]
public class CacheRebuildInitializerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedRebuild_RetainsStoresAndReadinessAndReportsFailure(bool wasInitialized)
    {
        using var fixture = new Fixture(wasInitialized);
        var original = fixture.StoreCache.Cache;
        var oldStore = Assert.Single(original).Value;
        var laterCache = new Mock<ICache>();
        laterCache.Setup(x => x.FillCache()).Throws(new InvalidOperationException("A later cache failed"));
        fixture.Config.Entries.Add(laterCache.Object);

        var error = Assert.Throws<InvalidOperationException>(() => fixture.Initializer.Initialize(true));

        Assert.Equal("A later cache failed", error.Message);
        Assert.Same(original, fixture.StoreCache.Cache);
        Assert.Same(oldStore, Assert.Single(fixture.StoreCache.Cache).Value);
        Assert.Equal(wasInitialized, fixture.Readiness.IsReady);
        Assert.False(fixture.Config.CacheRebuild.IsRebuilding);
    }

#if NET10_0
    [Fact]
    public void MissingRoot_DoesNotClearStoresAndRefreshServiceReportsFailure()
    {
        using var fixture = new Fixture(true);
        var original = fixture.StoreCache.Cache;
        fixture.Query.Setup(x => x.ContentAtRoot()).Returns(Array.Empty<IPublishedContent>());
        var refresh = new EkomCacheRefreshService(fixture.Provider.GetRequiredService<IServiceScopeFactory>());

        Assert.Throws<InvalidOperationException>(() => refresh.RefreshCache());

        Assert.Same(original, fixture.StoreCache.Cache);
        Assert.Single(fixture.StoreCache.Cache);
        Assert.True(fixture.Readiness.IsReady);
    }

    [Fact]
    public void UnconfiguredStartup_RemainsNotReadyButAllowsBackofficeSetup()
    {
        using var fixture = new Fixture(false);
        fixture.Query.Setup(x => x.ContentAtRoot()).Returns(Array.Empty<IPublishedContent>());
        fixture.Initializer.Initialize(false);
        Assert.False(fixture.Readiness.IsReady);
        Assert.False(fixture.Config.CacheRebuild.IsRebuilding);
    }
#endif

#if NET10_0
    // Umbraco's static published-content extensions resolve unrelated CMS services too.
    // Supply inert interfaces so this test exercises cache initialization without booting a CMS.
    private sealed class UmbracoTestServices(IServiceProvider services) : IServiceProvider
    {
        private readonly Dictionary<Type, object> _mocks = new();
        public object? GetService(Type serviceType)
        {
            var service = services.GetService(serviceType);
            if (service != null || !serviceType.IsInterface) return service;
            if (!_mocks.TryGetValue(serviceType, out service))
            {
                var mock = (Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(serviceType))!;
                _mocks[serviceType] = service = mock.Object;
            }
            return service;
        }
    }
#endif

    private sealed class TestConfiguration() : Configuration(new ConfigurationBuilder().Build())
    {
        internal List<ICache> Entries { get; } = new();
        internal override Lazy<List<ICache>> CacheList => new(() => Entries);
    }

    private sealed class Fixture : IDisposable
    {
        public TestConfiguration Config { get; } = new();
        public StockReservationReadiness Readiness { get; } = new();
        public ServiceProvider Provider { get; }
        public StoreCache StoreCache { get; }
        public EkomCacheInitializer Initializer { get; }
#if NET10_0
        public Mock<IPublishedContentQuery> Query { get; } = new();
        private readonly IServiceProvider? _previousServices = StaticServiceProvider.Instance;
#endif
        public Fixture(bool initialized)
        {
            Readiness.SchemaReady();
            if (initialized) Readiness.Initialized();
            var node = new UmbracoContent { Id = 1, Key = Guid.NewGuid() };
            var store = Mock.Of<IStore>(x => x.Id == node.Id && x.Key == node.Key && x.Alias == "store");
            var nodes = new Mock<INodeService>();
            nodes.Setup(x => x.NodesByTypes("ekmStore")).Returns(new[] { node });
            var factory = new Mock<IObjectFactory<IStore>>();
            factory.Setup(x => x.Create(It.IsAny<UmbracoContent>())).Returns(store);
            var services = new ServiceCollection()
                .AddSingleton<Configuration>(Config)
                .AddSingleton(Readiness)
                .AddSingleton(nodes.Object)
                .AddSingleton<Ekom.API.Prices>(_ => new Ekom.API.Prices(Config, null!))
                .AddSingleton<StoreCache>(sp => new StoreCache(Config, NullLogger<BaseCache<IStore>>.Instance, factory.Object, sp))
                .AddSingleton<IBaseCache<IStore>>(sp => sp.GetRequiredService<StoreCache>());
            services.AddSingleton<EkomCacheInitializer>(sp =>
            {
#if NET10_0
                var root = new Mock<IPublishedContent>();
                root.SetupGet(x => x.ContentType).Returns(Mock.Of<IPublishedContentType>(x => x.Alias == "ekom" && x.ItemType == PublishedItemType.Content));
                root.SetupGet(x => x.ItemType).Returns(PublishedItemType.Content);
                Query.Setup(x => x.ContentAtRoot()).Returns(new[] { root.Object });
                return new EkomCacheInitializer(Config, sp, Mock.Of<IUmbracoContextFactory>(), Query.Object,
                    new EkomCacheBuildContext(), new Umbraco17ContentCache(Config), NullLogger<EkomCacheInitializer>.Instance);
#else
                return new EkomCacheInitializer(Config, sp, NullLogger<EkomCacheInitializer>.Instance);
#endif
            });
#if NET10_0
            services.AddSingleton(Mock.Of<IVariationContextAccessor>());
            services.AddSingleton(Mock.Of<IDocumentNavigationQueryService>());
            services.AddSingleton(Mock.Of<IPublishedContentStatusFilteringService>());
#endif
            Provider = services.BuildServiceProvider();
#if NET10_0
            StaticServiceProvider.Instance = new UmbracoTestServices(Provider);
#endif
            StoreCache = Provider.GetRequiredService<StoreCache>();
            Config.Entries.Add(StoreCache);
            StoreCache.FillCache();
            Initializer = Provider.GetRequiredService<EkomCacheInitializer>();
        }
        public void Dispose()
        {
#if NET10_0
            StaticServiceProvider.Instance = _previousServices;
#endif
            Provider.Dispose();
        }
    }
}

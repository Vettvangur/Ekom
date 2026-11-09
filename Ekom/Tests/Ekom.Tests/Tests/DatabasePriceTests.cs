using Ekom.Algolia;
using Ekom.Algolia.Events;
using Ekom.Algolia.Indexing;
using Ekom.Algolia.Services;
using Microsoft.Extensions.Options;
using Ekom.API;
using Ekom.Cache;
using Ekom.Events;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using Ekom.Tests.Objects;
using Ekom.Utilities;
using LinqToDB;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Collections.Concurrent;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection(NativeDiscountPriceCollection.Name)]
public sealed class DatabasePriceTests
{
    [Fact]
    public async Task NodeStorageIsDefaultAndRejectsTableWrites()
    {
        using var fixture = new Fixture(enabled: false);
        Assert.False(Configuration.Instance.UseDatabasePrices);
        Assert.Equal("100", fixture.Prices.Resolve(Guid.NewGuid(), "price", "Web", "100", fixture.Store.Currencies));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Prices.SetPriceAsync(Guid.NewGuid(), "Web", "en-US", 20));
    }

    [Fact]
    public async Task UpsertIsCaseInsensitiveAndSeparatesPropertiesStoresAndCurrencies()
    {
        using var fixture = new Fixture();
        var key = Guid.NewGuid();
        fixture.Prices.SetPrice(key, "Web", "en-US", 12.3456m);
        await fixture.Prices.SetPriceAsync(key, "WEB", "EN-us", 20);
        await fixture.Prices.SetPriceAsync(key, "Web", "en-GB", 30);
        await fixture.Prices.SetPriceAsync(key, "Wholesale", "en-US", 40);
        await fixture.Prices.SetPriceAsync(key, "Web", "en-US", 10, "ekmDiscountPrice");
        var rows = await fixture.Prices.GetAsync(key);
        Assert.Equal(3, rows.Count);
        Assert.Equal(20, rows.Single(x => x.StoreAlias == "WEB" && x.Currency == "EN-US").Price);
        Assert.Equal(10, Assert.Single(await fixture.Prices.GetAsync(key, "ekmDiscountPrice")).Price);
        Assert.Empty(await fixture.Prices.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ConcurrentUpdatesKeepOneRowPerCurrency()
    {
        using var fixture = new Fixture();
        var key = Guid.NewGuid();
        await Task.WhenAll(Enumerable.Range(1, 12).Select(i =>
            fixture.Prices.SetPriceAsync(key, "Web", i % 2 == 0 ? "en-US" : "en-GB", i)));
        var rows = await fixture.Prices.GetAsync(key);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.InRange(row.Price, 1m, 12m));
    }

    [Fact]
    public async Task CachedProductAndVariantObserveExternalWritesWithoutPublishing()
    {
        using var fixture = new Fixture();
        var product = new Product(Content("100"), fixture.Store);
        var variant = new TestVariant(Content("0", "-1,100,200,300"), fixture.Store, product);
        Assert.Equal(100, product.Prices[0].BeforeDiscount.Value);
        Assert.Equal(100, variant.Prices[0].BeforeDiscount.Value);
        // Another service instance represents an ERP writer or another application server.
        var writer = new Prices(Configuration.Instance, fixture.Factory);
        await writer.SetPriceAsync(product.Key, "Web", "en-US", 150);
        Assert.Equal(150, product.Prices[0].BeforeDiscount.Value);
        Assert.Equal(150, variant.Prices[0].BeforeDiscount.Value);
        Assert.Equal(150, product.OriginalPrice.Value);
        await writer.SetPriceAsync(variant.Key, "Web", "en-US", 175);
        Assert.Equal(175, variant.Prices[0].BeforeDiscount.Value);
        await writer.SetPriceAsync(product.Key, "Web", "en-US", 120, "ekmDiscountPrice");
        Assert.Equal(120, product.Prices[0].AfterDiscount.Value);
        Assert.Equal("100", product.Properties["price"]);
    }

    [Fact]
    public async Task PartialOverridesPreserveOtherCurrenciesAndZeroIsAnOverride()
    {
        using var fixture = new Fixture();
        var key = Guid.NewGuid();
        const string raw = "[{\"Currency\":\"en-US\",\"Price\":100},{\"Currency\":\"en-GB\",\"Price\":80}]";
        await fixture.Prices.SetPriceAsync(key, "Web", "en-US", 0);
        var result = fixture.Prices.Resolve(key, "price", "Web", raw, fixture.Store.Currencies);
        Assert.Equal(0, NativeDiscountPrice.Read(result, "Web", "en-US", "en-US"));
        Assert.Equal(80, NativeDiscountPrice.Read(result, "Web", "en-GB", "en-US"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1.23456)]
    public async Task InvalidPriceDoesNotWrite(decimal price)
    {
        using var fixture = new Fixture();
        var key = Guid.NewGuid();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Prices.SetPriceAsync(key, "Web", "en-US", price));
        Assert.Empty(await fixture.Prices.GetAsync(key));
    }

    [Fact]
    public async Task BatchConflictRollsBackEveryCurrency()
    {
        using var fixture = new Fixture();
        var key = Guid.NewGuid();
        await fixture.Prices.SetPriceAsync(key, "Web", "en-US", 100);
        await Assert.ThrowsAsync<Ekom.Exceptions.PriceConflictException>(() => fixture.Prices.ApplyAsync([
            new(key, "Web", "en-GB", 80),
            new(key, "Web", "en-US", 120, CheckExpected: true, ExpectedPrice: 90),
        ]));
        Assert.Equal(100, Assert.Single(await fixture.Prices.GetAsync(key)).Price);
        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Prices.Apply([
            new(key, "Web", "en-GB", 80), new(key, "Web", "en-US", -1),
        ]));
        Assert.Equal(100, Assert.Single(await fixture.Prices.GetAsync(key)).Price);
    }

    [Fact]
    public async Task NotificationsArePostCommitNoOpsAreSilentAndRemovalRestoresFallback()
    {
        using var fixture = new Fixture();
        var key = Guid.NewGuid();
        var events = new List<PriceChangedEventArgs>();
        EventHandler<PriceChangedEventArgs> broken = (_, args) =>
        {
            if (args.Key == key) throw new InvalidOperationException("subscriber failed");
        };
        EventHandler<PriceChangedEventArgs> observer = (_, args) =>
        {
            if (args.Key != key) return;
            Assert.Equal(args.NewValue, fixture.Prices.Get(key).SingleOrDefault()?.Price);
            events.Add(args);
        };
        PriceEvents.PriceChanged += broken;
        PriceEvents.PriceChanged += observer;
        try
        {
            await fixture.Prices.SetPriceAsync(key, "Web", "en-US", 50);
            var timestamp = Assert.Single(await fixture.Prices.GetAsync(key)).UpdateDate;
            fixture.Prices.SetPrice(key, "Web", "en-US", 50);
            Assert.Equal(timestamp, Assert.Single(await fixture.Prices.GetAsync(key)).UpdateDate);
            Assert.Single(events);
            await fixture.Prices.RemovePriceAsync(key, "Web", "en-US");
            fixture.Prices.RemovePrice(key, "Web", "en-US");
            Assert.Equal(2, events.Count);
            Assert.Null(events[1].NewValue);
            Assert.Equal(50, events[1].OldValue);
            Assert.Equal("100", fixture.Prices.Resolve(key, "price", "Web", "100", fixture.Store.Currencies));
        }
        finally
        {
            PriceEvents.PriceChanged -= broken;
            PriceEvents.PriceChanged -= observer;
        }
    }

    [Fact]
    public async Task RequestSnapshotSurvivesExternalWritesAndRefreshesOnNextRequest()
    {
        using var fixture = new Fixture();
        var context = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var reader = new Prices(Configuration.Instance, fixture.Factory, context);
        var key = Guid.NewGuid();
        try
        {
            await reader.SetPriceAsync(key, "Web", "en-US", 50);
            Assert.Equal(50, Assert.Single(reader.Get(key)).Price);
            await fixture.Prices.SetPriceAsync(key, "Web", "en-US", 75);
            Assert.Equal(50, Assert.Single(reader.Get(key)).Price);
            context.HttpContext = new DefaultHttpContext();
            Assert.Equal(75, Assert.Single(reader.Get(key)).Price);
            await reader.SetPriceAsync(key, "Web", "en-US", 90);
            Assert.Equal(90, Assert.Single(reader.Get(key)).Price);
        }
        finally { context.HttpContext = null; }
    }

    [Fact]
    public async Task StaleMetadataEditsDoNotOverwriteErpAndExplicitEditsConflict()
    {
        using var fixture = new Fixture();
        var key = Guid.NewGuid();
        fixture.Prices.SetPrice(key, "Web", "en-US", 50);
        var baseline = fixture.Prices.Get(key);
        var original = new CurrencyPriceRoot { ["Web"] = [new CurrencyPrice(50, "en-US")] };
        await fixture.Prices.SetPriceAsync(key, "Web", "en-US", 75);
        var unchanged = PriceEdits.Create(key, original, original, baseline, false);
        Assert.Empty(unchanged);
        fixture.Prices.Apply(unchanged);
        var edited = new CurrencyPriceRoot { ["Web"] = [new CurrencyPrice(60, "en-US")] };
        Assert.Throws<Ekom.Exceptions.PriceConflictException>(() => fixture.Prices.Apply(
            PriceEdits.Create(key, edited, original, baseline, false)));
        Assert.Equal(75, Assert.Single(fixture.Prices.Get(key)).Price);
        Assert.Throws<Ekom.Exceptions.PriceConflictException>(() => PriceEdits.Create(key, edited, null, null, false));
    }

    [Fact]
    public async Task PriceRevisionsReplaceCalculatedCacheEntry()
    {
        using var fixture = new Fixture();
        var product = new Product(Content("100"), fixture.Store);
        _ = product.Prices;
        var before = fixture.CacheCount;
        for (var i = 1; i <= 30; i++)
        {
            await fixture.Prices.SetPriceAsync(product.Key, "Web", "en-US", i);
            Assert.Equal(i, product.Prices[0].BeforeDiscount.Value);
        }
        Assert.Equal(before, fixture.CacheCount);
    }

    [Theory]
    [InlineData("100", 0)]
    [InlineData("[{\"Currency\":\"en-US\",\"Price\":100},{\"Currency\":\"en-GB\",\"Price\":80}]", 80)]
    public void ExportPreservesLegacyFallbacks(string raw, decimal fallback)
    {
        using var fixture = new Fixture();
        var value = PriceHelper.Export(raw, [new PriceData { StoreAlias = "WEB", Currency = "EN-US", Price = 150 }], [fixture.Store]);
        Assert.Equal(150, NativeDiscountPrice.Read(value, "Web", "en-US", "en-US"));
        Assert.Equal(fallback, NativeDiscountPrice.Read(value, "Web", "en-GB", "en-US") ?? 0);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task PriceEndpointsRejectMissingNodeOrStorePermission(bool nodeAllowed, bool storeAllowed)
    {
        using var fixture = new Fixture();
        var content = Content("100");
        var nodes = new Mock<INodeService>();
        nodes.Setup(x => x.NodeById("200", true)).Returns(content);
        var security = new Mock<ISecurityService>();
        security.Setup(x => x.CanEditPrices(content)).Returns(nodeAllowed);
        var access = new Mock<IManagerAccessService>();
        access.Setup(x => x.CanAccessStore(It.IsAny<string>())).Returns(storeAllowed);
        using var services = new ServiceCollection().AddSingleton(security.Object).AddSingleton(access.Object).BuildServiceProvider();
        var controller = new Ekom.Controllers.EkomBackofficeApiController(Configuration.Instance,
            null!, null!, nodes.Object, null!, services, null!, null!, null!, null!);
        var put = await controller.SetPrice("200", "price", new TablePriceRequest
            { StoreAlias = "Web", Currency = "en-US", Price = 50 }, default);
        Assert.Equal(403, Assert.IsType<Microsoft.AspNetCore.Mvc.StatusCodeResult>(put).StatusCode);
        var delete = await controller.RemovePrice("200", "price", "Web", "en-US", default);
        Assert.Equal(403, Assert.IsType<Microsoft.AspNetCore.Mvc.StatusCodeResult>(delete).StatusCode);
        if (!nodeAllowed)
        {
            var get = await controller.GetPrices("200", "price", default);
            Assert.Equal(403, Assert.IsType<Microsoft.AspNetCore.Mvc.StatusCodeResult>(get).StatusCode);
        }
        Assert.Empty(await fixture.Prices.GetAsync(content.Key));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public void CommittedPricesQueuePublishedProductOrVariantParent(bool variant, bool published)
    {
        using var fixture = new Fixture();
        var key = Guid.NewGuid();
        var productKey = variant ? Guid.NewGuid() : key;
        var service = new Mock<Umbraco.Cms.Core.Services.IContentService>();
        var product = new Mock<Umbraco.Cms.Core.Models.IContent>();
        product.SetupGet(x => x.Key).Returns(productKey);
        product.SetupGet(x => x.Published).Returns(published);
        product.SetupGet(x => x.ContentType).Returns(Mock.Of<Umbraco.Cms.Core.Models.ISimpleContentType>(x => x.Alias == "ekmProduct"));
        if (variant)
        {
            var child = new Mock<Umbraco.Cms.Core.Models.IContent>();
            child.SetupGet(x => x.Published).Returns(published);
            child.SetupGet(x => x.ParentId).Returns(2);
            child.SetupGet(x => x.ContentType).Returns(Mock.Of<Umbraco.Cms.Core.Models.ISimpleContentType>(x => x.Alias == "ekmProductVariant"));
            service.Setup(x => x.GetById(key)).Returns(child.Object);
            service.Setup(x => x.GetById(2)).Returns(Mock.Of<Umbraco.Cms.Core.Models.IContent>(x => x.ParentId == 1));
            service.Setup(x => x.GetById(1)).Returns(product.Object);
        }
        else service.Setup(x => x.GetById(key)).Returns(product.Object);
        var options = Options.Create(new AlgoliaOptions
        {
            ApplicationId = "test", AdminApiKey = "test", SearchApiKey = "test",
            Enabled = true, Indexing = new AlgoliaIndexingOptions { Enabled = true, Products = true },
            Stores = [new AlgoliaStoreOptions { Alias = "Web" }],
        });
        var queue = new AlgoliaProductIndexQueue(NullLogger<AlgoliaProductIndexQueue>.Instance, options);
        using var provider = new ServiceCollection().AddSingleton(service.Object).AddSingleton<IAlgoliaProductIndexQueue>(queue)
            .AddSingleton(options).AddSingleton<ILogger<AlgoliaStoreResolver>>(NullLogger<AlgoliaStoreResolver>.Instance)
            .AddSingleton<AlgoliaStoreResolver>().BuildServiceProvider();
        var events = new AlgoliaEkomEvents(provider.GetRequiredService<IServiceScopeFactory>(), options);
        events.Initialize();
        try
        {
            fixture.Prices.SetPrice(key, "Web", "en-US", 125);
            Assert.Equal(published, queue.Reader.TryRead(out var job));
            if (published)
            {
                Assert.Equal("Web", job!.StoreAlias);
                Assert.Equal(AlgoliaProductIndexJobType.Upsert, job.Type);
                Assert.Equal(productKey, Assert.Single(job.ProductKeys));
            }
        }
        finally { events.Terminate(); }
    }

    private static UmbracoContent Content(string price, string path = "-1,100,200")
        => new(new Dictionary<string, string>(), new Dictionary<string, string>
        {
            ["id"] = path.Split(',')[^1], ["__Key"] = Guid.NewGuid().ToString(),
            ["parentID"] = "100", ["parentKey"] = Guid.NewGuid().ToString(),
            ["level"] = "3", ["nodeName"] = "Database price test", ["__Path"] = path,
            ["__NodeTypeAlias"] = "ekmProduct", ["sortOrder"] = "0",
            ["createDate"] = "2024-01-01T00:00:00Z", ["updateDate"] = "2024-01-01T00:00:00Z",
            ["sku"] = "database-price-test", ["price"] = price, ["ekmDiscountPrice"] = "",
        });

    private sealed class TestVariant(UmbracoContent content, IStore store, IProduct product) : Variant(content, store)
    {
        public override IProduct? Product => product;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"ekom-prices-{Guid.NewGuid():N}.db");
        private readonly ConfigurationScope _configuration;
        private readonly MemoryCache _memory = new(new MemoryCacheOptions());
        public int CacheCount => _memory.Count;
        public DatabaseFactory Factory { get; }
        public Prices Prices { get; }
        public IStore Store { get; }

        public Fixture(bool enabled = true)
        {
            var dbConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoDbDSN"] = $"Data Source={_path};Pooling=False",
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
            }).Build();
            var host = new Mock<IHostEnvironment>();
            host.SetupGet(x => x.ContentRootPath).Returns(Path.GetTempPath());
            Factory = new DatabaseFactory(dbConfiguration, host.Object);
            var schema = new DatabaseService(Factory, NullLogger<DatabaseService>.Instance, new StockReservationReadiness());
            schema.EnsurePriceTable();
            schema.EnsurePriceTable();
            var usd = new CurrencyModel { CurrencyValue = "en-US", CurrencyFormat = "C" };
            var gbp = new CurrencyModel { CurrencyValue = "en-GB", CurrencyFormat = "C" };
            var store = new Mock<IStore>();
            store.SetupGet(x => x.Alias).Returns("Web");
            store.SetupGet(x => x.Currency).Returns(usd);
            store.SetupGet(x => x.Currencies).Returns([usd, gbp]);
            store.SetupGet(x => x.VatIncludedInPrice).Returns(true);
            Store = store.Object;
            var discounts = new Mock<IPerStoreCache<IProductDiscount>>();
            discounts.SetupGet(x => x.Cache).Returns(new ConcurrentDictionary<string, ConcurrentDictionary<Guid, IProductDiscount>>());
            var stores = new Mock<IStoreService>();
            stores.Setup(x => x.GetStoreByAlias("Web")).Returns(Store);
            stores.Setup(x => x.GetStoreFromCache()).Returns(Store);
            _configuration = new ConfigurationScope(overrides: enabled ? new Dictionary<string, string?> { ["Ekom:UseDatabasePrices"] = "true" } : null,
                addServices: services =>
                {
                    services.AddSingleton(Factory);
                    services.AddSingleton<Prices>();
                    services.AddSingleton<IMemoryCache>(_memory);
                    services.AddSingleton(new ProductDiscountService(discounts.Object, new DiscountEvents()));
                    services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
                    services.AddSingleton<IStoreService>(stores.Object);
                    services.AddSingleton(new Ekom.API.Store(stores.Object, Mock.Of<ICacheRefreshService>()));
                    services.AddTransient(sp => new Catalog(Mock.Of<ILogger<Catalog>>(),
                        sp.GetRequiredService<Configuration>(), sp.GetRequiredService<IServiceScopeFactory>(),
                        Mock.Of<IPerStoreIndexedCache<IProduct>>(), Mock.Of<IPerStoreIndexedCache<ICategory>>(),
                        discounts.Object, Mock.Of<IPerStoreIndexedCache<IVariant>>(),
                        Mock.Of<IPerStoreIndexedCache<IVariantGroup>>(), stores.Object,
                        sp.GetRequiredService<IHttpContextAccessor>(), Mock.Of<IProductFilterService>()));
                });
            Prices = Prices.Instance;
        }

        public void Dispose()
        {
            _configuration.Dispose();
            _memory.Dispose();
            File.Delete(_path);
        }
    }
}

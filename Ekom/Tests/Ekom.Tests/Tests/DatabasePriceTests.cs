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
    public async Task CachedProductAndVariantObserveLocalWritesWithoutPublishing()
    {
        using var fixture = new Fixture();
        var product = new Product(Content("100"), fixture.Store);
        var variant = new TestVariant(Content("0", "-1,100,200,300"), fixture.Store, product);
        Assert.Equal(100, product.Prices[0].BeforeDiscount.Value);
        Assert.Equal(100, variant.Prices[0].BeforeDiscount.Value);
        // ERP sync uses the registered singleton, just like stock.
        var writer = fixture.Prices;
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
    public async Task ExternalWritesRequireExplicitCacheReloadAcrossRequests()
    {
        using var fixture = new Fixture();
        var context = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        using var reader = new Prices(Configuration.Instance, fixture.Factory, context);
        var key = Guid.NewGuid();
        try
        {
            await reader.SetPriceAsync(key, "Web", "en-US", 50);
            Assert.Equal(50, Assert.Single(reader.Get(key)).Price);
            await fixture.Prices.SetPriceAsync(key, "Web", "en-US", 75);
            Assert.Equal(50, Assert.Single(reader.Get(key)).Price);
            context.HttpContext = new DefaultHttpContext();
            Assert.Equal(50, Assert.Single(reader.Get(key)).Price);
            reader.FillCache();
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
    public async Task CommittedPricesQueuePublishedProductOrVariantParent(bool variant, bool published)
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
        var priceQueue = new AlgoliaPriceIndexQueue(options);
        using var provider = new ServiceCollection().AddSingleton(service.Object).AddSingleton<IAlgoliaProductIndexQueue>(queue)
            .AddSingleton(priceQueue).AddSingleton(options).AddSingleton<ILogger<AlgoliaStoreResolver>>(NullLogger<AlgoliaStoreResolver>.Instance)
            .AddSingleton<AlgoliaStoreResolver>().BuildServiceProvider();
        var events = new AlgoliaEkomEvents(provider.GetRequiredService<IServiceScopeFactory>(), options);
        events.Initialize();
        try
        {
            fixture.Prices.SetPrice(key, "Web", "en-US", 125);
            fixture.Prices.SetPrice(key, "Web", "en-GB", 100);
            Assert.Empty(service.Invocations); // No content queries on the price writer.
            var changes = priceQueue.Drain(100);
            Assert.Single(changes);
            using var worker = new AlgoliaPriceIndexWorker(priceQueue, provider.GetRequiredService<IServiceScopeFactory>(),
                options, NullLogger<AlgoliaPriceIndexWorker>.Instance);
            await worker.DispatchAsync(changes, default);
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

    [Theory]
    [InlineData("100", 1)]
    [InlineData("[{\"Currency\":\"en-US\",\"Price\":100},{\"Currency\":\"en-GB\",\"Price\":80}]", 2)]
    [InlineData("{\"Web\":[{\"Currency\":\"en-US\",\"Price\":100}]}", 1)]
    [InlineData("{\"values\":{\"Web\":[{\"Currency\":\"en-US\",\"Price\":100}]}}", 1)]
    public async Task MigrationCopiesRawPricesAndCanBeRerun(string raw, int count)
    {
        using var fixture = new Fixture();
        var key = Guid.NewGuid();
        Assert.Equal(count, await fixture.Prices.MigrateNodePricesAsync(key, raw, [fixture.Store]));
        Assert.Equal(100, (await fixture.Prices.GetAsync(key)).Single(x => x.Currency == "EN-US").Price);
        Assert.Equal(0, await fixture.Prices.MigrateNodePricesAsync(key, raw, [fixture.Store]));
        Assert.Equal(count, (await fixture.Prices.GetAsync(key)).Count);
    }

    [Fact]
    public async Task MigrationPreservesExistingOverridesAndVariantZero()
    {
        using var fixture = new Fixture();
        var key = Guid.NewGuid();
        fixture.Prices.SetPrice(key, "Web", "en-US", 150);
        const string raw = "[{\"Currency\":\"en-US\",\"Price\":100},{\"Currency\":\"en-GB\",\"Price\":0}]";
        Assert.Equal(1, await fixture.Prices.MigrateNodePricesAsync(key, raw, [fixture.Store]));
        var rows = await fixture.Prices.GetAsync(key);
        Assert.Equal(150, rows.Single(x => x.Currency == "EN-US").Price);
        Assert.Equal(0, rows.Single(x => x.Currency == "EN-GB").Price);
        Assert.Equal(1, await fixture.Prices.MigrateNodePricesAsync(key, "90", [fixture.Store], "ekmDiscountPrice"));
        Assert.Equal(90, Assert.Single(await fixture.Prices.GetAsync(key, "ekmDiscountPrice")).Price);
    }

    [Fact]
    public async Task MigrationSkipsMissingValuesAndRejectsInvalidBatch()
    {
        using var fixture = new Fixture();
        var key = Guid.NewGuid();
        Assert.Equal(0, await fixture.Prices.MigrateNodePricesAsync(key, "", [fixture.Store]));
        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() =>
            fixture.Prices.MigrateNodePricesAsync(key, "{invalid", [fixture.Store]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Prices.MigrateNodePricesAsync(key,
            "[{\"Currency\":\"en-US\",\"Price\":100},{\"Currency\":\"en-GB\",\"Price\":-1}]", [fixture.Store]));
        Assert.Empty(await fixture.Prices.GetAsync(key));
    }

    [Theory]
    [InlineData(100, 0)]
    [InlineData(801, 0)]
    public async Task PriceSortedCatalogReadsSharedCacheWithoutQueries(int count, int expectedQueries)
    {
        using var fixture = new Fixture();
        var products = Enumerable.Range(1, count).Select(i => (IProduct)new CatalogPriceProduct(
            Content("100", $"-1,100,{i + 200}"), fixture.Store)).ToList();
        var context = Configuration.Resolver.GetRequiredService<IHttpContextAccessor>();
        context.HttpContext = new DefaultHttpContext { RequestServices = Configuration.Resolver };
        try
        {
            fixture.Prices.FillCache();
            using var counter = new PriceQueryCounter(fixture.DatabasePath);
            var response = await ProductResponse.CreateAsync(products, new ProductQuery
                { OrderBy = Ekom.Utilities.OrderBy.PriceAsc, Page = 1, PageSize = 10, RaiseEvents = false });
            Assert.Equal(10, response.Products.Count());
            Assert.All(response.Products, product => Assert.Equal(100, product.Price.Value));
            Assert.Equal(expectedQueries, counter.Count);
        }
        finally { context.HttpContext = null; }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PriceFiltersAndTitlePagingReadSharedCacheWithoutQueries(bool priceFilter)
    {
        using var fixture = new Fixture();
        var products = Enumerable.Range(0, 801).Select(i => (IProduct)new CatalogPriceProduct(
            Content("100", $"-1,100,{i + 200}"), fixture.Store)).ToList();
        var context = Configuration.Resolver.GetRequiredService<IHttpContextAccessor>();
        context.HttpContext = new DefaultHttpContext { RequestServices = Configuration.Resolver };
        var metafields = Mock.Get(Configuration.Resolver.GetRequiredService<IMetafieldService>());
        metafields.Setup(x => x.FilterProducts(It.IsAny<IEnumerable<IProduct>>(), It.IsAny<ProductQuery>()))
            .Returns((IEnumerable<IProduct> input, ProductQuery _) => input.Where(product => product.OriginalPrice.Value >= 50));
        try
        {
            fixture.Prices.FillCache();
            using var counter = new PriceQueryCounter(fixture.DatabasePath);
            var query = new ProductQuery { OrderBy = Ekom.Utilities.OrderBy.TitleAsc, Page = 1, PageSize = 10, RaiseEvents = false };
            if (priceFilter) query.PropertyFilters = new() { ["priceFrom"] = ["50"] };
            var response = await ProductResponse.CreateAsync(products, query);
            Assert.Equal(10, response.Products.Count());
            Assert.Equal(0, counter.Count);
            _ = products[^1].Prices;
            Assert.Equal(0, counter.Count);
        }
        finally { context.HttpContext = null; }
    }

    [Fact]
    public async Task BackgroundReadsUseSharedCacheAndObserveChangesImmediately()
    {
        using var fixture = new Fixture();
        var product = new Product(Content("100"), fixture.Store);
        fixture.Prices.FillCache();
        using var counter = new PriceQueryCounter(fixture.DatabasePath);
        for (var i = 0; i < 20; i++) Assert.Equal(100, product.Prices[0].BeforeDiscount.Value);
        Assert.Equal(0, counter.Count);
        using (fixture.Prices.BeginReadScope())
        {
            await fixture.Prices.SetPriceAsync(product.Key, "Web", "en-US", 150);
            counter.Reset();
            for (var i = 0; i < 20; i++) Assert.Equal(150, product.Prices[0].BeforeDiscount.Value);
            Assert.Equal(0, counter.Count);
        }
    }

    [Fact]
    public async Task PreloadIncludesVariantAndParentPricesAndCachesMissingRows()
    {
        using var fixture = new Fixture();
        var product = new CatalogPriceProduct(Content("100"), fixture.Store);
        var variant = new TestVariant(Content("0", "-1,100,200,300"), fixture.Store, product);
        product.Variants.Add(variant);
        fixture.Prices.SetPrice(product.Key, "Web", "en-US", 150);
        using var scope = fixture.Prices.BeginReadScope();
        using var counter = new PriceQueryCounter(fixture.DatabasePath);
        await fixture.Prices.PreloadProductsAsync([product]);
        for (var i = 0; i < 20; i++) Assert.Equal(150, variant.Prices[0].BeforeDiscount.Value);
        Assert.Equal(0, counter.Count);
        await fixture.Prices.PreloadProductsAsync([product]);
        Assert.Equal(0, counter.Count);
    }

    [Fact]
    public async Task CancelledPreloadDoesNotPoisonScope()
    {
        using var fixture = new Fixture();
        var key = Guid.NewGuid();
        using var scope = fixture.Prices.BeginReadScope();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Prices.PreloadAsync([key], cancelled.Token));
        await fixture.Prices.PreloadAsync([key]);
        Assert.Empty(fixture.Prices.Get(key));
    }

    [Fact]
    public async Task StartupFillLoadsAllRowsAndPublicReadsCannotMutateCache()
    {
        using var fixture = new Fixture();
        var keys = Enumerable.Range(0, 801).Select(_ => Guid.NewGuid()).ToArray();
        using (var db = fixture.Factory.GetDatabase())
            foreach (var key in keys)
                db.Insert(new PriceData { NodeKey = key, PropertyAlias = "PRICE", StoreAlias = "WEB", Currency = "EN-US", Price = 123 });
        using var counter = new PriceQueryCounter(fixture.DatabasePath);
        fixture.Prices.FillCache();
        Assert.Equal(1, counter.Count);
        counter.Reset();
        foreach (var key in keys)
        {
            var row = Assert.Single(await fixture.Prices.GetAsync(key));
            row.Price = 999;
            Assert.Equal(123, Assert.Single(fixture.Prices.Get(key)).Price);
        }
        for (var i = 0; i < 20; i++) Assert.Empty(fixture.Prices.Get(Guid.NewGuid()));
        Assert.Equal(0, counter.Count);
    }

    [Fact]
    public async Task ConcurrentFillsAndWritesLeaveCacheEqualToCommittedRows()
    {
        using var fixture = new Fixture();
        var key = Guid.NewGuid();
        fixture.Prices.FillCache();
        await Task.WhenAll(Enumerable.Range(1, 12).Select(i => Task.Run(async () =>
        {
            if (i % 3 == 0) fixture.Prices.FillCache();
            await fixture.Prices.SetPriceAsync(key, "Web", i % 2 == 0 ? "en-US" : "en-GB", i);
        })));
        using var db = fixture.Factory.GetDatabase();
        var persisted = db.Prices.Where(row => row.NodeKey == key).ToList();
        var cached = fixture.Prices.Get(key);
        Assert.Equal(persisted.Count, cached.Count);
        foreach (var row in persisted)
            Assert.Equal(row.Price, cached.Single(value => value.Currency == row.Currency).Price);
        await fixture.Prices.RemovePriceAsync(key, "Web", "en-US");
        Assert.DoesNotContain(fixture.Prices.Get(key), row => row.Currency == "EN-US");
        fixture.Prices.RemovePrice(key, "Web", "en-GB");
        Assert.Empty(fixture.Prices.Get(key));
    }

    [Fact]
    public async Task NoOpWriteRefreshesCacheWithoutEmittingChange()
    {
        using var fixture = new Fixture();
        var key = Guid.NewGuid();
        fixture.Prices.SetPrice(key, "Web", "en-US", 10);
        using (var db = fixture.Factory.GetDatabase())
            db.Prices.Where(row => row.NodeKey == key).Set(row => row.Price, 20).Update();
        var observed = 0;
        EventHandler<PriceChangedEventArgs> handler = (_, args) => { if (args.Key == key) observed++; };
        PriceEvents.PriceChanged += handler;
        try
        {
            await fixture.Prices.SetPriceAsync(key, "Web", "en-US", 20);
            Assert.Equal(20, Assert.Single(fixture.Prices.Get(key)).Price);
            Assert.Equal(0, observed);
        }
        finally { PriceEvents.PriceChanged -= handler; }
    }

    [Fact]
    public async Task DisabledCacheDoesNotReadDatabaseAndReloadRemovesExternalDeletes()
    {
        using (var disabled = new Fixture(enabled: false))
        {
            using var counter = new PriceQueryCounter(disabled.DatabasePath);
            disabled.Prices.FillCache();
            await disabled.Prices.PreloadAsync([Guid.NewGuid()]);
            Assert.Empty(await disabled.Prices.GetAsync(Guid.NewGuid()));
            Assert.Equal(0, counter.Count);
        }
        using var fixture = new Fixture();
        var key = Guid.NewGuid();
        fixture.Prices.SetPrice(key, "Web", "en-US", 10);
        using (var db = fixture.Factory.GetDatabase()) db.Prices.Where(row => row.NodeKey == key).Delete();
        Assert.Single(fixture.Prices.Get(key));
        fixture.Prices.FillCache();
        Assert.Empty(fixture.Prices.Get(key));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CalculationKeepsBaseDiscountAndInheritedPricesTogetherDuringCommit(bool variantRead)
    {
        using var fixture = new Fixture();
        var product = new CatalogPriceProduct(Content("100"), fixture.Store);
        var variant = new TestVariant(Content("0", "-1,100,200,300"), fixture.Store, product);
        await fixture.Prices.ApplyAsync([new(product.Key, "Web", "en-US", 100),
            new(product.Key, "Web", "en-US", 80, "ekmDiscountPrice")]);
        var changed = false;
        decimal? subscriberValue = null;
        EventHandler<PriceChangedEventArgs> subscriber = (_, args) =>
        {
            if (args.Key == product.Key) subscriberValue = fixture.Prices.Get(product.Key).Single().Price;
        };
        async ValueTask Interleave(PriceCache.PriceGenerationEventArgs args, CancellationToken ct)
        {
            if (changed) return;
            changed = true;
            await Task.Run(() => fixture.Prices.ApplyAsync([new(product.Key, "Web", "en-US", 200),
                new(product.Key, "Web", "en-US", 160, "ekmDiscountPrice")]));
        }
        PriceCache.OnGenerationCreatedAsync += Interleave;
        PriceEvents.PriceChanged += subscriber;
        try
        {
            var overlapping = (variantRead ? variant.Prices : product.Prices)[0];
            Assert.Equal(100, overlapping.BeforeDiscount.Value);
            Assert.Equal(80, overlapping.AfterDiscount.Value);
            Assert.Equal(200m, subscriberValue);
        }
        finally
        {
            PriceCache.OnGenerationCreatedAsync -= Interleave;
            PriceEvents.PriceChanged -= subscriber;
        }
        using var counter = new PriceQueryCounter(fixture.DatabasePath);
        var next = (variantRead ? variant.Prices : product.Prices)[0];
        Assert.Equal(200, next.BeforeDiscount.Value);
        Assert.Equal(160, next.AfterDiscount.Value);
        Assert.Equal(0, counter.Count);
    }

    [Fact]
    public async Task AsyncRefreshQueriesAreAsyncAndReloadWaitDoesNotBlockCallerOrWarmReads()
    {
        using var fixture = new Fixture();
        fixture.Prices.FillCache();
        var key = Guid.NewGuid();
        var previous = LinqToDB.Data.DataConnection.DefaultOnTraceConnection;
        var level = LinqToDB.Data.DataConnection.TraceSwitch.Level;
        var asyncReads = new ConcurrentQueue<bool>();
        LinqToDB.Data.DataConnection.TurnTraceSwitchOn(System.Diagnostics.TraceLevel.Info);
        LinqToDB.Data.DataConnection.DefaultOnTraceConnection = info =>
        {
            if (info.TraceInfoStep == LinqToDB.Data.TraceInfoStep.BeforeExecute
                && info.DataConnection.ConnectionString?.Contains(fixture.DatabasePath, StringComparison.Ordinal) == true
                && info.SqlText?.Contains("SELECT", StringComparison.OrdinalIgnoreCase) == true
                && info.SqlText.Contains("EkomPrice", StringComparison.Ordinal)) asyncReads.Enqueue(info.IsAsync);
        };
        try
        {
            await fixture.Prices.SetPriceAsync(key, "Web", "en-US", 50);
            Assert.Equal(2, asyncReads.Count); // Mutation check plus post-commit refresh.
            Assert.All(asyncReads, value => Assert.True(value));
        }
        finally
        {
            LinqToDB.Data.DataConnection.DefaultOnTraceConnection = previous;
            LinqToDB.Data.DataConnection.TurnTraceSwitchOn(level);
        }
        var gate = (SemaphoreSlim)typeof(Prices).GetField("_refreshGate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(fixture.Prices)!;
        await gate.WaitAsync();
        var returned = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
        var caller = Task.Run(() => { returned.SetResult(fixture.Prices.FillCacheAsync()); });
        Task? pending = null;
        try
        {
            pending = await returned.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            using var counter = new PriceQueryCounter(fixture.DatabasePath);
            Assert.Equal(50, Assert.Single(await fixture.Prices.GetAsync(key)).Price);
            Assert.Equal(0, counter.Count);
        }
        finally { gate.Release(); }
        await caller;
        if (pending != null) await pending;
    }

    [Fact]
    public async Task FailedCalculationReleasesPinnedRowsAndCancelledReloadKeepsWarmCache()
    {
        using var fixture = new Fixture();
        var product = new CatalogPriceProduct(Content("100"), fixture.Store);
        fixture.Prices.SetPrice(product.Key, "Web", "en-US", 100);
        ValueTask Fail(PriceCache.PriceGenerationEventArgs args, CancellationToken ct)
            => throw new InvalidOperationException("calculation failed");
        PriceCache.OnGenerationCreatedAsync += Fail;
        try { Assert.Throws<InvalidOperationException>(() => product.Prices); }
        finally { PriceCache.OnGenerationCreatedAsync -= Fail; }
        await fixture.Prices.SetPriceAsync(product.Key, "Web", "en-US", 200);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Prices.FillCacheAsync(cancelled.Token));
        using var counter = new PriceQueryCounter(fixture.DatabasePath);
        Assert.Equal(200, product.Prices[0].BeforeDiscount.Value);
        Assert.Equal(0, counter.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefreshChunksCannotMixCommitsFromAConcurrentWriter(bool asyncWrite)
    {
        using var fixture = new Fixture();
        var keys = Enumerable.Range(0, 401).Select(_ => Guid.NewGuid()).OrderBy(key => key).ToArray();
        await fixture.Prices.ApplyAsync(keys.Select(key => new PriceMutation(key, "Web", "en-US", 100)));
        var previous = LinqToDB.Data.DataConnection.DefaultOnTraceConnection;
        var level = LinqToDB.Data.DataConnection.TraceSwitch.Level;
        using var continueRefresh = new ManualResetEventSlim();
        var betweenChunks = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var selects = 0;
        LinqToDB.Data.DataConnection.TurnTraceSwitchOn(System.Diagnostics.TraceLevel.Info);
        LinqToDB.Data.DataConnection.DefaultOnTraceConnection = info =>
        {
            if (info.TraceInfoStep != LinqToDB.Data.TraceInfoStep.BeforeExecute
                || info.DataConnection.ConnectionString?.Contains(fixture.DatabasePath, StringComparison.Ordinal) != true
                || info.SqlText?.Contains("SELECT", StringComparison.OrdinalIgnoreCase) != true
                || !info.SqlText.Contains("EkomPrice", StringComparison.Ordinal)) return;
            // 401 mutation checks, then the first refresh chunk. Pause before the second chunk.
            if (Interlocked.Increment(ref selects) != 403) return;
            betweenChunks.TrySetResult();
            if (!continueRefresh.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Refresh was not released");
        };
        var writer = Task.Run(async () =>
        {
            var changes = keys.Select(key => new PriceMutation(key, "Web", "en-US", 110));
            if (asyncWrite) await fixture.Prices.ApplyAsync(changes);
            else fixture.Prices.Apply(changes);
        });
        var concurrentCommit = false;
        try
        {
            await betweenChunks.Task.WaitAsync(TimeSpan.FromSeconds(10));
            // A separate transaction attempts to change nodes on opposite sides of the chunk boundary.
            // Serializable refresh reads must block this commit until both chunks have been read.
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(
                $"Data Source={fixture.DatabasePath};Pooling=False;Default Timeout=1");
            connection.Open();
            try
            {
                using var transaction = connection.BeginTransaction();
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "UPDATE EkomPrice SET Price = CASE WHEN NodeKey = $first THEN 200 ELSE 300 END WHERE NodeKey = $first OR NodeKey = $last";
                command.Parameters.AddWithValue("$first", keys[0].ToByteArray());
                command.Parameters.AddWithValue("$last", keys[^1].ToByteArray());
                Assert.Equal(2, command.ExecuteNonQuery());
                transaction.Commit();
                concurrentCommit = true;
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode is 5 or 6) { }
        }
        finally
        {
            continueRefresh.Set();
            try { await writer.WaitAsync(TimeSpan.FromSeconds(10)); }
            finally
            {
                LinqToDB.Data.DataConnection.DefaultOnTraceConnection = previous;
                LinqToDB.Data.DataConnection.TurnTraceSwitchOn(level);
            }
        }
        Assert.Equal(110, Assert.Single(fixture.Prices.Get(keys[0])).Price);
        Assert.Equal(110, Assert.Single(fixture.Prices.Get(keys[^1])).Price);
        Assert.False(concurrentCommit);
        Assert.Equal(403, selects); // Still two refresh SELECTs, not one query per node.
        await fixture.Prices.ApplyAsync([new(keys[0], "Web", "en-US", 200), new(keys[^1], "Web", "en-US", 300)]);
        using var counter = new PriceQueryCounter(fixture.DatabasePath);
        Assert.Equal(200, Assert.Single(fixture.Prices.Get(keys[0])).Price);
        Assert.Equal(300, Assert.Single(fixture.Prices.Get(keys[^1])).Price);
        Assert.Equal(0, counter.Count);
    }

    private sealed class CatalogPriceProduct(UmbracoContent content, IStore store) : Product(content, store)
    {
        public List<IVariant> Variants { get; } = new();
        public override IEnumerable<IVariant> AllVariants => Variants;
        public override IVariant? PrimaryVariant => Variants.FirstOrDefault();
    }

    private sealed class PriceQueryCounter : IDisposable
    {
        private readonly Action<LinqToDB.Data.TraceInfo> _previous = LinqToDB.Data.DataConnection.DefaultOnTraceConnection;
        private readonly System.Diagnostics.TraceLevel _level = LinqToDB.Data.DataConnection.TraceSwitch.Level;
        private int _count;
        public int Count => _count;
        public void Reset() => _count = 0;
        public PriceQueryCounter(string path)
        {
            LinqToDB.Data.DataConnection.TurnTraceSwitchOn(System.Diagnostics.TraceLevel.Info);
            LinqToDB.Data.DataConnection.DefaultOnTraceConnection = info =>
            {
                if (info.TraceInfoStep == LinqToDB.Data.TraceInfoStep.BeforeExecute
                    && info.DataConnection.ConnectionString?.Contains(path, StringComparison.Ordinal) == true
                    && info.SqlText?.Contains("EkomPrice", StringComparison.Ordinal) == true)
                    Interlocked.Increment(ref _count);
            };
        }
        public void Dispose()
        {
            LinqToDB.Data.DataConnection.DefaultOnTraceConnection = _previous;
            LinqToDB.Data.DataConnection.TurnTraceSwitchOn(_level);
        }
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

    internal sealed class Fixture : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"ekom-prices-{Guid.NewGuid():N}.db");
        private readonly ConfigurationScope _configuration;
        private readonly MemoryCache _memory = new(new MemoryCacheOptions());
        public string DatabasePath => _path;
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
            store.SetupGet(x => x.Cultures).Returns([]);
            store.SetupGet(x => x.VatIncludedInPrice).Returns(true);
            Store = store.Object;
            var discounts = new Mock<IPerStoreCache<IProductDiscount>>();
            discounts.SetupGet(x => x.Cache).Returns(new ConcurrentDictionary<string, ConcurrentDictionary<Guid, IProductDiscount>>());
            var stores = new Mock<IStoreService>();
            stores.Setup(x => x.GetStoreByAlias("Web")).Returns(Store);
            stores.Setup(x => x.GetStoreFromCache()).Returns(Store);
            stores.Setup(x => x.GetAllStores()).Returns([Store]);
            _configuration = new ConfigurationScope(overrides: enabled ? new Dictionary<string, string?> { ["Ekom:UseDatabasePrices"] = "true" } : null,
                addServices: services =>
                {
                    services.AddSingleton(Factory);
                    services.AddSingleton(Mock.Of<IMetafieldService>());
                    services.AddSingleton<Prices>();
                    var stockCache = new Mock<IBaseCache<StockData>>();
                    stockCache.SetupGet(x => x.Cache).Returns(new ConcurrentDictionary<Guid, StockData>());
                    services.AddSingleton(sp => new Ekom.API.Stock(sp.GetRequiredService<Configuration>(),
                        NullLogger<Ekom.API.Stock>.Instance, stockCache.Object, null!, null!, stores.Object,
                        null!, null!, null!));
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

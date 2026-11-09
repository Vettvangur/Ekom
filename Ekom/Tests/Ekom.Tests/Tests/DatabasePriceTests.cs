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

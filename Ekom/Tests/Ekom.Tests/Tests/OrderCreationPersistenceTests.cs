using Ekom.API;
using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using Ekom.Tests.Objects;
using Ekom.Tracking;
using Ekom.Utilities;
using LinqToDB;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Collections.Concurrent;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class OrderCreationPersistenceTests
{
    [Fact]
    public async Task FirstAddCreatesAndPersistsANewCartWithoutAnExistingCookieOrOrder()
    {
        using var fixture = new Fixture();
        await fixture.SeedStockAsync();
        using (var db = fixture.Database.Factory.GetDatabase())
        {
            Assert.Empty(await db.OrderData.ToListAsync());
        }
        Assert.Null(await fixture.Service.GetOrderAsync("main"));

        var created = await fixture.Service.AddOrderLineAsync(fixture.ProductKey, 1, "main",
            new AddOrderSettings { FireEvents = false, FireOnOrderUpdatedEvent = false });

        Assert.NotEqual(Guid.Empty, created.UniqueId);
        Assert.True(created.ReferenceId > 0);
        Assert.False(string.IsNullOrWhiteSpace(created.OrderNumber));
        Assert.Equal("main", created.StoreInfo.Alias);
        Assert.Equal("USD", created.StoreInfo.Currency.ISOCurrencySymbol);
        Assert.Equal(fixture.ProductKey, Assert.Single(created.OrderLines).ProductKey);
        Assert.Contains($"ekmOrder-main={created.UniqueId}", fixture.Context.Response.Headers.SetCookie.ToString());
        using var persistedDb = fixture.Database.Factory.GetDatabase();
        var data = Assert.Single(await persistedDb.OrderData.ToListAsync());
        Assert.Equal(created.UniqueId, data.UniqueId);
        Assert.Equal(created.OrderNumber, data.OrderNumber);
        Assert.False(string.IsNullOrWhiteSpace(data.OrderInfo));
        var persisted = new OrderInfo(data);
        Assert.Equal("main", persisted.StoreInfo.Alias);
        Assert.Equal("USD", persisted.StoreInfo.Currency.ISOCurrencySymbol);
        Assert.Equal(1, Assert.Single(persisted.OrderLines).Quantity);
        Assert.Equal(OrderStatus.Incomplete, persisted.OrderStatus);
        Assert.Null((await persistedDb.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
        Assert.Empty(await persistedDb.GetTable<CheckoutPaymentAttemptData>().ToListAsync());
        Assert.Equal(3, await fixture.Database.StockAsync(fixture.ProductKey));
        Assert.Empty(await persistedDb.StockReservations.ToListAsync());

        fixture.Context.Request.Headers.Cookie = $"ekmOrder-main={created.UniqueId}";
        Assert.Equal(created.UniqueId, (await fixture.Service.GetOrderAsync("main"))!.UniqueId);
    }

    [Fact]
    public async Task InsertedInstanceCanBeUpdatedBeforeReloadAndTracksEachSuccessfulVersion()
    {
        using var fixture = new Fixture();
        var data = NewOrder();
        await fixture.Repository.InsertOrderAsync(data);
        Assert.True(data.ReferenceId > 0);

        data.OrderNumber = $"order-{data.ReferenceId}";
        data.OrderInfo = "first persisted payload";
        data.UpdateDate = data.UpdateDate.AddSeconds(1);
        await fixture.Repository.UpdateOrderAsync(data);
        data.OrderInfo = "second persisted payload";
        data.UpdateDate = data.UpdateDate.AddSeconds(1);
        await fixture.Repository.UpdateOrderAsync(data);

        var persisted = (await fixture.Repository.GetOrderAsync(data.UniqueId))!;
        Assert.Equal(data.ReferenceId, persisted.ReferenceId);
        Assert.Equal(data.OrderNumber, persisted.OrderNumber);
        Assert.Equal(data.OrderInfo, persisted.OrderInfo);
        Assert.Equal(data.UpdateDate, persisted.UpdateDate);
    }

    [Fact]
    public async Task InsertTrackingDoesNotAuthorizeAnUntrackedStaleCloneOrAStaleInsertedInstance()
    {
        using var fixture = new Fixture();
        var inserted = NewOrder();
        await fixture.Repository.InsertOrderAsync(inserted);
        var untracked = (OrderData)inserted.Clone();
        var fresh = (await fixture.Repository.GetOrderAsync(inserted.UniqueId))!;
        fresh.OrderInfo = "current payload";
        fresh.OrderStatus = OrderStatus.ReadyForDispatch;
        fresh.UpdateDate = fresh.UpdateDate.AddSeconds(1);
        await fixture.Repository.UpdateOrderAsync(fresh);

        untracked.OrderInfo = "stale clone overwrite";
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Repository.UpdateOrderAsync(untracked));
        Assert.Contains("untracked snapshot", error.Message);
        inserted.OrderInfo = "stale inserted instance overwrite";
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Repository.UpdateOrderAsync(inserted));

        var persisted = (await fixture.Repository.GetOrderAsync(inserted.UniqueId))!;
        Assert.Equal(fresh.OrderInfo, persisted.OrderInfo);
        Assert.Equal(fresh.OrderStatus, persisted.OrderStatus);
        Assert.Equal(fresh.UpdateDate, persisted.UpdateDate);
    }

    private static OrderData NewOrder() => new()
    {
        UniqueId = Guid.NewGuid(),
        StoreAlias = "main",
        Currency = "USD",
        OrderStatus = OrderStatus.Incomplete,
        CreateDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdateDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(1234567),
    };

    private sealed class Fixture : IDisposable
    {
        private readonly ConfigurationScope _scope;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());

        public Fixture()
        {
            var currency = new CurrencyModel { CurrencyValue = "en-US" };
            var store = new Mock<IStore>();
            store.SetupGet(x => x.Alias).Returns("main");
            store.SetupGet(x => x.Currency).Returns(currency);
            store.SetupGet(x => x.Culture).Returns(new CultureInfoDto { Name = "en-US" });
            store.SetupGet(x => x.Cultures).Returns([new CultureInfoDto { Name = "en-US" }]);
            store.SetupGet(x => x.Currencies).Returns([currency]);
            var stores = new Mock<IStoreService>();
            stores.Setup(x => x.GetStoreByAlias("main")).Returns(store.Object);
            stores.Setup(x => x.GetStoreFromCache()).Returns(store.Object);
            var product = new Mock<IProduct>();
            product.SetupGet(x => x.Key).Returns(ProductKey);
            product.SetupGet(x => x.Stock).Returns(() => Ekom.API.Stock.Instance.GetStock(ProductKey, "main"));
            product.SetupGet(x => x.Url).Returns("");
            product.SetupGet(x => x.AllVariants).Returns(Array.Empty<IVariant>());
            product.SetupGet(x => x.Prices).Returns([new Price(10, currency, 0, false)]);
            product.SetupGet(x => x.Properties).Returns(new Dictionary<string, string>
            {
                ["__Key"] = ProductKey.ToString(), ["title"] = "Product", ["sku"] = "product",
            });
            var products = new Mock<IPerStoreIndexedCache<IProduct>>();
            IProduct? cachedProduct = product.Object;
            products.Setup(x => x.TryGetByKey("main", ProductKey, out cachedProduct)).Returns(true);
            var discounts = new Mock<IPerStoreCache<IDiscount>>();
            discounts.SetupGet(x => x.Cache).Returns(new ConcurrentDictionary<string, ConcurrentDictionary<Guid, IDiscount>>
            {
                ["main"] = new(),
            });
            _scope = new ConfigurationScope(addServices: services =>
            {
                services.AddSingleton(Database.NewStockApi());
                services.AddSingleton(Database.NewCheckout());
                services.AddSingleton(new Ekom.API.Store(stores.Object, Mock.Of<ICacheRefreshService>()));
                services.AddSingleton(sp => new Providers(sp.GetRequiredService<Configuration>(), NullLogger<Providers>.Instance,
                    Mock.Of<IPerStoreCache<IShippingProvider>>(x => x["main"] == new ConcurrentDictionary<Guid, IShippingProvider>()),
                    Mock.Of<IPerStoreCache<IPaymentProvider>>(x => x["main"] == new ConcurrentDictionary<Guid, IPaymentProvider>()),
                    Mock.Of<IBaseCache<IZone>>(), stores.Object, null!));
                services.AddSingleton(sp => new Discounts(sp.GetRequiredService<Configuration>(), NullLogger<Discounts>.Instance,
                    discounts.Object, stores.Object));
                services.AddSingleton(sp => new Catalog(NullLogger<Catalog>.Instance, sp.GetRequiredService<Configuration>(),
                    sp.GetRequiredService<IServiceScopeFactory>(), products.Object, Mock.Of<IPerStoreIndexedCache<ICategory>>(),
                    Mock.Of<IPerStoreCache<IProductDiscount>>(), Mock.Of<IPerStoreIndexedCache<IVariant>>(),
                    Mock.Of<IPerStoreIndexedCache<IVariantGroup>>(), stores.Object,
                    new HttpContextAccessor(), Mock.Of<IProductFilterService>()));
            });
            using var db = Database.Factory.GetDatabase();
            db.CreateTable<OrderData>();
            db.CreateTable<OrderActivityLog>();
            db.CreateTable<CheckoutPaymentOperationData>(tableOptions: TableOptions.CreateIfNotExists);
            db.CreateTable<CheckoutPaymentAttemptData>(tableOptions: TableOptions.CreateIfNotExists);
            var attempts = new CheckoutPaymentAttemptService(Database.Factory, Database.Service,
                new ActivityLogRepository(NullLogger<ActivityLogRepository>.Instance, Database.Factory),
                NullLogger<CheckoutPaymentAttemptService>.Instance, _cache);
            Repository = new OrderRepository(NullLogger<OrderRepository>.Instance, _scope.Instance, Database.Factory, _cache, attempts);
            Service = new OrderService(_scope.Instance, Repository, null!, Mock.Of<IOrderActivityLogService>(),
                NullLogger<OrderService>.Instance, stores.Object, _cache, Mock.Of<IMemberService>(), null!,
                Mock.Of<IOrderTrackingService>(), new HttpContextAccessor { HttpContext = Context }, paymentAttempts: attempts);
        }

        public StockReservationTests.ReservationDatabase Database { get; } = new();
        public Guid ProductKey { get; } = Guid.NewGuid();
        public DefaultHttpContext Context { get; } = new();
        public OrderRepository Repository { get; }
        public OrderService Service { get; }

        public async Task SeedStockAsync()
        {
            await Database.SeedAsync(3, key: ProductKey);
            Database.StockCache[ProductKey] = new StockData
            {
                UniqueId = ProductKey.ToString(), Stock = 3, CreateDate = DateTime.UtcNow, UpdateDate = DateTime.UtcNow,
            };
        }

        public void Dispose()
        {
            _scope.Dispose();
            _cache.Dispose();
            Database.Dispose();
        }
    }
}

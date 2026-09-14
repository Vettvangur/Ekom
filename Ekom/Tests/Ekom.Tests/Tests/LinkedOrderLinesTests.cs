using Ekom.API;
using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using Ekom.Tests.Objects;
using Ekom.Tracking;
using LinqToDB;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class LinkedOrderLinesTests
{
    [Fact]
    public async Task AddLinkedOrderLinesAsync_AddsParentAndLinkedChildren()
    {
        using var fixture = new Fixture();
        Guid parentProductKey = fixture.AddProduct();
        Guid markingProductKey = fixture.AddProduct();
        Guid packagingProductKey = fixture.AddProduct();

        OrderInfo result = await fixture.Service.AddLinkedOrderLinesAsync(
            parentProductKey,
            1,
            "main",
            [
                new LinkedOrderLineRequest
                {
                    ProductId = markingProductKey,
                    Quantity = 2,
                    CustomData = new Dictionary<string, string>
                    {
                        ["orderlineName"] = "Anna",
                        ["ignored"] = "value",
                    },
                },
                new LinkedOrderLineRequest
                {
                    ProductId = packagingProductKey,
                    Quantity = 3,
                },
            ],
            new AddOrderSettings
            {
                OrderInfo = fixture.Order,
                FireEvents = false,
            });

        Assert.Equal(3, result.OrderLines.Count);
        IOrderLine parent = Assert.Single(result.OrderLines, x => x.ProductKey == parentProductKey);
        IOrderLine marking = Assert.Single(result.OrderLines, x => x.ProductKey == markingProductKey);
        IOrderLine packaging = Assert.Single(result.OrderLines, x => x.ProductKey == packagingProductKey);
        Assert.Equal(Guid.Empty, parent.Settings.Link);
        Assert.Equal(parent.Key, marking.Settings.Link);
        Assert.Equal(parent.Key, packaging.Settings.Link);
        Assert.Equal(2, marking.Quantity);
        Assert.Equal(3, packaging.Quantity);
        Assert.Equal("Anna", marking.OrderLineInfo.Properties["orderlineName"]);
        Assert.False(marking.OrderLineInfo.Properties.ContainsKey("ignored"));

        OrderInfo reloaded = await fixture.ReloadAsync();
        Assert.Equal(parent.Key, Assert.Single(reloaded.OrderLines, x => x.ProductKey == markingProductKey).Settings.Link);
    }

    [Fact]
    public async Task AddLinkedOrderLinesAsync_InvalidChildLeavesOrderUnchanged()
    {
        using var fixture = new Fixture();
        Guid parentProductKey = fixture.AddProduct();

        await Assert.ThrowsAsync<Ekom.Exceptions.ProductNotFoundException>(() =>
            fixture.Service.AddLinkedOrderLinesAsync(
                parentProductKey,
                1,
                "main",
                [new LinkedOrderLineRequest { ProductId = Guid.NewGuid(), Quantity = 1 }],
                new AddOrderSettings
                {
                    OrderInfo = fixture.Order,
                    FireEvents = false,
                }));

        Assert.Empty(fixture.Order.OrderLines);
        Assert.Empty((await fixture.ReloadAsync()).OrderLines);
    }

    [Fact]
    public async Task AddLinkedOrderLinesAsync_ValidatesCombinedStockBeforePersisting()
    {
        using var fixture = new Fixture();
        Guid parentProductKey = fixture.AddProduct();
        Guid childProductKey = fixture.AddProduct(stock: 10);

        await Assert.ThrowsAsync<Ekom.Exceptions.NotEnoughStockException>(() =>
            fixture.Service.AddLinkedOrderLinesAsync(
                parentProductKey,
                1,
                "main",
                [
                    new LinkedOrderLineRequest { ProductId = childProductKey, Quantity = 6 },
                    new LinkedOrderLineRequest { ProductId = childProductKey, Quantity = 6 },
                ],
                new AddOrderSettings
                {
                    OrderInfo = fixture.Order,
                    FireEvents = false,
                }));

        Assert.Empty(fixture.Order.OrderLines);
        Assert.Empty((await fixture.ReloadAsync()).OrderLines);
    }

    [Fact]
    public async Task AddLinkedOrderLinesAsync_AlwaysCreatesANewParentGroup()
    {
        using var fixture = new Fixture();
        Guid parentProductKey = fixture.AddProduct();
        Guid childProductKey = fixture.AddProduct();
        LinkedOrderLineRequest[] children =
        [
            new LinkedOrderLineRequest { ProductId = childProductKey, Quantity = 1 },
        ];

        await fixture.Service.AddLinkedOrderLinesAsync(
            parentProductKey,
            1,
            "main",
            children,
            new AddOrderSettings { OrderInfo = fixture.Order, FireEvents = false });
        OrderInfo second = await fixture.Service.AddLinkedOrderLinesAsync(
            parentProductKey,
            1,
            "main",
            children,
            new AddOrderSettings { OrderInfo = fixture.Order, FireEvents = false });

        IOrderLine[] parents = second.OrderLines.Where(x => x.ProductKey == parentProductKey).ToArray();
        IOrderLine[] linkedChildren = second.OrderLines.Where(x => x.ProductKey == childProductKey).ToArray();
        Assert.Equal(2, parents.Length);
        Assert.Equal(2, linkedChildren.Length);
        Assert.NotEqual(parents[0].Key, parents[1].Key);
        Assert.All(linkedChildren, child => Assert.Contains(parents, parent => parent.Key == child.Settings.Link));
        Assert.NotEqual(linkedChildren[0].Settings.Link, linkedChildren[1].Settings.Link);
    }

    [Fact]
    public async Task AddLinkedOrderLinesAsync_RejectsSeparateCustomerUpdateFlow()
    {
        using var fixture = new Fixture();
        Guid parentProductKey = fixture.AddProduct();
        Guid childProductKey = fixture.AddProduct();

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.AddLinkedOrderLinesAsync(
            parentProductKey,
            1,
            "main",
            [
                new LinkedOrderLineRequest
                {
                    ProductId = childProductKey,
                    CustomData = new Dictionary<string, string>
                    {
                        ["ekomUpdateInformation"] = "true",
                    },
                },
            ],
            new AddOrderSettings { OrderInfo = fixture.Order, FireEvents = false }));

        Assert.Empty((await fixture.ReloadAsync()).OrderLines);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ConfigurationScope _scope;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        private readonly Mock<IPerStoreIndexedCache<IProduct>> _products = new();
        private readonly StockReservationTests.ReservationDatabase _database = new();

        public Fixture()
        {
            var store = new Mock<IStore>();
            store.SetupGet(x => x.Alias).Returns("main");
            store.SetupGet(x => x.Culture).Returns(new CultureInfoDto { Name = "en-US" });
            store.SetupGet(x => x.Cultures).Returns([new CultureInfoDto { Name = "en-US" }]);

            var stores = new Mock<IStoreService>();
            stores.Setup(x => x.GetStoreByAlias("main")).Returns(store.Object);
            stores.Setup(x => x.GetStoreFromCache()).Returns(store.Object);

            var variants = new Mock<IPerStoreIndexedCache<IVariant>>();
            variants.SetupGet(x => x.Cache).Returns(
                new ConcurrentDictionary<string, ConcurrentDictionary<Guid, IVariant>>
                {
                    ["main"] = new(),
                });
            var discounts = new Mock<IPerStoreCache<IDiscount>>();
            discounts.SetupGet(x => x.Cache).Returns(
                new ConcurrentDictionary<string, ConcurrentDictionary<Guid, IDiscount>>
                {
                    ["main"] = new(),
                });

            _scope = new ConfigurationScope(addServices: services =>
            {
                services.AddSingleton(_database.NewStockApi());
                services.AddSingleton(new Ekom.API.Store(stores.Object, Mock.Of<ICacheRefreshService>()));
                services.AddSingleton(sp => new Discounts(
                    sp.GetRequiredService<Configuration>(),
                    NullLogger<Discounts>.Instance,
                    discounts.Object,
                    stores.Object));
                services.AddSingleton(sp => new Catalog(
                    NullLogger<Catalog>.Instance,
                    sp.GetRequiredService<Configuration>(),
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    _products.Object,
                    Mock.Of<IPerStoreIndexedCache<ICategory>>(),
                    Mock.Of<IPerStoreCache<IProductDiscount>>(),
                    variants.Object,
                    Mock.Of<IPerStoreIndexedCache<IVariantGroup>>(),
                    stores.Object,
                    new Microsoft.AspNetCore.Http.HttpContextAccessor(),
                    Mock.Of<IProductFilterService>()));
            });

            using var db = _database.Factory.GetDatabase();
            db.CreateTable<OrderData>();

            var currency = new CurrencyModel { CurrencyValue = "en-US" };
            var storeInfo = new StoreInfo(Guid.NewGuid(), currency, [currency], "en-US", "main", false, 0, false);
            var data = new OrderData
            {
                UniqueId = Guid.NewGuid(),
                OrderStatusCol = "Incomplete",
                OrderInfo = JsonConvert.SerializeObject(new
                {
                    StoreInfo = storeInfo,
                    OrderLines = Array.Empty<object>(),
                    CustomerInformation = new CustomerInfo(),
                }),
            };
            db.Insert(data);

            Order = new OrderInfo(data);
            Repository = new OrderRepository(
                NullLogger<OrderRepository>.Instance,
                _scope.Instance,
                _database.Factory,
                _cache);
            Service = new OrderService(
                _scope.Instance,
                Repository,
                null!,
                Mock.Of<IOrderActivityLogService>(),
                NullLogger<OrderService>.Instance,
                stores.Object,
                _cache,
                Mock.Of<IMemberService>(),
                null!,
                Mock.Of<IOrderTrackingService>());
        }

        public OrderInfo Order { get; }
        public OrderRepository Repository { get; }
        public OrderService Service { get; }

        public Guid AddProduct(decimal stock = 100)
        {
            Guid key = Guid.NewGuid();
            var product = new Mock<IProduct>();
            product.SetupGet(x => x.Key).Returns(key);
            product.SetupGet(x => x.Stock).Returns(stock);
            product.SetupGet(x => x.StockBuffer).Returns(0);
            product.SetupGet(x => x.AllVariants).Returns(Array.Empty<IVariant>());
            product.SetupGet(x => x.Properties).Returns(new Dictionary<string, string>
            {
                ["__Key"] = key.ToString(),
                ["title"] = "Product",
                ["sku"] = key.ToString(),
            });
            product.SetupGet(x => x.Prices).Returns(
                [new Price(10, Order.StoreInfo.Currency, 0, false)]);
            product.Setup(x => x.ProductDiscountAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDiscount?)null);

            IProduct? cached = product.Object;
            _products.Setup(x => x.TryGetByKey("main", key, out cached)).Returns(true);
            return key;
        }

        public async Task<OrderInfo> ReloadAsync()
            => new((await Repository.GetOrderAsync(Order.UniqueId))!);

        public void Dispose()
        {
            _scope.Dispose();
            _cache.Dispose();
            _database.Dispose();
        }
    }
}

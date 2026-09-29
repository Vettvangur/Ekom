using Ekom.API;
using Ekom.Cache;
using Ekom.Events;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using Ekom.Tests.Objects;
using Ekom.Tracking;
using Ekom.Utilities;
using LinqToDB;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
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

    [Fact]
    public async Task UpdateOrderLineMetadataAsync_MergesMultipleLinesWithoutChangingOrderTotals()
    {
        using var fixture = new Fixture();
        var order = await AddTwoLinesAsync(fixture);
        var lines = order.OrderLines.ToArray();
        var before = await fixture.Repository.GetOrderAsync(order.UniqueId);

        var updated = await fixture.Service.UpdateOrderLineMetadataAsync(order.UniqueId,
        [
            new OrderLineMetadataUpdate
            {
                LineId = lines[0].Key,
                Properties = new Dictionary<string, string>
                {
                    ["orderlineWarehouse"] = "North",
                    ["orderlineBatch"] = "A123",
                    ["OrderLineNote"] = "Updated",
                },
            },
            new OrderLineMetadataUpdate
            {
                LineId = lines[1].Key,
                Properties = new Dictionary<string, string> { ["orderlineWarehouse"] = "South" },
            },
        ], new OrderSettings { FireEvents = false });

        var reloaded = await fixture.ReloadAsync();
        Assert.Equal(2, reloaded.OrderLines.Count);
        Assert.Equal("North", Assert.Single(reloaded.OrderLines, x => x.Key == lines[0].Key).OrderLineInfo.Properties["orderlineWarehouse"]);
        Assert.Equal("A123", Assert.Single(reloaded.OrderLines, x => x.Key == lines[0].Key).OrderLineInfo.Properties["orderlineBatch"]);
        Assert.Equal("South", Assert.Single(reloaded.OrderLines, x => x.Key == lines[1].Key).OrderLineInfo.Properties["orderlineWarehouse"]);
        Assert.Equal("Updated", Assert.Single(reloaded.OrderLines, x => x.Key == lines[0].Key).OrderLineInfo.Properties["orderlineNote"]);
        Assert.Equal("Existing", Assert.Single(reloaded.OrderLines, x => x.Key == lines[0].Key).OrderLineInfo.Properties["orderlineSource"]);
        Assert.Equal(lines.Select(x => x.Quantity), updated.OrderLines.Select(x => x.Quantity));
        Assert.Equal(before!.TotalAmount, (await fixture.Repository.GetOrderAsync(order.UniqueId))!.TotalAmount);
        Assert.Equal("North", Assert.Single((await fixture.Service.GetOrderAsync(order.UniqueId))!.OrderLines, x => x.Key == lines[0].Key).OrderLineInfo.Properties["orderlineWarehouse"]);

        var managerJson = JObject.FromObject(Ekom.Controllers.EkomManagerController.GetOrderInfoResponse(reloaded)!,
            JsonSerializer.Create(new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }));
        Assert.Equal("North", managerJson["orderLines"]?
            .FirstOrDefault(x => x["key"]?.Value<Guid>() == lines[0].Key)?["orderLineInfo"]?["properties"]?["orderlineWarehouse"]?.Value<string>());
    }

    [Fact]
    public async Task UpdateOrderLineMetadataAsync_InvalidBatchLeavesSavedOrderUnchanged()
    {
        using var fixture = new Fixture();
        var order = await AddTwoLinesAsync(fixture);
        var before = (await fixture.Repository.GetOrderAsync(order.UniqueId))!.OrderInfo;

        await Assert.ThrowsAsync<Ekom.Exceptions.OrderLineNotFoundException>(() => fixture.Service.UpdateOrderLineMetadataAsync(
            order.UniqueId,
            [
                new OrderLineMetadataUpdate { LineId = order.OrderLines.First().Key, Properties = new Dictionary<string, string> { ["orderlineWarehouse"] = "North" } },
                new OrderLineMetadataUpdate { LineId = Guid.NewGuid(), Properties = new Dictionary<string, string> { ["orderlineWarehouse"] = "South" } },
            ], new OrderSettings { FireEvents = false }));

        Assert.Equal(before, (await fixture.Repository.GetOrderAsync(order.UniqueId))!.OrderInfo);
        Assert.DoesNotContain((await fixture.ReloadAsync()).OrderLines, line => line.OrderLineInfo.Properties.ContainsKey("orderlineWarehouse"));
    }

    [Fact]
    public async Task UpdateOrderLineMetadataAsync_WorksOnCompletedOrder()
    {
        using var fixture = new Fixture();
        var order = await AddTwoLinesAsync(fixture);
        var data = (await fixture.Repository.GetOrderAsync(order.UniqueId))!;
        data.OrderStatus = OrderStatus.Closed;
        await fixture.Repository.UpdateOrderAsync(data);

        var updated = await fixture.Service.UpdateOrderLineMetadataAsync(order.UniqueId,
            [new OrderLineMetadataUpdate { LineId = order.OrderLines.First().Key, Properties = new Dictionary<string, string> { ["orderlineWarehouse"] = "North" } }],
            new OrderSettings { FireEvents = false });

        Assert.Equal(OrderStatus.Closed, updated.OrderStatus);
        Assert.Equal("North", Assert.Single((await fixture.ReloadAsync()).OrderLines, x => x.Key == order.OrderLines.First().Key).OrderLineInfo.Properties["orderlineWarehouse"]);
    }

    [Fact]
    public async Task UpdateOrderLineMetadataAsync_DoesNotRevalidateSelectedProviders()
    {
        using var fixture = new Fixture();
        var order = await AddTwoLinesAsync(fixture);
        var data = (await fixture.Repository.GetOrderAsync(order.UniqueId))!;
        var orderJson = JObject.Parse(data.OrderInfo);
        var shippingKey = Guid.NewGuid();
        var paymentKey = Guid.NewGuid();
        orderJson["ShippingProvider"] = new JObject { ["Id"] = 1, ["Key"] = shippingKey, ["Title"] = "Pickup" };
        orderJson["PaymentProvider"] = new JObject { ["Id"] = 2, ["Key"] = paymentKey, ["Title"] = "Invoice" };
        data.OrderInfo = orderJson.ToString();
        await fixture.Repository.UpdateOrderAsync(data);

        var updated = await fixture.Service.UpdateOrderLineMetadataAsync(order.UniqueId,
            [new OrderLineMetadataUpdate { LineId = order.OrderLines.First().Key, Properties = new Dictionary<string, string> { ["orderlineWarehouse"] = "North" } }],
            new OrderSettings { FireEvents = false });

        Assert.Equal(shippingKey, updated.ShippingProvider?.Key);
        Assert.Equal(paymentKey, updated.PaymentProvider?.Key);
        var reloaded = await fixture.ReloadAsync();
        Assert.Equal(shippingKey, reloaded.ShippingProvider?.Key);
        Assert.Equal(paymentKey, reloaded.PaymentProvider?.Key);
    }

    [Theory]
    [InlineData(OrderStatus.Incomplete, false)]
    [InlineData(OrderStatus.WaitingForPayment, true)]
    [InlineData(OrderStatus.ReadyForDispatch, true)]
    [InlineData(OrderStatus.Closed, true)]
    public async Task CustomerCountryUpdate_ValidatesProvidersOnlyWhileOrderIsIncomplete(OrderStatus status, bool preserveProviders)
    {
        using var fixture = new Fixture();
        var order = await AddTwoLinesAsync(fixture);
        var data = (await fixture.Repository.GetOrderAsync(order.UniqueId))!;
        var orderJson = JObject.Parse(data.OrderInfo);
        var shipping = new JObject { ["Id"] = 1, ["Key"] = Guid.NewGuid(), ["Title"] = "Pickup", ["Price"] = new JObject { ["OriginalValue"] = 5 } };
        var payment = new JObject { ["Id"] = 2, ["Key"] = Guid.NewGuid(), ["Title"] = "Invoice", ["Price"] = new JObject { ["OriginalValue"] = 2 } };
        orderJson["ShippingProvider"] = shipping;
        orderJson["PaymentProvider"] = payment;
        data.OrderInfo = orderJson.ToString();
        data.OrderStatus = status;
        await fixture.Repository.UpdateOrderAsync(data);

        await fixture.Service.UpdateCustomerInformationAsync(
            new Dictionary<string, string> { ["storeAlias"] = "main", ["customerCountry"] = "DK" },
            new OrderSettings { OrderInfo = await fixture.ReloadAsync(), FireEvents = false });

        var saved = JObject.Parse((await fixture.Repository.GetOrderAsync(order.UniqueId))!.OrderInfo);
        if (preserveProviders)
        {
            Assert.Equal(shipping["Key"], saved["ShippingProvider"]?["Key"]);
            Assert.Equal(shipping["Title"], saved["ShippingProvider"]?["Title"]);
            Assert.Equal(5, saved["ShippingProvider"]?["Price"]?["OriginalValue"]?.Value<int>());
            Assert.Equal(payment["Key"], saved["PaymentProvider"]?["Key"]);
            Assert.Equal(payment["Title"], saved["PaymentProvider"]?["Title"]);
            Assert.Equal(2, saved["PaymentProvider"]?["Price"]?["OriginalValue"]?.Value<int>());
            fixture.ActivityLogs.Verify(x => x.AddOrderLogAsync(order.UniqueId,
                It.Is<string>(message => message.StartsWith("Shipping provider removed", StringComparison.Ordinal)),
                It.IsAny<string?>(), OrderActivityLogType.Info, It.IsAny<CancellationToken>()), Times.Never);
        }
        else
        {
            Assert.True(saved["ShippingProvider"] == null || saved["ShippingProvider"]!.Type == JTokenType.Null);
            Assert.True(saved["PaymentProvider"] == null || saved["PaymentProvider"]!.Type == JTokenType.Null);
            fixture.ActivityLogs.Verify(x => x.AddOrderLogAsync(order.UniqueId,
                It.Is<string>(message => message.Contains(shipping["Key"]!.ToString(), StringComparison.Ordinal)
                    && message.Contains("Shipping provider removed", StringComparison.Ordinal)),
                It.IsAny<string?>(), OrderActivityLogType.Info, It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact]
    public async Task FailedOrderUpdate_DoesNotLogShippingProviderRemoval()
    {
        using var fixture = new Fixture();
        var order = await AddTwoLinesAsync(fixture);
        var data = (await fixture.Repository.GetOrderAsync(order.UniqueId))!;
        var orderJson = JObject.Parse(data.OrderInfo);
        orderJson["ShippingProvider"] = new JObject
        {
            ["Id"] = 1,
            ["Key"] = Guid.NewGuid(),
            ["Title"] = "Pickup",
            ["Price"] = new JObject { ["OriginalValue"] = 5 },
        };
        data.OrderInfo = orderJson.ToString();
        await fixture.Repository.UpdateOrderAsync(data);
        var before = data.OrderInfo;
        fixture.ActivityLogs.Invocations.Clear();

        EventHandler<OrderUpdatingEventArgs> handler = (_, _) => throw new InvalidOperationException("Save aborted");
        OrderEvents.OrderUpdating += handler;
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.UpdateCustomerInformationAsync(
                new Dictionary<string, string> { ["storeAlias"] = "main", ["customerCountry"] = "DK" },
                new OrderSettings { OrderInfo = new OrderInfo(data) }));
        }
        finally
        {
            OrderEvents.OrderUpdating -= handler;
        }

        Assert.Equal(before, (await fixture.Repository.GetOrderAsync(order.UniqueId))!.OrderInfo);
        fixture.ActivityLogs.Verify(x => x.AddOrderLogAsync(order.UniqueId,
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<OrderActivityLogType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CustomerCountryUpdate_WithoutShippingProviderDoesNotLogRemoval()
    {
        using var fixture = new Fixture();
        var order = await AddTwoLinesAsync(fixture);
        fixture.ActivityLogs.Invocations.Clear();

        await fixture.Service.UpdateCustomerInformationAsync(
            new Dictionary<string, string> { ["storeAlias"] = "main", ["customerCountry"] = "DK" },
            new OrderSettings { OrderInfo = await fixture.ReloadAsync(), FireEvents = false });

        fixture.ActivityLogs.Verify(x => x.AddOrderLogAsync(order.UniqueId,
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<OrderActivityLogType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateShippingInformationAsync_AddsAndReplacesProviderOnCompletedOrder()
    {
        using var fixture = new Fixture();
        var order = await AddTwoLinesAsync(fixture);
        var data = (await fixture.Repository.GetOrderAsync(order.UniqueId))!;
        data.OrderStatus = OrderStatus.Closed;
        await fixture.Repository.UpdateOrderAsync(data);
        var firstKey = fixture.AddShippingProvider(5);
        var secondKey = fixture.AddShippingProvider(7);
        var originalAmount = (await fixture.ReloadAsync()).ChargedAmount.Value;

        var added = await fixture.Service.UpdateShippingInformationAsync(firstKey, "main", null,
            new OrderSettings { OrderInfo = await fixture.ReloadAsync(), FireEvents = false });
        Assert.Equal(firstKey, added.ShippingProvider?.Key);
        Assert.Equal(originalAmount + 5, added.ChargedAmount.Value);

        var replaced = await fixture.Service.UpdateShippingInformationAsync(secondKey, "main", null,
            new OrderSettings { OrderInfo = await fixture.ReloadAsync(), FireEvents = false });
        Assert.Equal(secondKey, replaced.ShippingProvider?.Key);
        Assert.Equal(originalAmount + 7, replaced.ChargedAmount.Value);
        Assert.Equal(secondKey, (await fixture.ReloadAsync()).ShippingProvider?.Key);
        Assert.Equal(replaced.ChargedAmount.Value, (await fixture.Repository.GetOrderAsync(order.UniqueId))!.TotalAmount);
    }

    [Fact]
    public async Task UpdateShippingInformationAsync_AddsValidProviderOnIncompleteOrder()
    {
        using var fixture = new Fixture();
        var order = await AddTwoLinesAsync(fixture);
        var shippingKey = fixture.AddShippingProvider(5);
        var originalAmount = (await fixture.ReloadAsync()).ChargedAmount.Value;

        var updated = await fixture.Service.UpdateShippingInformationAsync(shippingKey, "main", null,
            new OrderSettings { OrderInfo = await fixture.ReloadAsync(), FireEvents = false });

        Assert.Equal(shippingKey, updated.ShippingProvider?.Key);
        Assert.Equal(originalAmount + 5, (await fixture.ReloadAsync()).ChargedAmount.Value);
    }

    [Fact]
    public async Task UpdateOrderLineMetadataAsync_FiresOnlyOrderUpdatedOnce()
    {
        using var fixture = new Fixture();
        var order = await AddTwoLinesAsync(fixture);
        var orderUpdated = 0;
        var lineUpdated = 0;
        EventHandler<OrderUpdatedEventArgs> orderHandler = (_, _) => orderUpdated++;
        EventHandler<UpdatedOrderlineEventArgs> lineHandler = (_, _) => lineUpdated++;
        OrderEvents.OrderUpdated += orderHandler;
        OrderEvents.UpdatedOrderline += lineHandler;
        try
        {
            await fixture.Service.UpdateOrderLineMetadataAsync(order.UniqueId,
                [new OrderLineMetadataUpdate { LineId = order.OrderLines.First().Key, Properties = new Dictionary<string, string> { ["orderlineWarehouse"] = "North" } }]);
        }
        finally
        {
            OrderEvents.OrderUpdated -= orderHandler;
            OrderEvents.UpdatedOrderline -= lineHandler;
        }

        Assert.Equal(1, orderUpdated);
        Assert.Equal(0, lineUpdated);
    }

    [Fact]
    public async Task UpdateOrderLineMetadataAsync_RejectsDuplicateLinesAndInvalidPropertyKeys()
    {
        using var fixture = new Fixture();
        var order = await AddTwoLinesAsync(fixture);
        var lineId = order.OrderLines.First().Key;
        var before = (await fixture.Repository.GetOrderAsync(order.UniqueId))!.OrderInfo;

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.UpdateOrderLineMetadataAsync(order.UniqueId,
        [
            new OrderLineMetadataUpdate { LineId = lineId, Properties = new Dictionary<string, string> { ["orderlineWarehouse"] = "North" } },
            new OrderLineMetadataUpdate { LineId = lineId, Properties = new Dictionary<string, string> { ["orderlineBatch"] = "A123" } },
        ]));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.UpdateOrderLineMetadataAsync(order.UniqueId,
            [new OrderLineMetadataUpdate { LineId = lineId, Properties = new Dictionary<string, string> { ["warehouse"] = "North" } }]));

        Assert.Equal(before, (await fixture.Repository.GetOrderAsync(order.UniqueId))!.OrderInfo);
    }

    [Fact]
    public async Task TryUpdateOrderInfoAsync_RejectsAConcurrentChange()
    {
        using var fixture = new Fixture();
        var order = await AddTwoLinesAsync(fixture);
        var data = (await fixture.Repository.GetOrderAsync(order.UniqueId))!;
        var original = data.OrderInfo;
        var changed = Newtonsoft.Json.Linq.JObject.Parse(original);
        changed["WarehouseSync"] = "other writer";
        data.OrderInfo = changed.ToString();
        await fixture.Repository.UpdateOrderAsync(data);

        Assert.False(await fixture.Repository.TryUpdateOrderInfoAsync(order.UniqueId, original, "stale write", DateTime.Now));
        Assert.Equal(data.OrderInfo, (await fixture.Repository.GetOrderAsync(order.UniqueId))!.OrderInfo);
    }

    private static async Task<OrderInfo> AddTwoLinesAsync(Fixture fixture)
    {
        var parent = fixture.AddProduct();
        var child = fixture.AddProduct();
        return await fixture.Service.AddLinkedOrderLinesAsync(parent, 1, "main",
            [new LinkedOrderLineRequest { ProductId = child, Quantity = 2 }],
            new AddOrderSettings
            {
                OrderInfo = fixture.Order,
                FireEvents = false,
                CustomData = new Dictionary<string, string>
                {
                    ["orderlineNote"] = "Existing",
                    ["orderlineSource"] = "Existing",
                },
            });
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ConfigurationScope _scope;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        private readonly Mock<IPerStoreIndexedCache<IProduct>> _products = new();
        private readonly StockReservationTests.ReservationDatabase _database = new();
        private readonly ConcurrentDictionary<Guid, IShippingProvider> _shippingProviders = new();
        public Mock<IOrderActivityLogService> ActivityLogs { get; } = new();

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
                services.AddSingleton(sp => new Providers(
                    sp.GetRequiredService<Configuration>(),
                    NullLogger<Providers>.Instance,
                    Mock.Of<IPerStoreCache<IShippingProvider>>(x => x["main"] == _shippingProviders),
                    Mock.Of<IPerStoreCache<IPaymentProvider>>(x => x["main"] == new ConcurrentDictionary<Guid, IPaymentProvider>()),
                    Mock.Of<IBaseCache<IZone>>(),
                    stores.Object,
                    null!));
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
                ActivityLogs.Object,
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

        public Guid AddShippingProvider(decimal amount)
        {
            var key = Guid.NewGuid();
            var provider = new Mock<IShippingProvider>();
            provider.SetupGet(x => x.Id).Returns(1);
            provider.SetupGet(x => x.Key).Returns(key);
            provider.SetupGet(x => x.Title).Returns("Delivery");
            provider.SetupGet(x => x.Properties).Returns(new Dictionary<string, string> { ["title"] = "Delivery" });
            provider.SetupGet(x => x.Prices).Returns([new Price(amount, Order.StoreInfo.Currency, 0, false)]);
            var constraints = new Mock<IConstraints>();
            constraints.Setup(x => x.IsValid(It.IsAny<string>(), It.IsAny<decimal>())).Returns(true);
            provider.SetupGet(x => x.Constraints).Returns(constraints.Object);
            _shippingProviders[key] = provider.Object;
            return key;
        }

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

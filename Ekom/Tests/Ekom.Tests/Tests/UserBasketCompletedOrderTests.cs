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
using Newtonsoft.Json;
using Xunit;
using static Ekom.Tests.Tests.StockReservationTests;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class UserBasketCompletedOrderTests
{
    [Fact]
    public async Task MemberGetsTheirNewestCompletedOrderAfterCompletionClearedTheReference()
    {
        using var f = new Fixture();
        f.Insert("anna", "main", OrderStatus.Dispatched, new DateTime(2026, 1, 5));
        var completed = f.Insert("anna", "main", OrderStatus.ReadyForDispatch, new DateTime(2026, 3, 1));
        f.Insert("anna", "main", OrderStatus.Incomplete, new DateTime(2026, 3, 2));
        f.Insert("anna", "wholesale", OrderStatus.ReadyForDispatch, new DateTime(2026, 4, 1));
        f.Insert("bjorn", "main", OrderStatus.ReadyForDispatch, new DateTime(2026, 5, 1));

        var order = await f.Service("anna", Guid.Empty).GetCompletedOrderAsync("main");

        Assert.Equal(completed, order?.UniqueId);
    }

    [Fact]
    public async Task MemberWithANewBasketHasNoCompletedOrder()
    {
        using var f = new Fixture();
        f.Insert("anna", "main", OrderStatus.ReadyForDispatch, new DateTime(2026, 3, 1));
        var basket = f.Insert("anna", "main", OrderStatus.Incomplete, new DateTime(2026, 3, 2));

        Assert.Null(await f.Service("anna", basket).GetCompletedOrderAsync("main"));
    }

    [Fact]
    public async Task MemberWithoutCompletedOrdersGetsNothing()
    {
        using var f = new Fixture();
        f.Insert("bjorn", "main", OrderStatus.ReadyForDispatch, new DateTime(2026, 5, 1));

        Assert.Null(await f.Service("anna", Guid.Empty).GetCompletedOrderAsync("main"));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ConfigurationScope _scope;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        private readonly ReservationDatabase _database = new();
        private readonly Mock<IStoreService> _stores = new();
        private readonly OrderRepository _repository;

        public Fixture()
        {
            var store = new Mock<IStore>();
            store.SetupGet(x => x.Alias).Returns("main");
            store.SetupGet(x => x.UserBasket).Returns(true);
            _stores.Setup(x => x.GetStoreByAlias("main")).Returns(store.Object);
            _scope = new ConfigurationScope(addServices: services =>
                services.AddSingleton(new Ekom.API.Store(_stores.Object, Mock.Of<ICacheRefreshService>())));
            _repository = new OrderRepository(NullLogger<OrderRepository>.Instance, _scope.Instance, _database.Factory, _cache);
            using var db = _database.Factory.GetDatabase();
            db.CreateTable<OrderData>();
        }

        public Guid Insert(string username, string storeAlias, OrderStatus status, DateTime created)
        {
            var storeInfo = new StoreInfo(Guid.NewGuid(), new CurrencyModel { CurrencyValue = "en-US" }, [], "en-US", storeAlias, false, 0, false);
            var data = new OrderData
            {
                UniqueId = Guid.NewGuid(),
                CustomerUsername = username,
                StoreAlias = storeAlias,
                OrderStatusCol = status.ToString(),
                CreateDate = created,
                OrderInfo = JsonConvert.SerializeObject(new { StoreInfo = storeInfo, OrderLines = Array.Empty<object>(), CustomerInformation = new CustomerInfo() }),
            };
            using var db = _database.Factory.GetDatabase();
            db.Insert(data);
            return data.UniqueId;
        }

        public OrderService Service(string username, Guid memberOrderId)
        {
            var context = new DefaultHttpContext();
            context.Items[Configuration.EkmRequestKey] = new Lazy<object>(() => new ContentRequest
            {
                User = new User { Username = username, OrderId = memberOrderId },
            });
            return new OrderService(_scope.Instance, _repository, null!, Mock.Of<IOrderActivityLogService>(),
                NullLogger<OrderService>.Instance, _stores.Object, _cache, Mock.Of<IMemberService>(), null!,
                Mock.Of<IOrderTrackingService>(), Mock.Of<IHttpContextAccessor>(x => x.HttpContext == context));
        }

        public void Dispose()
        {
            _cache.Dispose();
            _scope.Dispose();
            _database.Dispose();
        }
    }
}

using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using Ekom.Tests.Objects;
using Ekom.Tracking;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class OrderServiceHttpContextTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrderReads_WithoutHttpContextReturnNoCurrentOrder(bool useAccessor)
    {
        using var fixture = new Fixture();
        var service = fixture.CreateService(useAccessor);

        Assert.Null(await service.GetOrderAsync(fixture.Store.Object));
        Assert.Null(await service.GetCompletedOrderAsync("main"));
        Assert.Empty(await service.GetStatusOrdersByCustomerIdAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrderReads_WithoutEkomRequestReturnNoCurrentOrder(bool userBasket)
    {
        using var fixture = new Fixture(userBasket);
        var service = fixture.CreateService(true, new DefaultHttpContext());

        Assert.Null(await service.GetOrderAsync(fixture.Store.Object));
        Assert.Null(await service.GetCompletedOrderAsync("main"));
        Assert.Empty(await service.GetStatusOrdersByCustomerIdAsync());
    }

    [Fact]
    public async Task GetCompletedOrderAsync_UnknownStoreThrowsClearException()
    {
        using var fixture = new Fixture();
        var service = fixture.CreateService(false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetCompletedOrderAsync("missing"));

        Assert.Contains("missing", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClearCustomerOrderReference_WithoutUsernameOrContextRemovesCachedOrder(bool useAccessor)
    {
        using var fixture = new Fixture();
        var service = fixture.CreateService(useAccessor);
        var order = new OrderData
        {
            UniqueId = Guid.NewGuid(),
            StoreAlias = "main",
            OrderStatus = OrderStatus.ReadyForDispatch,
        };
        fixture.Cache.Set(order.UniqueId.ToString(), order);

        service.ClearCustomerOrderReference(order);

        Assert.False(fixture.Cache.TryGetValue(order.UniqueId.ToString(), out _));
        Assert.NotNull(order.PaidDate);
        fixture.Members.Verify(x => x.Save(It.IsAny<Dictionary<string, object>>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void ClearCustomerOrderReference_WithoutContextUsesOrderUsername()
    {
        using var fixture = new Fixture();
        var service = fixture.CreateService(false);
        var order = new OrderData
        {
            StoreAlias = "main",
            OrderStatus = OrderStatus.ReadyForDispatch,
            CustomerUsername = "order-customer",
        };

        service.ClearCustomerOrderReference(order);

        fixture.Members.Verify(x => x.Save(
            It.Is<Dictionary<string, object>>(data => (string)data["orderId"] == ""),
            "order-customer"), Times.Once);
    }

    [Theory]
    [InlineData("", "request-customer")]
    [InlineData("order-customer", "order-customer")]
    public void ClearCustomerOrderReference_PrefersOrderUsernameOverRequestIdentity(string orderUsername, string expectedUsername)
    {
        using var fixture = new Fixture();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "request-customer")], "test")),
        };
        var service = fixture.CreateService(true, context);
        var order = new OrderData
        {
            StoreAlias = "main",
            OrderStatus = OrderStatus.ReadyForDispatch,
            CustomerUsername = orderUsername,
        };

        service.ClearCustomerOrderReference(order);

        fixture.Members.Verify(x => x.Save(It.IsAny<Dictionary<string, object>>(), expectedUsername), Times.Once);
    }

    [Fact]
    public void ClearCustomerOrderReference_WithoutIdentityRemovesCachedOrder()
    {
        using var fixture = new Fixture();
        var service = fixture.CreateService(true, new DefaultHttpContext { User = new ClaimsPrincipal() });
        var order = new OrderData
        {
            UniqueId = Guid.NewGuid(),
            StoreAlias = "main",
            OrderStatus = OrderStatus.ReadyForDispatch,
        };
        fixture.Cache.Set(order.UniqueId.ToString(), order);

        service.ClearCustomerOrderReference(order);

        Assert.False(fixture.Cache.TryGetValue(order.UniqueId.ToString(), out _));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ConfigurationScope _scope;
        private readonly Mock<IStoreService> _stores = new();

        public Fixture(bool userBasket = true)
        {
            Store.SetupGet(x => x.Alias).Returns("main");
            Store.SetupGet(x => x.UserBasket).Returns(userBasket);
            _stores.Setup(x => x.GetStoreByAlias("main")).Returns(Store.Object);
            _scope = new ConfigurationScope(addServices: services =>
                services.AddSingleton(new Ekom.API.Store(_stores.Object, Mock.Of<ICacheRefreshService>())));
        }

        public Mock<IStore> Store { get; } = new();
        public Mock<IMemberService> Members { get; } = new();
        public MemoryCache Cache { get; } = new(new MemoryCacheOptions());

        public OrderService CreateService(bool useAccessor, HttpContext? context = null)
        {
            if (useAccessor)
            {
                return new OrderService(_scope.Instance, null!, null!, Mock.Of<IOrderActivityLogService>(),
                    NullLogger<OrderService>.Instance, _stores.Object, Cache, Members.Object, null!,
                    Mock.Of<IOrderTrackingService>(), Mock.Of<IHttpContextAccessor>(x => x.HttpContext == context));
            }

            return new OrderService(_scope.Instance, null!, null!, Mock.Of<IOrderActivityLogService>(),
                NullLogger<OrderService>.Instance, _stores.Object, Cache, Members.Object, null!,
                Mock.Of<IOrderTrackingService>());
        }

        public void Dispose()
        {
            Cache.Dispose();
            _scope.Dispose();
        }
    }
}

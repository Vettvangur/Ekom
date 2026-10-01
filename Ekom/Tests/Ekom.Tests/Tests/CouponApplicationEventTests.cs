using Ekom.API;
using Ekom.Cache;
using Ekom.Controllers;
using Ekom.Events;
using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using Xunit;

namespace Ekom.Tests.Tests;

public class CouponApplicationEventTests
{
    private const string RejectionReason = "This coupon is only available to wholesale customers.";

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("UnknownCODE", "missing-store")]
    public async Task RejectionRunsBeforeInputChecksAndLookup(string? coupon, string? storeAlias)
    {
        var events = new DiscountEvents();
        var coupons = new Mock<ICouponCache>(MockBehavior.Strict);
        var stores = new Mock<IStoreService>(MockBehavior.Strict);
        var order = CreateOrder(events, coupons.Object, stores.Object);
        using var cancellation = new CancellationTokenSource();
        events.BeforeApplyCouponDiscountAsync += async (sender, args) =>
        {
            await Task.Yield();
            Assert.Same(order, sender);
            Assert.Equal(coupon, args.CouponCode);
            Assert.Equal(storeAlias, args.StoreAlias);
            Assert.Equal(cancellation.Token, args.CancellationToken);
            args.Reject(RejectionReason);
        };

        var exception = await Assert.ThrowsAsync<CouponApplicationRejectedException>(() =>
            order.ApplyCouponToOrderAsync(coupon!, storeAlias!, cancellation.Token));

        Assert.Equal(RejectionReason, exception.Message);
        coupons.VerifyNoOtherCalls();
        stores.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CurrentStoreOverloadRejectsBeforeResolvingStore()
    {
        var events = new DiscountEvents();
        var stores = new Mock<IStoreService>(MockBehavior.Strict);
        var order = CreateOrder(events, storeService: stores.Object);
        var calls = 0;
        events.BeforeApplyCouponDiscountAsync += (sender, args) =>
        {
            calls++;
            Assert.Null(args.StoreAlias);
            args.Reject(RejectionReason);
            return Task.CompletedTask;
        };

        var exception = await Assert.ThrowsAsync<CouponApplicationRejectedException>(() =>
            order.ApplyCouponToOrderAsync("RawCODE"));

        Assert.Equal(RejectionReason, exception.Message);
        Assert.Equal(1, calls);
        stores.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AllowedApplicationRaisesOnceThenContinuesExistingLookup(bool currentStore)
    {
        var events = new DiscountEvents();
        var coupons = new Mock<ICouponCache>();
        coupons.SetupGet(x => x.Cache).Returns(new ConcurrentDictionary<string, CouponData>());
        var stores = new Mock<IStoreService>();
        stores.Setup(x => x.GetStoreFromCache()).Returns(Mock.Of<IStore>(x => x.Alias == "main"));
        var order = CreateOrder(events, coupons.Object, stores.Object);
        var calls = 0;
        events.BeforeApplyCouponDiscountAsync += (sender, args) =>
        {
            calls++;
            Assert.Equal("RawCODE", args.CouponCode);
            Assert.Equal(currentStore ? null : "main", args.StoreAlias);
            coupons.VerifyGet(x => x.Cache, Times.Never);
            stores.Verify(x => x.GetStoreFromCache(), Times.Never);
            return Task.CompletedTask;
        };

        var exception = await Assert.ThrowsAsync<DiscountNotFoundException>(() => currentStore
            ? order.ApplyCouponToOrderAsync("RawCODE")
            : order.ApplyCouponToOrderAsync("RawCODE", "main"));

        Assert.Contains("rawcode", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, calls);
        coupons.VerifyGet(x => x.Cache, Times.Once);
    }

    [Fact]
    public async Task NoSubscribersPreservesCouponUsageValidation()
    {
        var coupons = new Mock<ICouponCache>();
        coupons.SetupGet(x => x.Cache).Returns(new ConcurrentDictionary<string, CouponData>
        {
            ["used"] = new CouponData { NumberAvailable = 0 },
        });
        var order = CreateOrder(new DiscountEvents(), coupons.Object);

        await Assert.ThrowsAsync<DiscountHasNoUsageException>(() => order.ApplyCouponToOrderAsync("USED", "main"));
    }

    [Fact]
    public async Task HandlersRunSequentiallyAndStopAtFirstRejection()
    {
        var events = new DiscountEvents();
        var calls = new List<int>();
        events.BeforeApplyCouponDiscountAsync += async (sender, args) =>
        {
            await Task.Yield();
            calls.Add(1);
        };
        events.BeforeApplyCouponDiscountAsync += (sender, args) =>
        {
            calls.Add(2);
            args.Reject(RejectionReason);
            args.Reject("A different reason must not replace the first rejection.");
            return Task.CompletedTask;
        };
        events.BeforeApplyCouponDiscountAsync += (sender, args) =>
        {
            calls.Add(3);
            return Task.CompletedTask;
        };

        var exception = await Assert.ThrowsAsync<CouponApplicationRejectedException>(() =>
            CreateOrder(events).ApplyCouponToOrderAsync("code", "main"));

        Assert.Equal(new[] { 1, 2 }, calls);
        Assert.Equal(RejectionReason, exception.Message);
    }

    [Fact]
    public async Task CancellationInsideHandlerStopsBeforeLookup()
    {
        var events = new DiscountEvents();
        using var cancellation = new CancellationTokenSource();
        var coupons = new Mock<ICouponCache>(MockBehavior.Strict);
        var laterHandlerCalled = false;
        events.BeforeApplyCouponDiscountAsync += (sender, args) =>
        {
            cancellation.Cancel();
            return Task.CompletedTask;
        };
        events.BeforeApplyCouponDiscountAsync += (sender, args) =>
        {
            laterHandlerCalled = true;
            return Task.CompletedTask;
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateOrder(events, coupons.Object).ApplyCouponToOrderAsync("code", "main", cancellation.Token));

        Assert.False(laterHandlerCalled);
        coupons.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandlerExceptionsPropagateWithoutCouponLookup()
    {
        var events = new DiscountEvents();
        var expected = new InvalidOperationException("Handler failure");
        events.BeforeApplyCouponDiscountAsync += (sender, args) => throw expected;

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateOrder(events).ApplyCouponToOrderAsync("code", "main"));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task HttpEmptyCouponCanBeRejectedBeforeControllerValidation()
    {
        var events = new DiscountEvents();
        events.BeforeApplyCouponDiscountAsync += (sender, args) =>
        {
            args.Reject(RejectionReason);
            return Task.CompletedTask;
        };
        var controller = new EkomOrderController(NullLogger<EkomOrderController>.Instance, CreateOrder(events));

        var exception = await Assert.ThrowsAsync<CouponApplicationRejectedException>(() =>
            controller.ApplyCouponToOrder(new CouponRequest { coupon = "", storeAlias = "main" }));
        var response = Assert.IsType<BadRequestObjectResult>(ExceptionHandler.Handle(exception));
        var body = JObject.FromObject(response.Value!);

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("couponApplicationRejected", body["code"]?.Value<string>());
        Assert.Equal(RejectionReason, body["message"]?.Value<string>());
    }

    [Fact]
    public async Task HttpEmptyCouponWithoutRejectionKeepsExistingResponse()
    {
        var controller = new EkomOrderController(NullLogger<EkomOrderController>.Instance, CreateOrder(new DiscountEvents()));

        var result = Assert.IsType<BadRequestObjectResult>(await controller.ApplyCouponToOrder(
            new CouponRequest { coupon = "", storeAlias = "main" }));

        Assert.Equal("Coupon code can not be empty", result.Value);
    }

    [Fact]
    public async Task LineLevelCouponDoesNotRaiseWholeOrderEvent()
    {
        var events = new DiscountEvents();
        var called = false;
        events.BeforeApplyCouponDiscountAsync += (sender, args) =>
        {
            called = true;
            return Task.CompletedTask;
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateOrder(events).ApplyCouponToOrderLineAsync(Guid.NewGuid(), "", "main"));

        Assert.False(called);
    }

    private static Order CreateOrder(DiscountEvents events, ICouponCache? couponCache = null, IStoreService? storeService = null)
        => new(null!, NullLogger<Order>.Instance, null!, couponCache!, null!, null!, storeService!,
            null!, null!, null!, events);
}

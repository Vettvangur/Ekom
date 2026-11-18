using Ekom.API;
using Ekom.Cache;
using Ekom.Events;
using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using Ekom.Tests.Objects;
using Ekom.Tracking;
using LinqToDB;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using Xunit;
using ReservationDatabase = Ekom.Tests.Tests.StockReservationTests.ReservationDatabase;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class LegacyCheckoutReservationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LegacyWrapperRejectsQuantityThatWouldConsumeStockBuffer(bool perStore, bool variant)
    {
        using var f = new Fixture(false, perStore);
        var key = await f.AddLineAsync(8, variant: variant, stockBuffer: 5);
        var controller = f.Controller();
        await Assert.ThrowsAsync<NotEnoughStockException>(() => controller.PayAsync(new PaymentRequest(), "en-US", f.Order));
        Assert.False(controller.PaymentCalled);
        Assert.Equal(10, await f.StockAsync(key));
        Assert.Empty(f.Order.ReservationIds);
        Assert.Empty((await f.ReloadAsync()).ReservationIds);
        using var db = f.Database.Factory.GetDatabase();
        Assert.Empty(await db.StockReservations.ToListAsync());
        Assert.Null((await db.GetTable<CheckoutPreparationData>().SingleAsync()).Owner);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public async Task LegacyImmediateSavePreparesRetriesAndCompletes(bool enabled, bool perStore, bool variant)
    {
        using var f = new Fixture(enabled, perStore);
        var key = await f.AddLineAsync(3, variant: variant, backorder: true);
        var controller = f.Controller();
        Assert.Equal(230, (await controller.PayAsync(new PaymentRequest(), "en-US", f.Order)).HttpStatusCode);
        var id = Assert.Single(f.Order.ReservationIds);
        var saved = await f.ReloadAsync();
        Assert.Equal("false", saved.CustomerInformation.Customer.Properties["isDistributor"]);
        Assert.Equal(id, Assert.Single(saved.ReservationIds));
        Assert.Equal(7, await f.StockAsync(key));
        await controller.PayAsync(new PaymentRequest(), "en-US", saved);
        Assert.Equal(id, Assert.Single(saved.ReservationIds));
        var completedOrder = await f.ReloadAsync();
        var requirements = await f.Checkout.GetRequirementsAsync(completedOrder, default);
        await f.CompleteAsync();
        Assert.True(await f.Checkout.IsCompletedAsync(saved.UniqueId, default));
        Assert.False(await f.Checkout.CompleteStockAsync(saved.UniqueId, requirements, completedOrder.ReservationIds, true, default));
        Assert.Equal(7, await f.StockAsync(key));
        using var db = f.Database.Factory.GetDatabase();
        var row = await db.StockReservations.SingleAsync();
        Assert.Equal(StockReservationState.Consumed, row.State);
        Assert.Equal(saved.UniqueId.ToString(), row.OrderId);
        Assert.Equal("main", row.StoreAlias);
        Assert.Equal(perStore ? $"main_{key}" : key.ToString(), row.StockUniqueId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LaterLegacyLineFailureRemovesPersistedIdsRestoresStockAndAllowsRetry(bool perStore)
    {
        using var f = new Fixture(false, perStore);
        var first = await f.AddLineAsync(3);
        var second = await f.AddLineAsync(11, variant: true);
        var controller = f.Controller();
        Assert.Equal(530, (await controller.PayAsync(new PaymentRequest(), "en-US", f.Order)).HttpStatusCode);
        Assert.False(controller.PaymentCalled);
        Assert.Empty(f.Order.ReservationIds);
        Assert.Empty((await f.ReloadAsync()).ReservationIds);
        Assert.Equal(10, await f.StockAsync(first));
        Assert.Equal(10, await f.StockAsync(second));
        f.Order.orderLines[1].Quantity = 2;
        Assert.Equal(230, (await controller.PayAsync(new PaymentRequest(), "en-US", f.Order)).HttpStatusCode);
        Assert.Equal(2, f.Order.ReservationIds.Count());
        Assert.Equal(7, await f.StockAsync(first));
        Assert.Equal(8, await f.StockAsync(second));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WholesaleExemptionUsesDurableCustomerDataAtPreparationAndCompletion(bool wholesale)
    {
        using var f = new Fixture(false, false);
        var key = await f.AddLineAsync(3, backorder: true);
        var controller = f.Controller();
        controller.Wholesale = wholesale;
        await controller.PayAsync(new PaymentRequest(), "en-US", f.Order);
        var saved = await f.ReloadAsync();
        var requirements = await f.Checkout.GetRequirementsAsync(saved, default);
        Assert.Equal(wholesale ? 0 : 1, requirements.Count);
        await f.Checkout.CompleteStockAsync(saved.UniqueId, requirements, saved.ReservationIds, true, default);
        Assert.Equal(wholesale ? 10 : 7, await f.StockAsync(key));
    }

    [Fact]
    public async Task GenericUnownedIdCannotBeAttachedOrStolenAndRejectionUnlocksOrder()
    {
        using var f = new Fixture(false, false);
        var key = await f.AddLineAsync(3);
        var unowned = await f.Database.Service.ReserveAsync(new StockReservationRequest { Key = key, Quantity = 3 });
        await Assert.ThrowsAsync<StockException>(() => Order.Instance.AddReservationsToOrderAsync([unowned.ReservationId!], f.Order));
        Assert.Empty((await f.ReloadAsync()).ReservationIds);
        using var db = f.Database.Factory.GetDatabase();
        Assert.Null((await db.StockReservations.SingleAsync()).OrderId);
        Assert.Null((await db.GetTable<CheckoutPreparationData>().SingleAsync()).Owner);
        await f.Database.Service.ReleaseAsync(unowned.ReservationId!);
        await f.Controller().PayAsync(new PaymentRequest(), "en-US", f.Order);
        Assert.Single(f.Order.ReservationIds);
    }

    [Fact]
    public async Task LegacyImmediateSaveCannotBeAdoptedOrCompletedWhileFailingOwnerCompensates()
    {
        using var f = new Fixture(false, false);
        var key = await f.AddLineAsync(3);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = f.Controller();
        first.AfterLines = async () => { entered.SetResult(); await resume.Task; return new CheckoutResponse { HttpStatusCode = 400 }; };
        var payment = first.PayAsync(new PaymentRequest(), "en-US", f.Order);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var saved = await f.ReloadAsync();
            Assert.Single(saved.ReservationIds);
            var response = await f.Controller().PayAsync(new PaymentRequest(), "en-US", saved);
            AssertCheckoutStateConflict(response);
            var requirements = await f.Checkout.GetRequirementsAsync(saved, default);
            await Assert.ThrowsAsync<StockException>(() => f.Checkout.CompleteStockAsync(saved.UniqueId, requirements, saved.ReservationIds, true, default));
            await Assert.ThrowsAsync<StockException>(() => Order.Instance.AddReservationsToOrderAsync(saved.ReservationIds, saved));
        }
        finally { resume.SetResult(); }
        Assert.Equal(400, (await payment).HttpStatusCode);
        Assert.Equal(10, await f.StockAsync(key));
        Assert.Empty((await f.ReloadAsync()).ReservationIds);
    }

    [Fact]
    public async Task RetryFailurePreservesPreviouslySubmittedLegacyAssociations()
    {
        using var f = new Fixture(false, false);
        var key = await f.AddLineAsync(3);
        await f.Controller().PayAsync(new PaymentRequest(), "en-US", f.Order);
        var id = Assert.Single(f.Order.ReservationIds);
        var controller = f.Controller();
        controller.AfterLines = () => Task.FromResult<CheckoutResponse?>(new CheckoutResponse { HttpStatusCode = 400 });
        Assert.Equal(400, (await controller.PayAsync(new PaymentRequest(), "en-US", await f.ReloadAsync())).HttpStatusCode);
        Assert.Equal(id, Assert.Single((await f.ReloadAsync()).ReservationIds));
        Assert.Equal(7, await f.StockAsync(key));
    }

    private sealed class WholesalePolicy : ICheckoutStockPolicy
    {
        public bool RequiresStock(IOrderInfo order, IOrderLine line) => !(line.Product.Backorder &&
            order.CustomerInformation.Customer.Properties.TryGetValue("isDistributor", out var value) && value == "true");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectlySavedForeignIdsFromNoBaseOverrideAreRejectedWithoutReleasingThem(bool enabled)
    {
        using var f = new Fixture(enabled, false);
        var key = await f.AddLineAsync(3);
        var foreign = await f.Database.Service.ReserveAsync(new StockReservationRequest
        {
            Key = key, Quantity = 3, StoreAlias = "main", OrderId = Guid.NewGuid().ToString(),
        });
        var controller = f.Controller();
        controller.SkipReservations = true;
        controller.AfterLines = async () =>
        {
            f.Order._hangfireJobs.Add(foreign.ReservationId!);
            var data = (await f.Repository.GetOrderAsync(f.Order.UniqueId))!;
            data.OrderInfo = JsonConvert.SerializeObject(f.Order, EkomJsonDotNet.Settings);
            await f.Repository.UpdateOrderAsync(data, reservationPersistence: true, default);
            return null;
        };
        var response = await controller.PayAsync(new PaymentRequest(), "en-US", f.Order);
        AssertCheckoutStateConflict(response);
        Assert.False(controller.PaymentCalled);
        using var db = f.Database.Factory.GetDatabase();
        Assert.Equal(StockReservationState.Active, (await db.StockReservations.SingleAsync()).State);
        Assert.Equal(7, await f.StockAsync(key));
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("store")]
    [InlineData("quantity")]
    [InlineData("expired")]
    [InlineData("released")]
    [InlineData("consumed")]
    [InlineData("missing")]
    public async Task PublicAttachmentValidatesOwnershipIdentityQuantityAndState(string mismatch)
    {
        using var f = new Fixture(false, false);
        var key = await f.AddLineAsync(3);
        var hold = await f.Database.Service.ReserveAsync(new StockReservationRequest
        {
            Key = key, Quantity = mismatch == "quantity" ? 4 : 3,
            StoreAlias = mismatch == "store" ? "other" : "main",
            OrderId = mismatch == "owner" ? Guid.NewGuid().ToString() : f.Order.UniqueId.ToString(),
        });
        if (mismatch == "expired") await f.Database.MakeDueAsync(hold.ReservationId!);
        if (mismatch == "released") await f.Database.Service.ReleaseAsync(hold.ReservationId!);
        if (mismatch == "consumed") await f.Database.Service.ConsumeAsync(hold.ReservationId!);
        await Assert.ThrowsAsync<StockException>(() => Order.Instance.AddReservationsToOrderAsync(
            [mismatch == "missing" ? "missing" : hold.ReservationId!], f.Order));
        Assert.Empty((await f.ReloadAsync()).ReservationIds);
        using var db = f.Database.Factory.GetDatabase();
        Assert.Null((await db.GetTable<CheckoutPreparationData>().SingleAsync()).Owner);
    }

    [Fact]
    public async Task UnownedLegacyOriginCannotBeStolenByAnotherPreparation()
    {
        using var f = new Fixture(false, false);
        var key = await f.AddLineAsync(3);
        // Even a forged legacy marker is not the current scope's creation capability.
        var ownership = await f.Checkout.AcquirePreparationAsync(f.Order.UniqueId, default);
        using var scope = new CheckoutPreparationScope(f.Checkout, ownership, f.Order);
        var generic = await f.Database.Service.ReserveAsync(new StockReservationRequest
        {
            Key = key, Quantity = 3, StoreAlias = "main", PaymentAttemptId = "legacy-preparation:" + ownership.Owner,
        });
        await Assert.ThrowsAsync<StockException>(() => Order.Instance.AddHangfireJobsToOrderAsync([generic.ReservationId!], f.Order));
        using var db = f.Database.Factory.GetDatabase();
        Assert.Null((await db.StockReservations.SingleAsync()).OrderId);
        await f.Checkout.ReleasePreparationAsync(ownership, default);
    }

    [Fact]
    public async Task LegacyAssociationBatchRollsBackWhenAnyIdIsInvalid()
    {
        using var f = new Fixture(false, false);
        var key = await f.AddLineAsync(3);
        var ownership = await f.Checkout.AcquirePreparationAsync(f.Order.UniqueId, default);
        using var scope = new CheckoutPreparationScope(f.Checkout, ownership, f.Order);
        var id = await Stock.Instance.ReserveStockAsync(key, -3);
        await Assert.ThrowsAsync<StockException>(() => Order.Instance.AddHangfireJobsToOrderAsync([id, "missing"], f.Order));
        using var db = f.Database.Factory.GetDatabase();
        Assert.Null((await db.StockReservations.SingleAsync()).OrderId);
        Assert.Empty((await f.ReloadAsync()).ReservationIds);
        await f.Database.Service.ReleaseAsync(id);
        await f.Checkout.ReleasePreparationAsync(ownership, default);
    }

    [Fact]
    public async Task CompletionRecoversHoldsWhenInventoryValidationIsDisabled()
    {
        using var f = new Fixture(false, false);
        var key = await f.AddLineAsync(3);
        var requirements = await f.Checkout.GetRequirementsAsync(f.Order, default);
        var hold = await f.Database.Service.ReserveAsync(requirements.Single());
        await Order.Instance.AddReservationsToOrderAsync([], f.Order);
        Assert.Empty((await f.ReloadAsync()).ReservationIds);
        await f.CompleteAsync(validateInventory: false);
        Assert.Equal(7, await f.StockAsync(key));
        using var db = f.Database.Factory.GetDatabase();
        Assert.Equal(StockReservationState.Consumed, (await db.StockReservations.SingleAsync(x => x.Id == hold.ReservationId)).State);
    }

    [Fact]
    public async Task WholesaleCustomizationRequiresExplicitSharedPolicyRegistration()
    {
        using var f = new Fixture(false, false, customPolicy: false);
        var key = await f.AddLineAsync(3, backorder: true);
        var controller = f.Controller();
        var response = await controller.PayAsync(new PaymentRequest(), "en-US", f.Order);
        AssertCheckoutStateConflict(response);
        Assert.False(controller.PaymentCalled);
        Assert.Equal(10, await f.StockAsync(key));
    }

    [Fact]
    public async Task LegacyCollectionBasedDeferredAttachmentStillWorks()
    {
        using var f = new Fixture(false, false);
        var key = await f.AddLineAsync(3);
        var controller = f.Controller();
        controller.DeferAttachment = true;
        await controller.PayAsync(new PaymentRequest(), "en-US", f.Order);
        Assert.Single((await f.ReloadAsync()).ReservationIds);
        await f.CompleteAsync();
        Assert.Equal(7, await f.StockAsync(key));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task CompletionFailureRaisesReviewBeforeEmailAndPreservesOriginalError(bool stockFailure, bool reviewFails)
    {
        using var f = new Fixture(false, false);
        Exception failure = stockFailure ? new NotEnoughStockException("stock failure") : new InvalidOperationException("completion failure");
        string code = stockFailure ? "completion-stock-failed" : "completion-failed";
        if (reviewFails)
        {
            f.Reviews.Setup(x => x.RaiseAsync(f.Order.UniqueId, "checkout", code, It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None))
                .ThrowsAsync(new InvalidOperationException("review database unavailable"));
        }
        f.Mail.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), null, null))
            .Callback(() => f.Reviews.Verify(x => x.RaiseAsync(f.Order.UniqueId, "checkout", code,
                It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None), Times.Once))
            .Returns(Task.CompletedTask);
        // A handler that throws no longer fails completion, so the failure comes from completion itself.
        f.FailCompletionWith(failure);

        Assert.Same(failure, await Assert.ThrowsAnyAsync<Exception>(() => f.CompleteAsync()));

        f.Mail.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), null, null), Times.Once);
    }

    [Fact]
    public async Task ThrowingCompletionHandlerDoesNotStopCompletionOrOtherHandlers()
    {
        using var f = new Fixture(false, false);
        var laterHandlerRan = false;
        void Fail(object? sender, CompleteCheckoutEventArgs args) => throw new InvalidOperationException("ERP is down");
        Task FailAsync(object sender, CompleteCheckoutEventArgs args, CancellationToken ct) => throw new InvalidOperationException("export failed");
        void Later(object? sender, CompleteCheckoutEventArgs args) => laterHandlerRan = true;
        CheckoutEvents.CompleteCheckout += Fail;
        CheckoutEvents.CompleteCheckout += Later;
        CheckoutEvents.CompleteCheckoutAsync += FailAsync;
        try
        {
            await f.CompleteAsync();
        }
        finally
        {
            CheckoutEvents.CompleteCheckout -= Fail;
            CheckoutEvents.CompleteCheckout -= Later;
            CheckoutEvents.CompleteCheckoutAsync -= FailAsync;
        }

        Assert.True(laterHandlerRan);
        f.ActivityLog.Verify(x => x.AddOrderLogAsync(f.Order.UniqueId, It.IsAny<string>(), null, OrderActivityLogType.Success, It.IsAny<CancellationToken>()), Times.Once);
        // Staff are told, once, that an integration may have missed the order.
        f.Reviews.Verify(x => x.RaiseAsync(f.Order.UniqueId, "checkout", "completion-handler-failed",
            It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None), Times.Once);
        f.Mail.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CompleteCheckoutIsRaisedAfterTheOrderIsCompleted()
    {
        using var f = new Fixture(false, false);
        var order = new List<string>();
        f.ActivityLog.Setup(x => x.AddOrderLogAsync(f.Order.UniqueId, It.IsAny<string>(), null, OrderActivityLogType.Success, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("completed")).Returns(Task.CompletedTask);
        Task Before(object sender, CompletingCheckoutEventArgs args, CancellationToken ct) { order.Add("before"); return Task.CompletedTask; }
        void After(object? sender, CompleteCheckoutEventArgs args)
        {
            if (args.OrderInfo.UniqueId == f.Order.UniqueId && args.OrderData.UniqueId == f.Order.UniqueId) order.Add("after");
        }
        Task AfterAsync(object sender, CompleteCheckoutEventArgs args, CancellationToken ct) { order.Add("after async"); return Task.CompletedTask; }
        CheckoutEvents.CompletingCheckoutAsync += Before;
        CheckoutEvents.CompleteCheckout += After;
        CheckoutEvents.CompleteCheckoutAsync += AfterAsync;
        try
        {
            await f.CompleteAsync();
        }
        finally
        {
            CheckoutEvents.CompletingCheckoutAsync -= Before;
            CheckoutEvents.CompleteCheckout -= After;
            CheckoutEvents.CompleteCheckoutAsync -= AfterAsync;
        }

        Assert.Equal(new[] { "before", "completed", "after", "after async" }, order);
    }

    [Fact]
    public async Task CompleteCheckoutIsNotRaisedWhenCompletionFails()
    {
        using var f = new Fixture(false, false);
        var raised = false;
        void After(object? sender, CompleteCheckoutEventArgs args) => raised = true;
        Task AfterAsync(object sender, CompleteCheckoutEventArgs args, CancellationToken ct) { raised = true; return Task.CompletedTask; }
        f.FailCompletionWith(new InvalidOperationException("completion failure"));
        CheckoutEvents.CompleteCheckout += After;
        CheckoutEvents.CompleteCheckoutAsync += AfterAsync;
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => f.CompleteAsync());
        }
        finally
        {
            CheckoutEvents.CompleteCheckout -= After;
            CheckoutEvents.CompleteCheckoutAsync -= AfterAsync;
        }

        Assert.False(raised);
    }

    [Fact]
    public async Task ThrowingCompletingHandlerDoesNotStopCompletion()
    {
        using var f = new Fixture(false, false);
        var key = await f.AddLineAsync(3);
        var controller = f.Controller();
        await controller.PayAsync(new PaymentRequest(), "en-US", f.Order);
        Task Fail(object sender, CompletingCheckoutEventArgs args, CancellationToken ct) => throw new InvalidOperationException("rule failed");
        CheckoutEvents.CompletingCheckoutAsync += Fail;
        try
        {
            await f.CompleteAsync();
        }
        finally
        {
            CheckoutEvents.CompletingCheckoutAsync -= Fail;
        }

        Assert.Equal(7, await f.StockAsync(key));
        f.Reviews.Verify(x => x.RaiseAsync(f.Order.UniqueId, "checkout", "completion-handler-failed",
            It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task CompleteCheckoutReportsTheSettingsCompletionUsed()
    {
        using var f = new Fixture(false, false);
        bool? stockValidation = null, updateOrderStatus = null;
#pragma warning disable CS0618 // The obsolete members still report what completion did
        void After(object? sender, CompleteCheckoutEventArgs args)
        {
            stockValidation = args.StockValidation;
            updateOrderStatus = args.UpdateOrderStatus;
        }
#pragma warning restore CS0618
        CheckoutEvents.CompleteCheckout += After;
        try
        {
            // The fixture turns both off from a CompletingCheckout handler.
            await f.CompleteAsync(validateInventory: false);
        }
        finally
        {
            CheckoutEvents.CompleteCheckout -= After;
        }

        Assert.False(stockValidation);
        Assert.False(updateOrderStatus);
    }

    [Fact]
    public async Task FailingAlertEmailDoesNotReplaceTheCompletionError()
    {
        using var f = new Fixture(false, false);
        var failure = new InvalidOperationException("completion failure");
        f.FailCompletionWith(failure);
        f.Mail.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), null, null))
            .ThrowsAsync(new InvalidOperationException("Umbraco:CMS:Global:Smtp:Host is not configured"));

        Assert.Same(failure, await Assert.ThrowsAnyAsync<Exception>(() => f.CompleteAsync()));
    }

    [Fact]
    public async Task CancelledCompletionDoesNotRaiseReview()
    {
        using var f = new Fixture(false, false);
        void Cancel(object? sender, CompletingCheckoutEventArgs args) => throw new OperationCanceledException();
        CheckoutEvents.CompletingCheckout += Cancel;
        try
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() => f.CompleteAsync());
        }
        finally
        {
            CheckoutEvents.CompletingCheckout -= Cancel;
        }
        f.Reviews.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    public async Task CouponCompletionClaimsAvailableUsesOrRaisesReviewWithoutBlockingCheckout(bool reviewFails, int available)
    {
        using var f = new Fixture(false, false);
        f.DisableDiscountCacheRefresh();
        using (var db = f.Database.Factory.GetDatabase())
        {
            db.CreateTable<CouponData>();
            db.Insert(new CouponData { CouponCode = "EXHAUSTED", NumberAvailable = available, CouponKey = Guid.NewGuid(), DiscountId = Guid.NewGuid() });
        }
        if (reviewFails)
        {
            f.Reviews.Setup(x => x.RaiseAsync(f.Order.UniqueId, "checkout", "coupon-exhausted", It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None))
                .ThrowsAsync(new InvalidOperationException("review database unavailable"));
        }
        void ApplyCoupon(object? sender, CompletingCheckoutEventArgs args)
        {
            var order = (OrderInfo)args.OrderInfo;
            order.Coupon = "EXHAUSTED";
            order.Discount = new OrderedDiscount(Guid.NewGuid(), "Discount", true, 10, default, [], [], null, false, true);
        }
        CheckoutEvents.CompletingCheckout += ApplyCoupon;
        try
        {
            await f.CompleteAsync();
        }
        finally
        {
            CheckoutEvents.CompletingCheckout -= ApplyCoupon;
        }
        f.Reviews.Verify(x => x.RaiseAsync(f.Order.UniqueId, "checkout", "coupon-exhausted", It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None), available == 0 ? Times.Once() : Times.Never());
        using (var db = f.Database.Factory.GetDatabase())
        {
            Assert.Equal(Math.Max(0, available - 1), db.CouponData.Single().NumberAvailable);
        }
        f.ActivityLog.Verify(x => x.AddOrderLogAsync(f.Order.UniqueId, It.IsAny<string>(), null, OrderActivityLogType.Alert, It.IsAny<CancellationToken>()), available == 0 ? Times.Once() : Times.Never());
        f.ActivityLog.Verify(x => x.AddOrderLogAsync(f.Order.UniqueId, It.IsAny<string>(), null, OrderActivityLogType.Success, It.IsAny<CancellationToken>()), Times.Once);
        f.Mail.VerifyNoOtherCalls();
    }

    private static void AssertCheckoutStateConflict(CheckoutResponse response)
    {
        Assert.Equal(409, response.HttpStatusCode);
        var error = Assert.IsType<CheckoutStateError>(response.ResponseBody);
        Assert.Equal("checkout_state_conflict", error.Code);
        Assert.Equal("Unable to prepare checkout. Please refresh your basket and try again. If the problem continues, contact the store.", error.Message);
        Assert.False(error.CanRetry);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ConfigurationScope _scope;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        private readonly Mock<IPerStoreIndexedCache<IProduct>> _products = new();
        private readonly ConcurrentDictionary<Guid, IVariant> _variants = new();
        private readonly bool _perStore;
        public ReservationDatabase Database { get; }
        public OrderInfo Order { get; }
        public CheckoutReservationService Checkout { get; }
        public OrderRepository Repository { get; }
        public Mock<IOrderReviewService> Reviews { get; } = new();
        public Mock<IMailService> Mail { get; } = new();
        public Mock<IOrderActivityLogService> ActivityLog { get; } = new();

        public Fixture(bool enabled, bool perStore, bool customPolicy = true)
        {
            _perStore = perStore;
            Database = new ReservationDatabase(perStore);
            var store = new Mock<IStore>();
            store.SetupGet(x => x.Alias).Returns("main");
            store.SetupGet(x => x.Culture).Returns(new CultureInfoDto { Name = "en-US" });
            var stores = new Mock<IStoreService>();
            stores.Setup(x => x.GetStoreByAlias("main")).Returns(store.Object);
            stores.Setup(x => x.GetStoreFromCache()).Returns(store.Object);
            var variants = new Mock<IPerStoreIndexedCache<IVariant>>();
            variants.SetupGet(x => x.Cache).Returns(new ConcurrentDictionary<string, ConcurrentDictionary<Guid, IVariant>> { ["main"] = _variants });
            var discounts = new Mock<IPerStoreCache<IDiscount>>();
            discounts.SetupGet(x => x.Cache).Returns(new ConcurrentDictionary<string, ConcurrentDictionary<Guid, IDiscount>> { ["main"] = new() });
            _scope = new ConfigurationScope(overrides: new Dictionary<string, string?>
            {
                ["Ekom:Reservations:Enabled"] = enabled.ToString(), ["Ekom:PerStoreStock"] = perStore.ToString(),
            }, addServices: services =>
            {
                services.AddSingleton<ICheckoutStockPolicy>(customPolicy ? new WholesalePolicy() : new DefaultCheckoutStockPolicy());
                services.AddSingleton(sp => Database.NewCheckout(sp.GetRequiredService<Configuration>(), sp.GetRequiredService<ICheckoutStockPolicy>()));
                services.AddSingleton(Database.NewStockApi());
                services.AddSingleton(new API.Store(stores.Object, Mock.Of<ICacheRefreshService>()));
                services.AddSingleton(sp => new Discounts(sp.GetRequiredService<Configuration>(), NullLogger<Discounts>.Instance, discounts.Object, stores.Object));
                services.AddSingleton(sp => new Catalog(NullLogger<Catalog>.Instance, sp.GetRequiredService<Configuration>(),
                    sp.GetRequiredService<IServiceScopeFactory>(), _products.Object, Mock.Of<IPerStoreIndexedCache<ICategory>>(),
                    Mock.Of<IPerStoreCache<IProductDiscount>>(), variants.Object, Mock.Of<IPerStoreIndexedCache<IVariantGroup>>(),
                    stores.Object, new HttpContextAccessor(), Mock.Of<IProductFilterService>()));
                services.AddSingleton(sp => new OrderRepository(NullLogger<OrderRepository>.Instance, sp.GetRequiredService<Configuration>(), Database.Factory, _cache));
                services.AddSingleton(sp => new OrderService(sp.GetRequiredService<Configuration>(), sp.GetRequiredService<OrderRepository>(),
                    null!, Mock.Of<IOrderActivityLogService>(), NullLogger<OrderService>.Instance, stores.Object, _cache,
                    Mock.Of<IMemberService>(), null!, Mock.Of<IOrderTrackingService>()));
                services.AddSingleton(sp => new Order(sp.GetRequiredService<Configuration>(), NullLogger<Order>.Instance, null!, null!,
                    sp.GetRequiredService<OrderService>(), null!, stores.Object, sp.GetRequiredService<OrderRepository>(), null!, Mock.Of<IOrderActivityLogService>(), new Ekom.Events.DiscountEvents()));
            });
            Checkout = Configuration.Resolver.GetRequiredService<CheckoutReservationService>();
            Repository = Configuration.Resolver.GetRequiredService<OrderRepository>();
            using var db = Database.Factory.GetDatabase();
            db.CreateTable<OrderData>();
            var storeInfo = new StoreInfo(Guid.NewGuid(), new CurrencyModel { CurrencyValue = "en-US" }, [], "en-US", "main", false, 0, false);
            var data = new OrderData
            {
                UniqueId = Guid.NewGuid(), OrderStatusCol = "Incomplete",
                OrderInfo = JsonConvert.SerializeObject(new { StoreInfo = storeInfo, OrderLines = Array.Empty<object>(), CustomerInformation = new CustomerInfo() }),
            };
            db.Insert(data);
            Order = new OrderInfo(data);
        }

        public async Task<Guid> AddLineAsync(decimal quantity, bool variant = false, bool backorder = false, int stockBuffer = 0)
        {
            var product = new Mock<IProduct>();
            var key = Guid.NewGuid();
            product.SetupGet(x => x.Key).Returns(key);
            product.SetupGet(x => x.Stock).Returns(10);
            product.SetupGet(x => x.StockBuffer).Returns(variant ? 0 : stockBuffer);
            product.SetupGet(x => x.Properties).Returns(new Dictionary<string, string> { ["__Key"] = key.ToString(), ["enableBackorder"] = backorder.ToString() });
            product.SetupGet(x => x.Prices).Returns(new List<IPrice> { new Price(10, Order.StoreInfo.Currency, 0, false) });
            var cached = product.Object;
            _products.Setup(x => x.TryGetByKey("main", key, out cached)).Returns(true);
            IVariant? selected = null;
            if (variant)
            {
                var v = new Mock<IVariant>();
                key = Guid.NewGuid();
                v.SetupGet(x => x.Key).Returns(key);
                v.SetupGet(x => x.Stock).Returns(10);
                v.SetupGet(x => x.StockBuffer).Returns(stockBuffer);
                v.SetupGet(x => x.Prices).Returns(product.Object.Prices);
                v.SetupGet(x => x.Properties).Returns(new Dictionary<string, string> { ["__Key"] = key.ToString(), ["id"] = "123" });
                var group = new Mock<IVariantGroup>();
                group.SetupGet(x => x.Properties).Returns(new Dictionary<string, string> { ["id"] = "456", ["__Key"] = Guid.NewGuid().ToString() });
                v.SetupGet(x => x.VariantGroup).Returns(group.Object);
                selected = v.Object;
                _variants[key] = selected;
            }
            Order.orderLines.Add(new OrderLine(product.Object, quantity, Guid.NewGuid(), Order, [], selected));
            await Database.SeedAsync(10, _perStore ? "main" : null, key);
            return key;
        }

        // This fixture has no content caches to rebuild after a coupon update.
        public void DisableDiscountCacheRefresh() => _scope.Instance.CacheList.Value.Clear();

        public Task<decimal> StockAsync(Guid key) => Database.StockAsync(key, _perStore ? "main" : null);
        /// <summary>Makes completion itself fail, at its last step.</summary>
        public void FailCompletionWith(Exception failure)
            => ActivityLog.Setup(x => x.AddOrderLogAsync(Order.UniqueId, It.IsAny<string>(), null, OrderActivityLogType.Success, It.IsAny<CancellationToken>()))
                .ThrowsAsync(failure);

        public async Task CompleteAsync(bool validateInventory = true)
        {
            var service = new CheckoutService(NullLogger<CheckoutService>.Instance, _scope.Instance, Repository,
                new CouponRepository(_scope.Instance, NullLogger<CouponRepository>.Instance, Mock.Of<ICouponCache>(), Database.Factory, Mock.Of<INodeService>()),
                Configuration.Resolver.GetRequiredService<OrderService>(), null!, ActivityLog.Object,
                Mail.Object, Database.Service, Checkout, Reviews.Object);
            void KeepStatus(object? sender, CompletingCheckoutEventArgs args)
            {
                args.UpdateOrderStatus = false;
                args.StockValidation = validateInventory;
            }
            CheckoutEvents.CompletingCheckout += KeepStatus;
            try { await service.CompleteAsync(Order.UniqueId); }
            finally { CheckoutEvents.CompletingCheckout -= KeepStatus; }
        }
        public async Task<OrderInfo> ReloadAsync() => new((await Repository.GetOrderAsync(Order.UniqueId))!);
        public LegacyController Controller() => new(_scope.Instance, Database.Factory, Configuration.Resolver);
        public void Dispose() { _scope.Dispose(); _cache.Dispose(); Database.Dispose(); }
    }

    // Deliberately uses the original constructor and ignores hangfireJobs; no call to base.
    private sealed class LegacyController : CheckoutControllerService
    {
        public bool Wholesale { get; set; }
        public bool SkipReservations { get; set; }
        public bool DeferAttachment { get; set; }
        public bool PaymentCalled { get; private set; }
        public Func<Task<CheckoutResponse?>>? AfterLines { get; set; }
        public LegacyController(Configuration config, DatabaseFactory database, IServiceProvider services)
            : base(NullLogger.Instance, config, database, Mock.Of<IMemberService>(), new HttpContextAccessor(), null!,
                services.GetRequiredService<IServiceScopeFactory>(), services) { }

        protected override Task<IOrderInfo> UpdateOrderDateAsync(Dictionary<string, string> collection, IOrderInfo order,
            Guid? paymentProviderKey = null, Guid? shippingProviderKey = null, CancellationToken ct = default) => Task.FromResult(order);
        protected override Task<CheckoutResponse?> ValidationAndOrderUpdatesAsync(PaymentRequest request, IOrderInfo order, CancellationToken ct)
            => Task.FromResult<CheckoutResponse?>(null);
        protected override async Task<CheckoutResponse?> ProcessOrderLinesAsync(PaymentRequest request, IOrderInfo order, ICollection<string> hangfireJobs, CancellationToken ct = default)
        {
            order.CustomerInformation.Customer.Properties["customerName"] = "Customer";
            order.CustomerInformation.Customer.Properties["customerEmail"] = "customer@example.test";
            order.CustomerInformation.Customer.Properties["isDistributor"] = Wholesale ? "true" : "false";
            var wholesale = Wholesale;
            foreach (var line in order.OrderLines)
            {
                if (SkipReservations) continue;
                if (line.Product.Backorder && wholesale) continue;
                var product = await Catalog.Instance.GetProductAsync(line.ProductKey, order.StoreInfo.Alias, ct: ct);
                if (line.Product.VariantGroups.Any())
                {
                    foreach (var selected in line.Product.VariantGroups.SelectMany(x => x.Variants))
                    {
                        var variant = await Catalog.Instance.GetVariantAsync(selected.Key, order.StoreInfo.Alias, ct: ct);
                        if (variant!.Stock < line.Quantity) return new CheckoutResponse { HttpStatusCode = 530 };
                        var id = await Stock.Instance.ReserveStockAsync(variant.Key, -line.Quantity, ct: ct);
                        if (DeferAttachment) hangfireJobs.Add(id);
                        else await Order.Instance.AddHangfireJobsToOrderAsync([id], order, ct: ct);
                    }
                }
                else
                {
                    if (product!.Stock < line.Quantity) return new CheckoutResponse { HttpStatusCode = 530 };
                    var id = await Stock.Instance.ReserveStockAsync(product.Key, -line.Quantity, ct: ct);
                    if (DeferAttachment) hangfireJobs.Add(id);
                    else await Order.Instance.AddHangfireJobsToOrderAsync([id], order, ct: ct);
                }
            }
            return AfterLines == null ? null : await AfterLines();
        }
        protected override Task<string> CreateOrderTitleAsync(PaymentRequest request, IOrderInfo order, IStore store, CancellationToken ct) => Task.FromResult("order");
        protected override Task<CheckoutResponse> ProcessPaymentAsync(PaymentRequest request, IOrderInfo order, string title, CancellationToken ct)
        {
            PaymentCalled = true;
            return Task.FromResult(new CheckoutResponse { HttpStatusCode = 230 });
        }
    }
}

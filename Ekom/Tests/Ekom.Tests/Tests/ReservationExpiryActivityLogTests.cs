using Ekom.Cache;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Collections.Concurrent;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class ReservationExpiryActivityLogTests
{
    [Fact]
    public async Task TimeoutPersistsAttemptBeforeRestorationAndRestoresOnlyOnce()
    {
        using var fixture = new ActivityDatabase();
        var key = await fixture.Database.SeedAsync(10);
        var reservation = await fixture.ReserveAsync(key);
        await fixture.Database.MakeDueAsync(reservation.ReservationId!);
        using var db = fixture.Database.Factory.GetDatabase();
        // The attempted activity must already be committed when the stock update starts.
        db.Execute("CREATE TRIGGER RequireAttempt BEFORE UPDATE ON EkomStock WHEN NEW.Stock > OLD.Stock AND NOT EXISTS (SELECT 1 FROM EkomOrdersActivityLog WHERE Log LIKE 'Reservation release Attempted%') BEGIN SELECT RAISE(ABORT, 'missing attempt'); END");

        Assert.Equal(1, await fixture.Service.ExpireDueAsync(10));
        Assert.Equal(StockReservationStatus.AlreadyExpired,
            (await fixture.Service.ExpireAsync(reservation.ReservationId!)).Status);
        Assert.Equal(10, await fixture.Database.StockAsync(key));
        var logs = await fixture.LogsAsync();
        Assert.Equal(4, logs.Count);
        Assert.All(logs, log =>
        {
            Assert.Equal(fixture.OrderId, log.Key);
            Assert.Contains(reservation.ReservationId!, log.Log);
            Assert.Contains(key.ToString(), log.Log);
            Assert.Contains("Quantity: 3", log.Log);
            Assert.Contains("Reason: timeout", log.Log);
        });
        Assert.Single(logs.Where(x => x.Log.Contains("RestoredQuantity: 3", StringComparison.Ordinal)));
        Assert.Contains(logs, x => x.Log.Contains("AlreadyExpired", StringComparison.Ordinal) && x.Log.Contains("RestoredQuantity: 0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FailedRestorationRollsBackAndPersistsSafeFailure()
    {
        using var fixture = new ActivityDatabase();
        var key = await fixture.Database.SeedAsync(10);
        var reservation = await fixture.ReserveAsync(key);
        await fixture.Database.MakeDueAsync(reservation.ReservationId!);
        using var db = fixture.Database.Factory.GetDatabase();
        const string secret = "private-payment-or-coupon-secret";
        db.Execute($"CREATE TRIGGER FailRestore BEFORE UPDATE ON EkomStock WHEN NEW.Stock > OLD.Stock BEGIN SELECT RAISE(ABORT, '{secret}'); END");

        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Service.ExpireAsync(reservation.ReservationId!));

        Assert.Equal(StockReservationState.Active, (await db.StockReservations.SingleAsync()).State);
        Assert.Equal(7, await fixture.Database.StockAsync(key));
        var logs = await fixture.LogsAsync();
        Assert.Equal(2, logs.Count);
        Assert.Contains(logs, x => x.Log.Contains("Attempted", StringComparison.Ordinal));
        var failed = Assert.Single(logs.Where(x => x.Log.Contains("release Failed", StringComparison.Ordinal)));
        Assert.Equal(OrderActivityLogType.Alert, failed.LogType);
        Assert.Contains("FailureType:", failed.Log);
        Assert.Contains("RestoredQuantity: 0", failed.Log);
        Assert.All(logs, x => Assert.DoesNotContain(secret, x.Log));
        Assert.DoesNotContain(logs, x => x.LogType == OrderActivityLogType.Success);
    }

    [Fact]
    public async Task NotDueAttemptDoesNotClaimReturnedQuantity()
    {
        using var fixture = new ActivityDatabase();
        var key = await fixture.Database.SeedAsync(10);
        var reservation = await fixture.ReserveAsync(key);

        Assert.Equal(StockReservationStatus.NotDue, (await fixture.Service.ExpireAsync(reservation.ReservationId!)).Status);
        Assert.Equal(7, await fixture.Database.StockAsync(key));
        var logs = await fixture.LogsAsync();
        Assert.Equal(2, logs.Count);
        Assert.Contains(logs, x => x.Log.Contains("NotDue", StringComparison.Ordinal));
        Assert.All(logs, x => Assert.Contains("RestoredQuantity: 0", x.Log));
    }

    [Fact]
    public async Task LateConsumeLogsTimeoutButRepeatedConsumeDoesNotClaimRestoration()
    {
        using var fixture = new ActivityDatabase();
        var key = await fixture.Database.SeedAsync(10);
        var reservation = await fixture.ReserveAsync(key);
        await fixture.Database.MakeDueAsync(reservation.ReservationId!);

        Assert.Equal(StockReservationStatus.LateConsumption, (await fixture.Service.ConsumeAsync(reservation.ReservationId!)).Status);
        Assert.Equal(StockReservationStatus.LateConsumption, (await fixture.Service.ConsumeAsync(reservation.ReservationId!)).Status);
        Assert.Equal(10, await fixture.Database.StockAsync(key));
        var logs = await fixture.LogsAsync();
        Assert.Equal(4, logs.Count);
        Assert.Single(logs.Where(x => x.Log.Contains("RestoredQuantity: 3", StringComparison.Ordinal)));
        Assert.Contains(logs, x => x.Log.Contains("LateConsumption", StringComparison.Ordinal) && x.Log.Contains("RestoredQuantity: 0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DiscountTimeoutDoesNotLeakCouponOrPaymentAttempt()
    {
        using var fixture = new ActivityDatabase();
        var key = Guid.NewGuid();
        const string coupon = "SECRET_COUPON_CODE";
        const string paymentAttempt = "SECRET_PAYMENT_ATTEMPT";
        await fixture.Database.SeedDiscountAsync(key, coupon, 5);
        var reservation = await fixture.Service.ReserveAsync(new()
        {
            Key = key, Quantity = 2, IsDiscount = true, Coupon = coupon,
            OrderId = fixture.OrderId.ToString(), PaymentAttemptId = paymentAttempt,
        });
        await fixture.Database.MakeDueAsync(reservation.ReservationId!);

        Assert.Equal(StockReservationStatus.Expired, (await fixture.Service.ExpireAsync(reservation.ReservationId!)).Status);
        using var db = fixture.Database.Factory.GetDatabase();
        Assert.Equal(5, (await db.DiscountStockData.SingleAsync()).Stock);
        var logs = await fixture.LogsAsync();
        Assert.Equal(2, logs.Count);
        Assert.All(logs, x =>
        {
            Assert.Contains(key.ToString(), x.Log);
            Assert.DoesNotContain(coupon, x.Log);
            Assert.DoesNotContain(paymentAttempt, x.Log);
        });
        Assert.Contains(logs, x => x.Log.Contains("RestoredQuantity: 2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ActivityWriteFailuresDoNotBlockOrReplayRestoration()
    {
        using var fixture = new ActivityDatabase();
        var key = await fixture.Database.SeedAsync(10);
        var reservation = await fixture.ReserveAsync(key);
        await fixture.Database.MakeDueAsync(reservation.ReservationId!);
        using var db = fixture.Database.Factory.GetDatabase();
        db.Execute("CREATE TRIGGER FailActivity BEFORE INSERT ON EkomOrdersActivityLog BEGIN SELECT RAISE(ABORT, 'activity unavailable'); END");

        Assert.Equal(StockReservationStatus.Expired, (await fixture.Service.ExpireAsync(reservation.ReservationId!)).Status);
        Assert.Equal(StockReservationStatus.AlreadyExpired, (await fixture.Service.ExpireAsync(reservation.ReservationId!)).Status);
        Assert.Equal(10, await fixture.Database.StockAsync(key));
        Assert.Empty(await fixture.LogsAsync());
    }

    [Fact]
    public async Task ManualReleaseAndOnTimeConsumeDoNotDuplicateCoordinatorLogs()
    {
        using var fixture = new ActivityDatabase();
        var key = await fixture.Database.SeedAsync(10);
        var manual = await fixture.ReserveAsync(key);
        var consume = await fixture.ReserveAsync(key);
        await fixture.Service.ReleaseAsync(manual.ReservationId!);
        await fixture.Service.ConsumeAsync(consume.ReservationId!);
        Assert.Empty(await fixture.LogsAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-an-order-guid")]
    public async Task ExpiryWithoutOrderGuidStillRestoresStock(string? orderId)
    {
        using var fixture = new ActivityDatabase();
        var key = await fixture.Database.SeedAsync(10);
        var reservation = await fixture.Service.ReserveAsync(new() { Key = key, Quantity = 3, OrderId = orderId });
        await fixture.Database.MakeDueAsync(reservation.ReservationId!);
        Assert.Equal(StockReservationStatus.Expired, (await fixture.Service.ExpireAsync(reservation.ReservationId!)).Status);
        Assert.Equal(10, await fixture.Database.StockAsync(key));
        Assert.Empty(await fixture.LogsAsync());
    }

    private sealed class ActivityDatabase : IDisposable
    {
        public StockReservationTests.ReservationDatabase Database { get; } = new();
        public Guid OrderId { get; } = Guid.NewGuid();
        public StockReservationService Service { get; }
        private readonly StockChangePublisher _publisher;

        public ActivityDatabase()
        {
            using var db = Database.Factory.GetDatabase();
            db.CreateTable<OrderActivityLog>();
            var config = new Configuration(new ConfigurationBuilder().Build());
            var stock = new Mock<IBaseCache<StockData>>();
            stock.SetupGet(x => x.Cache).Returns(Database.StockCache);
            var perStore = new Mock<IPerStoreCache<StockData>>();
            perStore.SetupGet(x => x.Cache).Returns(new ConcurrentDictionary<string, ConcurrentDictionary<Guid, StockData>>());
            _publisher = new StockChangePublisher(Database.Factory, config, stock.Object, perStore.Object,
                NullLogger<StockChangePublisher>.Instance);
            Service = new StockReservationService(Database.Factory, config, _publisher,
                NullLogger<StockReservationService>.Instance,
                new ActivityLogRepository(NullLogger<ActivityLogRepository>.Instance, Database.Factory));
        }

        public Task<StockReservationResult> ReserveAsync(Guid key)
            => Service.ReserveAsync(new() { Key = key, Quantity = 3, OrderId = OrderId.ToString() });

        public async Task<List<OrderActivityLog>> LogsAsync()
        {
            using var db = Database.Factory.GetDatabase();
            return await db.OrderActivityLog.ToListAsync();
        }

        public void Dispose()
        {
            _publisher.Dispose();
            Database.Dispose();
        }
    }
}

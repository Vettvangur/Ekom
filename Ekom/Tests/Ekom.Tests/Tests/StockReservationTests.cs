using Ekom.Cache;
using Ekom.Events;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Collections.Concurrent;
using Xunit;

namespace Ekom.Tests.Tests;

[CollectionDefinition("Reservations", DisableParallelization = true)]
public sealed class ReservationTestCollection;

[Collection("Reservations")]
public sealed class StockReservationTests
{
    [Fact]
    public async Task ReservationSurvivesServiceRestartAndReleasesExactlyOnce()
    {
        using var fixture = new ReservationDatabase();
        var key = await fixture.SeedAsync(10);
        var result = await fixture.Service.ReserveAsync(new() { Key = key, Quantity = 3 });
        Assert.Equal(StockReservationStatus.Created, result.Status);
        Assert.Equal(7, await fixture.StockAsync(key));
        var restarted = fixture.NewService();
        Assert.Equal(StockReservationStatus.Released, (await restarted.ReleaseAsync(result.ReservationId!)).Status);
        Assert.Equal(StockReservationStatus.AlreadyReleased, (await fixture.Service.ReleaseAsync(result.ReservationId!)).Status);
        Assert.Equal(10, await fixture.StockAsync(key));
    }

    [Fact]
    public async Task InsufficientStockLeavesNoReservationOrDeduction()
    {
        using var fixture = new ReservationDatabase();
        var key = await fixture.SeedAsync(2);
        Assert.Equal(StockReservationStatus.InsufficientStock,
            (await fixture.Service.ReserveAsync(new() { Key = key, Quantity = 3 })).Status);
        using var db = fixture.Factory.GetDatabase();
        Assert.Empty(await db.StockReservations.ToListAsync());
        Assert.Equal(2, await fixture.StockAsync(key));
    }

    [Fact]
    public async Task FailedReservationInsertRollsBackStockDeduction()
    {
        using var fixture = new ReservationDatabase();
        var key = await fixture.SeedAsync(10);
        using var db = fixture.Factory.GetDatabase();
        db.Execute("CREATE TRIGGER FailReservation BEFORE INSERT ON EkomStockReservation BEGIN SELECT RAISE(ABORT, 'test failure'); END");
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Service.ReserveAsync(new() { Key = key, Quantity = 3 }));
        Assert.Equal(10, await fixture.StockAsync(key));
        Assert.Empty(await db.StockReservations.ToListAsync());
    }

    [Fact]
    public async Task FailedRestorationKeepsReservationActiveForRetry()
    {
        using var fixture = new ReservationDatabase();
        var key = await fixture.SeedAsync(10);
        var reservation = await fixture.Service.ReserveAsync(new() { Key = key, Quantity = 3 });
        using var db = fixture.Factory.GetDatabase();
        db.Execute("CREATE TRIGGER FailRestore BEFORE UPDATE ON EkomStock WHEN NEW.Stock > OLD.Stock BEGIN SELECT RAISE(ABORT, 'test failure'); END");
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Service.ReleaseAsync(reservation.ReservationId!));
        Assert.Equal(StockReservationState.Active, (await db.StockReservations.SingleAsync()).State);
        Assert.Equal(7, await fixture.StockAsync(key));
    }

    [Fact]
    public async Task ConcurrentCreationUsesSqlIdempotencyConstraintAndRejectsChangedPayload()
    {
        using var fixture = new ReservationDatabase();
        var key = await fixture.SeedAsync(20);
        var request = new StockReservationRequest
        {
            Key = key, Quantity = 3, IdempotencyKey = "payment-line-1", OrderId = "order", PaymentAttemptId = "attempt",
        };
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => fixture.NewService().ReserveAsync(request))));
        Assert.Single(results.Where(x => x.Status == StockReservationStatus.Created));
        Assert.Single(results.Select(x => x.ReservationId).Distinct());
        Assert.Equal(17, await fixture.StockAsync(key));
        foreach (var changed in new[]
        {
            request with { Quantity = 4 }, request with { OrderId = "different" },
            request with { PaymentAttemptId = "different" }, request with { Duration = TimeSpan.FromMinutes(1) },
            request with { StoreAlias = "different" },
        })
            Assert.Equal(StockReservationStatus.Conflict, (await fixture.Service.ReserveAsync(changed)).Status);
        using var db = fixture.Factory.GetDatabase();
        var row = await db.StockReservations.SingleAsync();
        row.Id = Guid.NewGuid().ToString();
        await Assert.ThrowsAnyAsync<Exception>(() => db.InsertAsync(row));
        Assert.Equal(17, await fixture.StockAsync(key));
    }

    [Fact]
    public async Task IdempotencyNormalizesDecimalScaleAndReportsTerminalStateUntilCleanup()
    {
        using var fixture = new ReservationDatabase();
        var key = await fixture.SeedAsync(20);
        var request = new StockReservationRequest { Key = key, Quantity = 3m, IdempotencyKey = "same" };
        var first = await fixture.Service.ReserveAsync(request);
        var same = await fixture.Service.ReserveAsync(request with { Quantity = 3.00m });
        Assert.Equal(StockReservationStatus.AlreadyExists, same.Status);
        Assert.Equal(first.ReservationId, same.ReservationId);
        Assert.Equal(StockReservationState.Active, same.State);
        Assert.InRange(first.ExpiresUtc!.Value - DateTime.UtcNow, TimeSpan.FromMinutes(29), TimeSpan.FromMinutes(30));
        await fixture.Service.ConsumeAsync(first.ReservationId!);
        var terminal = await fixture.Service.ReserveAsync(request);
        Assert.Equal(StockReservationStatus.AlreadyExists, terminal.Status);
        Assert.Equal(StockReservationState.Consumed, terminal.State);
        Assert.Equal(0, await fixture.Service.CleanupAsync(DateTime.UtcNow.AddDays(-7), 100));
        Assert.Equal(1, await fixture.Service.CleanupAsync(DateTime.UtcNow.AddMinutes(1), 100));
        var newHold = await fixture.Service.ReserveAsync(request);
        Assert.Equal(StockReservationStatus.Created, newHold.Status);
        Assert.NotEqual(first.ReservationId, newHold.ReservationId);
        Assert.Equal(14, await fixture.StockAsync(key));
    }

    [Fact]
    public async Task ChangingCouponWithSameCreationKeyConflicts()
    {
        using var fixture = new ReservationDatabase();
        var key = Guid.NewGuid();
        await fixture.SeedDiscountAsync(key, "coupon", 5);
        var request = new StockReservationRequest { Key = key, Quantity = 1, IsDiscount = true, Coupon = "coupon", IdempotencyKey = "attempt" };
        var first = await fixture.Service.ReserveAsync(request);
        Assert.Equal(StockReservationStatus.Created, first.Status);
        Assert.Equal(StockReservationStatus.Conflict, (await fixture.Service.ReserveAsync(request with { Coupon = null })).Status);
    }

    [Fact]
    public async Task DistinctCreationsCannotOversellAcrossServices()
    {
        using var fixture = new ReservationDatabase();
        var key = await fixture.SeedAsync(3);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            fixture.NewService().ReserveAsync(new() { Key = key, Quantity = 1 }))));
        Assert.Equal(3, results.Count(x => x.Status == StockReservationStatus.Created));
        Assert.Equal(5, results.Count(x => x.Status == StockReservationStatus.InsufficientStock));
        Assert.Equal(0, await fixture.StockAsync(key));
    }

    [Fact]
    public async Task FractionalProductStockReservesAndRestoresAtDatabasePrecision()
    {
        using var fixture = new ReservationDatabase();
        var key = await fixture.SeedAsync(0.3m);
        var reservations = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var result = await fixture.Service.ReserveAsync(new() { Key = key, Quantity = 0.1m });
            Assert.Equal(StockReservationStatus.Created, result.Status);
            reservations.Add(result.ReservationId!);
        }
        Assert.Equal(0, await fixture.StockAsync(key));
        foreach (var id in reservations) await fixture.Service.ReleaseAsync(id);
        Assert.Equal(0.3m, await fixture.StockAsync(key));
    }

    [Fact]
    public async Task ConcurrentExpiryAndCancellationRestoreOnce()
    {
        using var fixture = new ReservationDatabase();
        var key = await fixture.SeedAsync(10);
        var reservation = await fixture.Service.ReserveAsync(new() { Key = key, Quantity = 3 });
        await fixture.MakeDueAsync(reservation.ReservationId!);
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => Task.Run(() => i % 2 == 0
            ? fixture.NewService().ExpireAsync(reservation.ReservationId!)
            : fixture.NewService().ReleaseAsync(reservation.ReservationId!))));
        Assert.Single(results.Where(x => x.Status is StockReservationStatus.Expired or StockReservationStatus.Released));
        Assert.Equal(10, await fixture.StockAsync(key));
    }

    [Fact]
    public async Task ConsumeAndReleaseRaceHasOneTerminalWinner()
    {
        using var fixture = new ReservationDatabase();
        var key = await fixture.SeedAsync(10);
        var reservation = await fixture.Service.ReserveAsync(new() { Key = key, Quantity = 3 });
        var results = await Task.WhenAll(
            Task.Run(() => fixture.NewService().ConsumeAsync(reservation.ReservationId!)),
            Task.Run(() => fixture.NewService().ReleaseAsync(reservation.ReservationId!)));
        Assert.Single(results.Where(x => x.Status is StockReservationStatus.Consumed or StockReservationStatus.Released));
        var consumed = results.Any(x => x.Status == StockReservationStatus.Consumed);
        Assert.Equal(consumed ? 7 : 10, await fixture.StockAsync(key));
    }

    [Fact]
    public async Task ConsumedReservationNeverRestoresAndLatePaymentIsExplicit()
    {
        using var fixture = new ReservationDatabase();
        var key = await fixture.SeedAsync(10);
        var first = await fixture.Service.ReserveAsync(new() { Key = key, Quantity = 3 });
        Assert.Equal(StockReservationStatus.NotDue, (await fixture.Service.ExpireAsync(first.ReservationId!)).Status);
        Assert.Equal(StockReservationStatus.Consumed, (await fixture.Service.ConsumeAsync(first.ReservationId!)).Status);
        Assert.Equal(StockReservationStatus.AlreadyConsumed, (await fixture.Service.ReleaseAsync(first.ReservationId!)).Status);
        var late = await fixture.Service.ReserveAsync(new() { Key = key, Quantity = 2 });
        await fixture.MakeDueAsync(late.ReservationId!);
        Assert.Equal(StockReservationStatus.LateConsumption, (await fixture.Service.ConsumeAsync(late.ReservationId!)).Status);
        Assert.Equal(StockReservationStatus.LateConsumption, (await fixture.Service.ConsumeAsync(late.ReservationId!)).Status);
        Assert.Equal(7, await fixture.StockAsync(key));
        Assert.Equal(StockReservationStatus.NotFound, (await fixture.Service.ReleaseAsync("old-hangfire-id")).Status);
    }

    [Fact]
    public async Task ExactStoreAndCouponRemainCapturedAcrossConfigurationChanges()
    {
        using var fixture = new ReservationDatabase(perStore: true);
        var key = await fixture.SeedAsync(10, "store_with_underscore");
        await fixture.SeedAsync(20, "other", key);
        var reservation = await fixture.Service.ReserveAsync(new() { Key = key, StoreAlias = "store_with_underscore", Quantity = 3 });
        // The persisted SQL identity wins over a later global-stock configuration.
        await fixture.NewService(perStore: false).ReleaseAsync(reservation.ReservationId!);
        Assert.Equal(10, await fixture.StockAsync(key, "store_with_underscore"));
        Assert.Equal(20, await fixture.StockAsync(key, "other"));
        var discountKey = Guid.NewGuid();
        await fixture.SeedDiscountAsync(discountKey, null, 30);
        await fixture.SeedDiscountAsync(discountKey, "Exact_Coupon", 4);
        await fixture.SeedDiscountAsync(discountKey, "other", 6);
        var discount = await fixture.Service.ReserveAsync(new()
        {
            Key = discountKey, IsDiscount = true, Coupon = "Exact_Coupon", StoreAlias = "store_with_underscore", Quantity = 2,
        });
        await fixture.MakeDueAsync(discount.ReservationId!);
        await fixture.Service.ExpireAsync(discount.ReservationId!);
        using var db = fixture.Factory.GetDatabase();
        Assert.Equal(30, (await db.DiscountStockData.SingleAsync(x => x.UniqueId == discountKey.ToString())).Stock);
        Assert.Equal(4, (await db.DiscountStockData.SingleAsync(x => x.UniqueId == $"{discountKey}_Exact_Coupon")).Stock);
        Assert.Equal(6, (await db.DiscountStockData.SingleAsync(x => x.UniqueId == $"{discountKey}_other")).Stock);
        var row = await db.StockReservations.SingleAsync(x => x.Id == discount.ReservationId);
        Assert.Equal("Exact_Coupon", row.Coupon);
        Assert.Equal("store_with_underscore", row.StoreAlias);
    }

    [Fact]
    public async Task ExpiryAndCleanupAreBoundedAndCleanupKeepsActiveRecords()
    {
        using var fixture = new ReservationDatabase();
        var key = await fixture.SeedAsync(20);
        for (int i = 0; i < 4; i++)
        {
            var reservation = await fixture.Service.ReserveAsync(new() { Key = key, Quantity = 1 });
            await fixture.MakeDueAsync(reservation.ReservationId!);
        }
        Assert.Equal(2, await fixture.Service.ExpireDueAsync(2));
        Assert.Equal(1, await fixture.Service.CleanupAsync(DateTime.UtcNow.AddMinutes(1), 1));
        using var db = fixture.Factory.GetDatabase();
        Assert.Equal(2, await db.StockReservations.CountAsync(x => x.State == StockReservationState.Active));
        Assert.Equal(3, await db.StockReservations.CountAsync());
    }

    [Fact]
    public async Task BrokenExpiryBacksOffAndDoesNotStarveOtherReservations()
    {
        using var fixture = new ReservationDatabase();
        var broken = await fixture.SeedAsync(2);
        var valid = await fixture.SeedAsync(2);
        var first = await fixture.Service.ReserveAsync(new() { Key = broken, Quantity = 1 });
        var second = await fixture.Service.ReserveAsync(new() { Key = valid, Quantity = 1 });
        await fixture.MakeDueAsync(first.ReservationId!, minutesAgo: 2);
        await fixture.MakeDueAsync(second.ReservationId!);
        using var db = fixture.Factory.GetDatabase();
        await db.StockData.Where(x => x.UniqueId == broken.ToString()).DeleteAsync();
        Assert.Equal(0, await fixture.Service.ExpireDueAsync(1));
        var row = await db.StockReservations.SingleAsync(x => x.Id == first.ReservationId);
        Assert.Equal(StockReservationState.Active, row.State);
        Assert.True(row.RetryAfterUtc > DateTime.UtcNow);
        Assert.Equal(1, await fixture.Service.ExpireDueAsync(1));
        Assert.Equal(2, await fixture.StockAsync(valid));
    }

    [Fact]
    public async Task EventFailureDoesNotReplayCommittedRestoration()
    {
        using var fixture = new ReservationDatabase();
        var key = await fixture.SeedAsync(10);
        Task Fail(object sender, StockChangedEventArgs args, CancellationToken ct) => throw new InvalidOperationException("subscriber failed");
        StockEvents.StockChangedAsync += Fail;
        try
        {
            var reservation = await fixture.Service.ReserveAsync(new() { Key = key, Quantity = 3 });
            Assert.Equal(StockReservationStatus.Released, (await fixture.Service.ReleaseAsync(reservation.ReservationId!)).Status);
            Assert.Equal(StockReservationStatus.AlreadyReleased, (await fixture.Service.ReleaseAsync(reservation.ReservationId!)).Status);
            Assert.Equal(10, await fixture.StockAsync(key));
            Assert.Equal(10, fixture.StockCache[key].Stock);
        }
        finally { StockEvents.StockChangedAsync -= Fail; }
    }

    [Fact]
    public async Task CacheFailureDoesNotHideOrReplayCommittedLifecycle()
    {
        using var fixture = new ReservationDatabase();
        var key = await fixture.SeedAsync(10);
        fixture.FailCacheReads();
        var reservation = await fixture.Service.ReserveAsync(new() { Key = key, Quantity = 3 });
        Assert.Equal(StockReservationStatus.Created, reservation.Status);
        Assert.Equal(7, await fixture.StockAsync(key));
        Assert.Equal(StockReservationStatus.Released, (await fixture.Service.ReleaseAsync(reservation.ReservationId!)).Status);
        Assert.Equal(StockReservationStatus.AlreadyReleased, (await fixture.Service.ReleaseAsync(reservation.ReservationId!)).Status);
        Assert.Equal(10, await fixture.StockAsync(key));
    }

    [Fact]
    public async Task CancellationFromSubscriberDoesNotCancelCommittedReservation()
    {
        using var fixture = new ReservationDatabase();
        using var cancellation = new CancellationTokenSource();
        var key = await fixture.SeedAsync(10);
        Task Cancel(object sender, StockChangedEventArgs args, CancellationToken ct)
        {
            cancellation.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
        StockEvents.StockChangedAsync += Cancel;
        try
        {
            var reservation = await fixture.Service.ReserveAsync(new() { Key = key, Quantity = 3 }, cancellation.Token);
            Assert.Equal(StockReservationStatus.Created, reservation.Status);
            Assert.Equal(7, await fixture.StockAsync(key));
        }
        finally { StockEvents.StockChangedAsync -= Cancel; }
    }

    [Fact]
    public async Task StockIncrementsUseSqlBalanceInsteadOfStaleCache()
    {
        using var fixture = new ReservationDatabase();
        var key = await fixture.SeedAsync(10);
        fixture.StockCache[key] = new StockData { UniqueId = key.ToString(), Stock = 1000 };
        var reservation = await fixture.Service.ReserveAsync(new() { Key = key, Quantity = 3 });
        var stock = fixture.NewStockApi();
        fixture.StockCache[key].Stock = 1000;
        await stock.IncrementStockAsync(key, -2);
        Assert.Equal(5, await fixture.StockAsync(key));
        await fixture.Service.ReleaseAsync(reservation.ReservationId!);
        Assert.Equal(8, await fixture.StockAsync(key));
        await Assert.ThrowsAsync<NotEnoughStockException>(() => stock.IncrementStockAsync(key, -9));
        // Explicit replacement must still write when the cached value already equals the request.
        fixture.StockCache[key].Stock = 20;
        await stock.SetStockAsync(key, 20);
        Assert.Equal(20, await fixture.StockAsync(key));
    }

    [Theory]
    [InlineData(false, 10)]
    [InlineData(false, 1000)]
    [InlineData(true, 10)]
    [InlineData(true, 1000)]
    public async Task EqualSetAndZeroIncrementSkipSqlWritesAndEventsButRefreshCache(bool increment, decimal cachedBalance)
    {
        using var fixture = new ReservationDatabase();
        var key = await fixture.SeedAsync(10);
        using var db = fixture.Factory.GetDatabase();
        var originalTimestamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await db.StockData.Where(x => x.UniqueId == key.ToString()).Set(x => x.UpdateDate, originalTimestamp).UpdateAsync();
        // Catch even an UPDATE that happens to preserve the timestamp.
        db.Execute("CREATE TRIGGER FailNoOpWrite BEFORE UPDATE ON EkomStock BEGIN SELECT RAISE(ABORT, 'unexpected no-op write'); END");
        fixture.StockCache[key] = new StockData { UniqueId = key.ToString(), Stock = cachedBalance };
        int events = 0;
        Task OnChanged(object sender, StockChangedEventArgs args, CancellationToken ct)
        {
            if (args.Key == key) Interlocked.Increment(ref events);
            return Task.CompletedTask;
        }
        StockEvents.StockChangedAsync += OnChanged;
        try
        {
            var stock = fixture.NewStockApi();
            if (increment)
                await stock.IncrementStockAsync(key, 0);
            else
                Assert.True(await stock.SetStockAsync(key, 10));

            var persisted = await db.StockData.SingleAsync(x => x.UniqueId == key.ToString());
            Assert.Equal(10, persisted.Stock);
            Assert.Equal(originalTimestamp, persisted.UpdateDate);
            Assert.Equal(10, fixture.StockCache[key].Stock);
            Assert.Equal(originalTimestamp, fixture.StockCache[key].UpdateDate);
            Assert.Equal(0, events);
        }
        finally { StockEvents.StockChangedAsync -= OnChanged; }
    }

    [Fact]
    public async Task CompatibilityStockApiReturnsNativeIdsAndPreservesConsumeMeaning()
    {
        using var fixture = new ReservationDatabase();
        var key = await fixture.SeedAsync(10);
        var api = fixture.NewStockApi();
        var id = await api.ReserveStockAsync(key, -2);
        Assert.True(Guid.TryParse(id, out _));
        api.CancelRollback(id);
        await api.RollbackJobAsync(id);
        api.CompleteRollback(id);
        Assert.Equal(8, await fixture.StockAsync(key));
        var release = await api.ReserveStockAsync(key, -2);
        api.CompleteRollback(release);
        api.CompleteRollback(release);
        Assert.Equal(8, await fixture.StockAsync(key));
    }

    [Fact]
    public async Task CancellationBeforeCreationMakesNoChanges()
    {
        using var fixture = new ReservationDatabase();
        var key = await fixture.SeedAsync(10);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Service.ReserveAsync(new() { Key = key, Quantity = 3 }, cancellation.Token));
        Assert.Equal(10, await fixture.StockAsync(key));
    }

    [Theory]
    [InlineData("{}", 0)]
    [InlineData("{\"HangfireJobs\":null}", 0)]
    [InlineData("{\"HangfireJobs\":[\"legacy\"]}", 1)]
    [InlineData("{\"ReservationIds\":[\"native\"],\"HangfireJobs\":[\"legacy\"]}", 1)]
    public void OrderJsonReadsLegacyAndNeutralCollections(string json, int count)
    {
        var document = JObject.Parse(json);
        document[nameof(OrderInfo.OrderLines)] = new JArray();
        var info = new OrderInfo(new OrderData { OrderInfo = document.ToString() });
        Assert.Equal(count, info.ReservationIds.Count);
        Assert.Same(info.ReservationIds, info.HangfireJobs);
        if (json.Contains("native", StringComparison.Ordinal)) Assert.Equal("native", Assert.Single(info.ReservationIds));
        info._hangfireJobs.Add("next");
        var serialized = JObject.Parse(JsonConvert.SerializeObject(info, new JsonSerializerSettings { ContractResolver = new ReservationOnlyContractResolver() }));
        Assert.True(JToken.DeepEquals(serialized[nameof(OrderInfo.ReservationIds)], serialized[nameof(OrderInfo.HangfireJobs)]));
        serialized[nameof(OrderInfo.OrderLines)] = new JArray();
        var roundTrip = new OrderInfo(new OrderData { OrderInfo = serialized.ToString() });
        Assert.Equal(info.ReservationIds, roundTrip.ReservationIds);
    }

    [Fact]
    public async Task WorkerWaitsForSchemaAndInitializationAndStopsOnCancellation()
    {
        var service = new Mock<IStockReservationService>();
        var swept = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Setup(x => x.ExpireDueAsync(2, It.IsAny<CancellationToken>())).Returns(() => { swept.TrySetResult(); return Task.FromResult(0); });
        var ready = new StockReservationReadiness();
        using var worker = new StockReservationWorker(service.Object, ready,
            Options.Create(new StockReservationOptions { PollInterval = TimeSpan.FromMilliseconds(10), BatchSize = 2 }),
            NullLogger<StockReservationWorker>.Instance);
        await worker.StartAsync(default);
        await Task.Delay(40);
        service.Verify(x => x.ExpireDueAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        ready.SchemaReady();
        await Task.Delay(40);
        service.Verify(x => x.ExpireDueAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        ready.Initialized();
        await swept.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(default);
        Assert.True(worker.ExecuteTask!.IsCompleted);
    }

    [Fact]
    public async Task DisabledWorkerNeverSweeps()
    {
        var service = new Mock<IStockReservationService>(MockBehavior.Strict);
        var ready = new StockReservationReadiness();
        ready.SchemaReady();
        ready.Initialized();
        using var worker = new StockReservationWorker(service.Object, ready,
            Options.Create(new StockReservationOptions { WorkerEnabled = false }), NullLogger<StockReservationWorker>.Instance);
        await worker.StartAsync(default);
        await worker.StopAsync(default);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WorkerRecoversAfterSweepFailure()
    {
        var service = new Mock<IStockReservationService>();
        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        service.Setup(x => x.ExpireDueAsync(100, It.IsAny<CancellationToken>())).Returns(() =>
        {
            if (Interlocked.Increment(ref calls) == 1) throw new InvalidOperationException("temporary database failure");
            recovered.TrySetResult();
            return Task.FromResult(0);
        });
        var ready = new StockReservationReadiness();
        ready.SchemaReady();
        ready.Initialized();
        using var worker = new StockReservationWorker(service.Object, ready,
            Options.Create(new StockReservationOptions { PollInterval = TimeSpan.FromMilliseconds(10) }), NullLogger<StockReservationWorker>.Instance);
        await worker.StartAsync(default);
        await recovered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await worker.StopAsync(default);
        Assert.True(calls >= 2);
    }

    [Fact]
    public void DefaultInterfacePropertySupportsLegacyImplementers()
    {
        var ids = new[] { "legacy-id" };
        var legacy = new Mock<IOrderInfo> { CallBase = true };
        legacy.SetupGet(x => x.HangfireJobs).Returns(ids);
        Assert.Same(ids, legacy.Object.ReservationIds);
    }

    private sealed class ReservationOnlyContractResolver : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            => base.CreateProperties(type, memberSerialization).Where(x => x.PropertyName is nameof(OrderInfo.ReservationIds) or nameof(OrderInfo.HangfireJobs)).ToList();
    }

    internal sealed class ReservationDatabase : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"ekom-reservation-{Guid.NewGuid():N}.sqlite");
        private readonly bool _perStore;
        public DatabaseFactory Factory { get; }
        public StockReservationService Service { get; }
        public ConcurrentDictionary<Guid, StockData> StockCache { get; } = new();
        private readonly Mock<IBaseCache<StockData>> _stock = new();
        private readonly Mock<IPerStoreCache<StockData>> _perStoreCache = new();
        private readonly ConcurrentBag<StockChangePublisher> _publishers = new();

        public ReservationDatabase(bool perStore = false)
        {
            _perStore = perStore;
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoDbDSN"] = $"Data Source={_path};Pooling=False;Default Timeout=2",
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
            }).Build();
            var host = new Mock<IHostEnvironment>();
            host.SetupGet(x => x.ContentRootPath).Returns(Path.GetTempPath());
            Factory = new DatabaseFactory(config, host.Object);
            _stock.SetupGet(x => x.Cache).Returns(StockCache);
            _perStoreCache.SetupGet(x => x.Cache).Returns(new ConcurrentDictionary<string, ConcurrentDictionary<Guid, StockData>>());
            using var db = Factory.GetDatabase();
            db.CreateTable<StockData>();
            db.CreateTable<DiscountStockData>();
            var schema = new DatabaseService(Factory, NullLogger<DatabaseService>.Instance, new StockReservationReadiness());
            schema.EnsureStockReservationTable();
            schema.EnsureStockReservationTable();
            Service = NewService();
        }

        private Configuration Config(bool perStore) => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ekom:PerStoreStock"] = perStore.ToString(), ["Ekom:ReservationTimeout"] = "30",
        }).Build());

        private StockChangePublisher Publisher(Configuration config)
        {
            var publisher = new StockChangePublisher(Factory, config, _stock.Object, _perStoreCache.Object, NullLogger<StockChangePublisher>.Instance);
            _publishers.Add(publisher);
            return publisher;
        }

        public void FailCacheReads() => _stock.SetupGet(x => x.Cache).Throws(new InvalidOperationException("cache unavailable"));

        public StockReservationService NewService(bool? perStore = null)
        {
            var config = Config(perStore ?? _perStore);
            return new StockReservationService(Factory, config, Publisher(config), NullLogger<StockReservationService>.Instance);
        }

        public API.Stock NewStockApi()
        {
            var config = Config(_perStore);
            return new API.Stock(config, NullLogger<API.Stock>.Instance, _stock.Object,
                new StockRepository(Factory, NullLogger<StockRepository>.Instance),
                new DiscountStockRepository(NullLogger<DiscountStockRepository>.Instance, Factory),
                Mock.Of<IStoreService>(), _perStoreCache.Object, Service, Publisher(config));
        }

        public CheckoutReservationService NewCheckout(Configuration? configuration = null, ICheckoutStockPolicy? policy = null)
        {
            var config = configuration ?? Config(_perStore);
            return new CheckoutReservationService(Factory, config, Service, Publisher(config), policy);
        }

        public async Task<Guid> SeedAsync(decimal stock, string? store = null, Guid? key = null)
        {
            var id = key ?? Guid.NewGuid();
            using var db = Factory.GetDatabase();
            await db.InsertAsync(new StockData { UniqueId = store == null ? id.ToString() : $"{store}_{id}", Stock = stock, CreateDate = DateTime.UtcNow, UpdateDate = DateTime.UtcNow });
            return id;
        }

        public async Task SeedDiscountAsync(Guid key, string? coupon, int stock)
        {
            using var db = Factory.GetDatabase();
            await db.InsertAsync(new DiscountStockData { UniqueId = coupon == null ? key.ToString() : $"{key}_{coupon}", Stock = stock, CreateDate = DateTime.UtcNow, UpdateDate = DateTime.UtcNow });
        }

        public async Task<decimal> StockAsync(Guid key, string? store = null)
        {
            using var db = Factory.GetDatabase();
            var id = store == null ? key.ToString() : $"{store}_{key}";
            return (await db.StockData.SingleAsync(x => x.UniqueId == id)).Stock;
        }

        public async Task MakeDueAsync(string id, int minutesAgo = 1)
        {
            using var db = Factory.GetDatabase();
            await db.StockReservations.Where(x => x.Id == id).Set(x => x.ExpiresUtc, DateTime.UtcNow.AddMinutes(-minutesAgo)).UpdateAsync();
        }

        public void Dispose()
        {
            foreach (var publisher in _publishers) publisher.Dispose();
            File.Delete(_path);
        }
    }
}

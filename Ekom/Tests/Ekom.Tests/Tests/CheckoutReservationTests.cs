using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Services;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Configuration;
using Xunit;
using ReservationDatabase = Ekom.Tests.Tests.StockReservationTests.ReservationDatabase;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class CheckoutReservationTests
{
    [Theory]
    [InlineData(null, null, 30)]
    [InlineData(null, "12", 12)]
    [InlineData("7.5", null, 7.5)]
    [InlineData("7.5", "12", 7.5)]
    public void TimeoutDefaultsAndNestedPrecedence(string? nested, string? legacy, double minutes)
    {
        var config = Config(("Ekom:Reservations:Timeout", nested), ("Ekom:ReservationTimeout", legacy));
        Assert.Equal(TimeSpan.FromMinutes(minutes), config.ReservationTimeout);
        Assert.False(config.ReservationsEnabled);
        Assert.False(new StockReservationOptions().Enabled);
        Assert.Equal(30, new StockReservationOptions().Timeout);
        Assert.True(new StockReservationOptions().WorkerEnabled);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void CheckoutAndWorkerSwitchesAreIndependent(bool enabled, bool workerEnabled)
    {
        var root = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ekom:Reservations:Enabled"] = enabled.ToString(),
            ["Ekom:Reservations:WorkerEnabled"] = workerEnabled.ToString(),
        }).Build();
        var options = root.GetSection("Ekom:Reservations").Get<StockReservationOptions>()!;
        Assert.Equal(enabled, new Configuration(root).ReservationsEnabled);
        Assert.Equal(workerEnabled, options.WorkerEnabled);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task BothModesDeductOnceIncludingConcurrentDuplicateCompletion(bool enabled, bool perStore)
    {
        using var fixture = new ReservationDatabase(perStore);
        var store = perStore ? "main" : null;
        var key = await fixture.SeedAsync(10, store);
        var orderId = Guid.NewGuid();
        var requests = new[] { new StockReservationRequest { Key = key, Quantity = 3, StoreAlias = store, OrderId = orderId.ToString() } };
        var ids = new List<string>();
        var checkout = fixture.NewCheckout();
        await checkout.PrepareAsync(orderId, requests, ids, default, createReservations: enabled);
        Assert.Equal(enabled ? 7 : 10, await fixture.StockAsync(key, store));
        Assert.Equal(enabled ? 1 : 0, ids.Count);
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() =>
            fixture.NewCheckout().CompleteStockAsync(orderId, requests, ids, true, default))));
        Assert.Single(outcomes, x => x);
        Assert.Equal(7, await fixture.StockAsync(key, store));
        Assert.True(await checkout.IsCompletedAsync(orderId, default));
        await fixture.Service.CleanupAsync(DateTime.UtcNow.AddDays(1), 100);
        Assert.False(await checkout.CompleteStockAsync(orderId, requests, ids, true, default));
        Assert.Equal(7, await fixture.StockAsync(key, store));
        await Assert.ThrowsAsync<StockException>(() => checkout.PrepareAsync(orderId, requests, ids, default));
    }

    [Fact]
    public async Task PartialPreparationFailureCompensatesAndCanRetry()
    {
        using var fixture = new ReservationDatabase();
        var first = await fixture.SeedAsync(10);
        var second = await fixture.SeedAsync(0);
        var order = Guid.NewGuid();
        var requests = Requests(order, (first, 3), (second, 2));
        var ids = new List<string>();
        var checkout = fixture.NewCheckout();
        await Assert.ThrowsAsync<NotEnoughStockException>(() => checkout.PrepareAsync(order, requests, ids, default));
        Assert.Empty(ids);
        Assert.Equal(10, await fixture.StockAsync(first));
        using var db = fixture.Factory.GetDatabase();
        Assert.Equal(StockReservationState.Released, (await db.StockReservations.SingleAsync()).State);
        await db.StockData.Where(x => x.UniqueId == second.ToString()).Set(x => x.Stock, 5).UpdateAsync();
        await checkout.PrepareAsync(order, requests, ids, default);
        Assert.Equal(2, ids.Count);
        Assert.Equal(7, await fixture.StockAsync(first));
        Assert.Equal(3, await fixture.StockAsync(second));
    }

    [Fact]
    public async Task RetryReusesAndRecoversHoldsWithoutExtendingTimeout()
    {
        using var fixture = new ReservationDatabase();
        var key = await fixture.SeedAsync(3);
        var order = Guid.NewGuid();
        var requests = Requests(order, (key, 3));
        var ids = new List<string>();
        await fixture.NewCheckout().PrepareAsync(order, requests, ids, default);
        var recovered = new List<string>();
        await fixture.NewCheckout().PrepareAsync(order, requests, recovered, default);
        Assert.Equal(ids, recovered);
        Assert.Equal(0, await fixture.StockAsync(key));
        using var db = fixture.Factory.GetDatabase();
        var row = await db.StockReservations.SingleAsync();
        Assert.Equal(order.ToString(), row.OrderId);
        Assert.NotNull(row.PaymentAttemptId);
        await Assert.ThrowsAsync<StockException>(() => fixture.NewCheckout().PrepareAsync(order, Requests(order, (key, 4)), ids, default));
        Assert.Equal(row.ExpiresUtc, (await db.StockReservations.SingleAsync()).ExpiresUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LatePaymentAndRetryFailWithoutAdditionalDeduction(bool sweep)
    {
        using var fixture = new ReservationDatabase();
        var key = await fixture.SeedAsync(10);
        var order = Guid.NewGuid();
        var requests = Requests(order, (key, 3));
        var ids = new List<string>();
        var checkout = fixture.NewCheckout();
        await checkout.PrepareAsync(order, requests, ids, default);
        await fixture.MakeDueAsync(ids.Single());
        if (sweep) await fixture.Service.ExpireDueAsync(100);
        await Assert.ThrowsAsync<StockException>(() => checkout.CompleteStockAsync(order, requests, ids, true, default));
        await Assert.ThrowsAsync<StockException>(() => checkout.PrepareAsync(order, requests, ids, default));
        Assert.False(await checkout.IsCompletedAsync(order, default));
        Assert.Equal(sweep ? 10 : 7, await fixture.StockAsync(key));
    }

    [Fact]
    public async Task PartialHoldDoesNotSkipUnreservedStockAndSurvivesToggleOff()
    {
        using var fixture = new ReservationDatabase();
        var first = await fixture.SeedAsync(10);
        var second = await fixture.SeedAsync(10);
        var order = Guid.NewGuid();
        var requests = Requests(order, (first, 5), (second, 2));
        var hold = await fixture.Service.ReserveAsync(requests[0] with { Quantity = 2 });
        var ids = new List<string> { hold.ReservationId! };
        var disabled = fixture.NewCheckout(Config(("Ekom:Reservations:Enabled", "false")));
        await disabled.PrepareAsync(order, requests, ids, default, createReservations: false);
        Assert.Single(ids);
        await disabled.CompleteStockAsync(order, requests, ids, true, default);
        Assert.Equal(5, await fixture.StockAsync(first));
        Assert.Equal(8, await fixture.StockAsync(second));
        Assert.Equal(StockReservationStatus.AlreadyConsumed, (await fixture.Service.ConsumeAsync(ids[0])).Status);
    }

    [Fact]
    public async Task FailedUncoveredDeductionRollsBackConsumptionAndAllDeductions()
    {
        using var fixture = new ReservationDatabase();
        var first = await fixture.SeedAsync(10);
        var second = await fixture.SeedAsync(0);
        var order = Guid.NewGuid();
        var requests = Requests(order, (first, 5), (second, 2));
        var hold = await fixture.Service.ReserveAsync(requests[0] with { Quantity = 2 });
        var ids = new[] { hold.ReservationId! };
        var checkout = fixture.NewCheckout();
        await Assert.ThrowsAsync<NotEnoughStockException>(() => checkout.CompleteStockAsync(order, requests, ids, true, default));
        Assert.Equal(8, await fixture.StockAsync(first));
        using var db = fixture.Factory.GetDatabase();
        Assert.Equal(StockReservationState.Active, (await db.StockReservations.SingleAsync()).State);
        Assert.Empty(await db.GetTable<CheckoutStockCompletionData>().ToListAsync());
    }

    [Theory]
    [InlineData("order")]
    [InlineData("key")]
    [InlineData("quantity")]
    [InlineData("store")]
    [InlineData("missing")]
    public async Task ForeignOrMismatchedReservationCannotSuppressDeduction(string mismatch)
    {
        using var fixture = new ReservationDatabase();
        var key = await fixture.SeedAsync(10);
        var order = Guid.NewGuid();
        var requests = Requests(order, (key, 3));
        var holdRequest = mismatch switch
        {
            "order" => requests[0] with { OrderId = Guid.NewGuid().ToString() },
            "store" => requests[0] with { StoreAlias = "other" },
            "quantity" => requests[0] with { Quantity = 4 },
            _ => requests[0],
        };
        var hold = await fixture.Service.ReserveAsync(holdRequest);
        if (mismatch == "key") requests = Requests(order, (Guid.NewGuid(), 3));
        await Assert.ThrowsAsync<StockException>(() => fixture.NewCheckout().CompleteStockAsync(order, requests,
            new[] { mismatch == "missing" ? "missing" : hold.ReservationId! }, true, default));
        using var db = fixture.Factory.GetDatabase();
        Assert.Equal(StockReservationState.Active, (await db.StockReservations.SingleAsync()).State);
    }

    [Fact]
    public async Task StockBufferIsEnforcedAtomicallyAcrossOrders()
    {
        using var fixture = new ReservationDatabase();
        var key = await fixture.SeedAsync(5);
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() =>
            fixture.NewService().ReserveAsync(new() { Key = key, Quantity = 1, MinimumRemainingStock = 2 }))));
        Assert.Equal(3, results.Count(x => x.Status == StockReservationStatus.Created));
        Assert.Equal(2, await fixture.StockAsync(key));
    }

    [Fact]
    public async Task DiscountAndExactCouponAreConsumedOnceWithPartialCoverage()
    {
        using var fixture = new ReservationDatabase();
        var key = Guid.NewGuid();
        await fixture.SeedDiscountAsync(key, null, 10);
        await fixture.SeedDiscountAsync(key, "Coupon", 10);
        var order = Guid.NewGuid();
        var requests = new[]
        {
            new StockReservationRequest { Key = key, IsDiscount = true, Quantity = 2, OrderId = order.ToString() },
            new StockReservationRequest { Key = key, IsDiscount = true, Coupon = "Coupon", Quantity = 2, OrderId = order.ToString() },
        };
        var hold = await fixture.Service.ReserveAsync(requests[0] with { Quantity = 1 });
        var checkout = fixture.NewCheckout();
        await checkout.CompleteStockAsync(order, requests, new[] { hold.ReservationId! }, false, default);
        await checkout.CompleteStockAsync(order, requests, new[] { hold.ReservationId! }, false, default);
        using var db = fixture.Factory.GetDatabase();
        Assert.All(await db.DiscountStockData.ToListAsync(), x => Assert.Equal(8, x.Stock));
    }

    [Fact]
    public async Task DisabledInventoryValidationStillConsumesHoldButSkipsUncoveredInventory()
    {
        using var fixture = new ReservationDatabase();
        var key = await fixture.SeedAsync(10);
        var order = Guid.NewGuid();
        var requests = Requests(order, (key, 5));
        var hold = await fixture.Service.ReserveAsync(requests[0] with { Quantity = 2 });
        await fixture.NewCheckout().CompleteStockAsync(order, requests, new[] { hold.ReservationId! }, false, default);
        Assert.Equal(8, await fixture.StockAsync(key));
        Assert.Equal(StockReservationStatus.AlreadyConsumed, (await fixture.Service.ReleaseAsync(hold.ReservationId!)).Status);
    }

    private static StockReservationRequest[] Requests(Guid order, params (Guid Key, decimal Quantity)[] items)
        => items.Select(x => new StockReservationRequest { Key = x.Key, Quantity = x.Quantity, OrderId = order.ToString() }).ToArray();

    private static Configuration Config(params (string Key, string? Value)[] settings)
        => new(new ConfigurationBuilder().AddInMemoryCollection(settings.ToDictionary(x => x.Key, x => x.Value)).Build());
}

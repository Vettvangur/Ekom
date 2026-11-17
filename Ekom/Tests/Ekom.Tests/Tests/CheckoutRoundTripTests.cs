using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using Xunit;
using ReservationDatabase = Ekom.Tests.Tests.StockReservationTests.ReservationDatabase;

namespace Ekom.Tests.Tests;

/// <summary>
/// Guards the number of database statements on the basket edit and stock completion paths.
/// </summary>
[Collection("Reservations")]
public sealed class CheckoutRoundTripTests
{
    [Fact]
    public async Task BasketEditReadsTheOperationRowOnceAfterClaimingIt()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var order = new OrderInfo(await f.ReadAsync());
        // The first edit creates the operation row.
        await (await f.Service.BeginEditAsync(order, "quantity")).DisposeAsync();

        List<string> statements;
        using (var trace = new StatementTrace(f.Database))
        {
            await (await f.Service.BeginEditAsync(order, "quantity")).DisposeAsync();
            statements = trace.Statements;
        }

        // The existence check before the claim, then one read shared by the reconciliation and attempt checks.
        Assert.Equal(2, statements.Count(x => IsSelect(x) && x.Contains("EkomCheckoutPaymentOperation", StringComparison.Ordinal)));
        using var db = f.Database.Factory.GetDatabase();
        Assert.Null((await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
    }

    [Fact]
    public async Task OrderSaveDoesNotReadTheStoredOrderJsonBack()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var repository = new OrderRepository(NullLogger<OrderRepository>.Instance, Configuration.Instance, f.Database.Factory, cache);
        var data = await f.ReadAsync();
        var revision = data.StatusRevision;
        data.CustomerName = "Changed";

        List<string> statements;
        using (var trace = new StatementTrace(f.Database))
        {
            await repository.UpdateOrderAsync(data);
            statements = trace.Statements;
        }

        var reads = statements.Where(x => IsSelect(x) && x.Contains("EkomOrders", StringComparison.Ordinal)).ToList();
        Assert.Single(reads);
        Assert.DoesNotContain("OrderInfo", reads[0], StringComparison.Ordinal);
        var saved = await f.ReadAsync();
        Assert.Equal("Changed", saved.CustomerName);
        // The previous state was still read: every save moves the revision on from the stored one.
        Assert.Equal(revision + 1, saved.StatusRevision);
    }

    [Fact]
    public async Task StockCompletionVerifiesAndConsumesAllHoldsTogether()
    {
        using var fixture = new ReservationDatabase();
        var orderId = Guid.NewGuid();
        var keys = new[] { await fixture.SeedAsync(10), await fixture.SeedAsync(10), await fixture.SeedAsync(10) };
        var requests = keys.Select(key => new StockReservationRequest { Key = key, Quantity = 2, OrderId = orderId.ToString() }).ToArray();
        var ids = new List<string>();
        var checkout = fixture.NewCheckout();
        await checkout.PrepareAsync(orderId, requests, ids, default);
        Assert.Equal(3, ids.Count);

        List<string> statements;
        using (var trace = new StatementTrace(fixture))
        {
            Assert.True(await checkout.CompleteStockAsync(orderId, requests, ids, true, default));
            statements = trace.Statements;
        }

        var reservationStatements = statements.Where(x => x.Contains("EkomStockReservation", StringComparison.Ordinal)).ToList();
        // The holds recovered for the order, then one read and one update for all three holds.
        Assert.Equal(2, reservationStatements.Count(IsSelect));
        Assert.Equal(1, reservationStatements.Count(x => x.StartsWith("UPDATE", StringComparison.Ordinal)));
        using var db = fixture.Factory.GetDatabase();
        Assert.Equal(3, await db.StockReservations.CountAsync(x => x.OrderId == orderId.ToString() && x.State == StockReservationState.Consumed));
        foreach (var key in keys)
            Assert.Equal(8, await fixture.StockAsync(key));
    }

    [Fact]
    public async Task StockCompletionFailsWhenAHoldChangedBeforeItWasConsumed()
    {
        using var fixture = new ReservationDatabase();
        var orderId = Guid.NewGuid();
        var keys = new[] { await fixture.SeedAsync(10), await fixture.SeedAsync(10) };
        var requests = keys.Select(key => new StockReservationRequest { Key = key, Quantity = 2, OrderId = orderId.ToString() }).ToArray();
        var ids = new List<string>();
        var checkout = fixture.NewCheckout();
        await checkout.PrepareAsync(orderId, requests, ids, default);
        using (var db = fixture.Factory.GetDatabase())
        {
            await db.StockReservations.Where(x => x.Id == ids[1])
                .Set(x => x.State, StockReservationState.Released).UpdateAsync();
        }

        await Assert.ThrowsAnyAsync<Exception>(() => checkout.CompleteStockAsync(orderId, requests, ids, true, default));

        using var read = fixture.Factory.GetDatabase();
        // Nothing was consumed: the first hold is still active.
        Assert.Equal(StockReservationState.Active, (await read.StockReservations.SingleAsync(x => x.Id == ids[0])).State);
        Assert.False(await checkout.IsCompletedAsync(orderId, default));
    }

    private static bool IsSelect(string sql) => sql.StartsWith("SELECT", StringComparison.Ordinal);

    private sealed class StatementTrace : IDisposable
    {
        private readonly Action<TraceInfo> _previous = DataConnection.DefaultOnTraceConnection;
        private readonly TraceLevel _level = DataConnection.TraceSwitch.Level;

        public List<string> Statements { get; } = new();

        public StatementTrace(ReservationDatabase database)
        {
            using var db = database.Factory.GetDatabase();
            var connectionString = db.ConnectionString;
            DataConnection.TraceSwitch.Level = TraceLevel.Info;
            DataConnection.DefaultOnTraceConnection = info =>
            {
                if (info.DataConnection.ConnectionString != connectionString || info.TraceInfoStep != TraceInfoStep.BeforeExecute) return;
                if (info.Command?.CommandText is string sql)
                    lock (Statements) Statements.Add(sql.TrimStart());
            };
        }

        public void Dispose()
        {
            DataConnection.DefaultOnTraceConnection = _previous;
            DataConnection.TraceSwitch.Level = _level;
        }
    }
}

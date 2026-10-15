using Ekom.Models;
using Ekom.Repositories;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using Xunit;
using Fixture = Ekom.Tests.Tests.CheckoutPaymentAttemptTests.Fixture;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class OrderPersistenceCancellationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledReadOnlySaveCheckDoesNotIntroduceOrRemoveAnOwnershipFence(bool alreadyPreserved)
    {
        using var f = new Fixture();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var data = await f.ReadAsync();
        await using var operation = await f.Service.BeginCheckoutAsync(new OrderInfo(data));
        using var activation = operation.Enter();
        operation.PreserveOwnership = alreadyPreserved;
        using var cancelled = new CancellationTokenSource();
        using (var fault = new CancelSql(f, cancelled, sql => sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Repository(f, cache).UpdateOrderAsync(data, cancelled.Token));
            Assert.True(fault.Triggered);
        }

        Assert.Equal(alreadyPreserved, operation.PreserveOwnership);
        await operation.DisposeAsync();
        using var db = f.Database.Factory.GetDatabase();
        var owner = (await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner;
        Assert.Equal(alreadyPreserved ? operation.Owner : null, owner);
    }

    [Fact]
    public async Task CancelledSaveAfterWriteStartsStillPreservesUncertainOwnership()
    {
        using var f = new Fixture();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var data = await f.ReadAsync();
        await using var operation = await f.Service.BeginCheckoutAsync(new OrderInfo(data));
        using var activation = operation.Enter();
        using var cancelled = new CancellationTokenSource();
        using (var fault = new CancelSql(f, cancelled, sql => sql.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
            && sql.Contains("EkomOrders", StringComparison.Ordinal)))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Repository(f, cache).UpdateOrderAsync(data, cancelled.Token));
            Assert.True(fault.Triggered);
        }

        Assert.True(operation.PreserveOwnership);
        await operation.DisposeAsync();
        using var db = f.Database.Factory.GetDatabase();
        Assert.Equal(operation.Owner, (await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
    }

    private static OrderRepository Repository(Fixture f, IMemoryCache cache)
        => new(NullLogger<OrderRepository>.Instance, new Configuration(new ConfigurationBuilder().Build()),
            f.Database.Factory, cache, f.Service);

    private sealed class CancelSql : IDisposable
    {
        private readonly Action<TraceInfo> _previous = DataConnection.DefaultOnTraceConnection;
        private readonly TraceLevel _level = DataConnection.TraceSwitch.Level;
        public bool Triggered { get; private set; }

        public CancelSql(Fixture f, CancellationTokenSource cancelled, Func<string, bool> matches)
        {
            using var db = f.Database.Factory.GetDatabase();
            var connectionString = db.ConnectionString;
            DataConnection.DefaultOnTraceConnection = info =>
            {
                if (!Triggered && info.DataConnection.ConnectionString == connectionString &&
                    info.TraceInfoStep == TraceInfoStep.BeforeExecute && info.Command?.CommandText is string sql && matches(sql))
                {
                    Triggered = true;
                    cancelled.Cancel();
                    throw new OperationCanceledException(cancelled.Token);
                }
            };
            DataConnection.TraceSwitch.Level = TraceLevel.Info;
        }

        public void Dispose()
        {
            DataConnection.DefaultOnTraceConnection = _previous;
            DataConnection.TraceSwitch.Level = _level;
        }
    }
}

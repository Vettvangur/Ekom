using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using Ekom.Utilities;
using LinqToDB;
using LinqToDB.Mapping;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Fixture = Ekom.Tests.Tests.CheckoutPaymentAttemptTests.Fixture;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class OrderStatusPersistenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StatusAndDurableRecordCommitTogether(bool guarded)
    {
        using var fixture = new Fixture();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        await using var db = fixture.Database.Factory.GetDatabase();
        await db.CreateTableAsync<StatusRecord>();
        var handler = new RecordingHandler();
        var repository = new OrderRepository(NullLogger<OrderRepository>.Instance,
            new Configuration(new ConfigurationBuilder().Build()), fixture.Database.Factory, cache,
            guarded ? fixture.Service : null, [handler]);
        var order = await fixture.ReadAsync();
        var previous = order.OrderStatusCol;
        order.OrderStatus = OrderStatus.OfflinePayment;
        await repository.UpdateOrderAsync(order);
        var record = await db.GetTable<StatusRecord>().SingleAsync();
        Assert.Equal(previous, record.Previous);
        Assert.Equal(OrderStatus.OfflinePayment.ToString(), record.Current);
        Assert.Equal(OrderStatus.OfflinePayment, (await repository.GetOrderAsync(order.UniqueId))!.OrderStatus);
        // A metadata-only save is not another status transition.
        await repository.UpdateOrderAsync(order);
        Assert.Equal(1, await db.GetTable<StatusRecord>().CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedDurableRecordRollsBackStatusAndRecord(bool guarded)
    {
        using var fixture = new Fixture();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        await using var db = fixture.Database.Factory.GetDatabase();
        await db.CreateTableAsync<StatusRecord>();
        var repository = new OrderRepository(NullLogger<OrderRepository>.Instance,
            new Configuration(new ConfigurationBuilder().Build()), fixture.Database.Factory, cache,
            guarded ? fixture.Service : null, [new RecordingHandler { Fail = true }]);
        var order = await fixture.ReadAsync();
        var original = order.OrderStatus;
        order.OrderStatus = OrderStatus.OfflinePayment;
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.UpdateOrderAsync(order));
        Assert.Equal(original, (await repository.GetOrderAsync(order.UniqueId))!.OrderStatus);
        Assert.Empty(await db.GetTable<StatusRecord>().ToListAsync());
    }

    [Fact]
    public async Task InsertCapturesInitialStatusAndRollsBackOnHandlerFailure()
    {
        using var fixture = new Fixture();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        await using var db = fixture.Database.Factory.GetDatabase();
        await db.CreateTableAsync<StatusRecord>();
        var handler = new RecordingHandler();
        var repository = new OrderRepository(NullLogger<OrderRepository>.Instance,
            new Configuration(new ConfigurationBuilder().Build()), fixture.Database.Factory, cache,
            statusPersistenceHandlers: [handler]);
        var order = await fixture.ReadAsync();
        order.UniqueId = Guid.NewGuid();
        order.OrderStatus = OrderStatus.OfflinePayment;
        await repository.InsertOrderAsync(order);
        Assert.Null((await db.GetTable<StatusRecord>().SingleAsync()).Previous);
        Assert.True(order.ReferenceId > 0);
        handler.Fail = true;
        order.UniqueId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.InsertOrderAsync(order));
        Assert.Null(await repository.GetOrderAsync(order.UniqueId));
        Assert.Equal(1, await db.GetTable<StatusRecord>().CountAsync());
    }

    [Fact]
    public async Task PaymentReleaseCapturesItsDirectStatusWrite()
    {
        using var fixture = new Fixture();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        await using var db = fixture.Database.Factory.GetDatabase();
        await db.CreateTableAsync<StatusRecord>();
        var attempt = await fixture.SubmittedAsync();
        var service = new CheckoutPaymentAttemptService(fixture.Database.Factory, fixture.Database.Service,
            new ActivityLogRepository(NullLogger<ActivityLogRepository>.Instance, fixture.Database.Factory),
            NullLogger<CheckoutPaymentAttemptService>.Instance, cache,
            statusPersistenceHandlers: [new RecordingHandler()]);
        await service.ReleaseAsync(fixture.OrderId, attempt.AttemptId, "test release");
        var record = await db.GetTable<StatusRecord>().SingleAsync();
        Assert.Equal("Pending", record.Previous);
        Assert.Equal("Incomplete", record.Current);
        Assert.Equal(OrderStatus.Incomplete, (await fixture.ReadAsync()).OrderStatus);
    }

    [Table("TestDurableStatus")]
    private sealed class StatusRecord
    {
        [PrimaryKey] public Guid Id { get; set; }
        [Column, Nullable] public string? Previous { get; set; }
        [Column] public string Current { get; set; } = string.Empty;
    }

    private sealed class RecordingHandler : IOrderStatusPersistenceHandler
    {
        public bool Fail { get; set; }
        public async Task PersistAsync(DbContext database, OrderData order, string? previousStatus, CancellationToken cancellationToken = default)
        {
            Assert.NotNull(database.Transaction);
            await database.InsertAsync(new StatusRecord { Id = Guid.NewGuid(), Previous = previousStatus, Current = order.OrderStatusCol },
                token: cancellationToken);
            if (Fail) throw new InvalidOperationException("Simulated capture failure.");
        }
    }
}

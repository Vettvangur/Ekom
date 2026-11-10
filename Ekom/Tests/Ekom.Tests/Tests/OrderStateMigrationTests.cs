using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Models.Orders;
using Ekom.Repositories;
using Ekom.Services;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class OrderStateMigrationTests
{
    [Theory]
    [InlineData(true, OrderStatus.Closed)]
    [InlineData(false, OrderStatus.Open)]
    public async Task PreviewMatchesPersistedMappingAndMigrationIsIdempotent(bool autoClose, OrderStatus dispatchedState)
    {
        using var fixture = new Fixture(autoClose);
        var expected = new[]
        {
            ("Incomplete", OrderStatus.Draft, PaymentStatus.Pending, FulfillmentStatus.Unfulfilled),
            ("WaitingForPayment", OrderStatus.Draft, PaymentStatus.Pending, FulfillmentStatus.Unfulfilled),
            ("Wishlist", OrderStatus.Draft, PaymentStatus.Pending, FulfillmentStatus.Unfulfilled),
            ("Pending", OrderStatus.Open, PaymentStatus.Pending, FulfillmentStatus.Unfulfilled),
            ("OfflinePayment", OrderStatus.Open, PaymentStatus.Paid, FulfillmentStatus.Unfulfilled),
            ("PaymentFailed", OrderStatus.Draft, PaymentStatus.Failed, FulfillmentStatus.Unfulfilled),
            ("ReadyForDispatch", OrderStatus.Open, PaymentStatus.Paid, FulfillmentStatus.Unfulfilled),
            ("ReadyForDispatchWhenStockArrives", OrderStatus.Open, PaymentStatus.Paid, FulfillmentStatus.Unfulfilled),
            ("ReadyForPickup", OrderStatus.Open, PaymentStatus.Paid, FulfillmentStatus.Packed),
            ("Dispatched", dispatchedState, PaymentStatus.Paid, FulfillmentStatus.Fulfilled),
            ("Closed", OrderStatus.Closed, PaymentStatus.Paid, FulfillmentStatus.Fulfilled),
            ("Cancelled", OrderStatus.Cancelled, PaymentStatus.Pending, FulfillmentStatus.Unfulfilled),
        };
        foreach (var item in expected) fixture.Add(item.Item1);
        foreach (var legacy in new[] { "Returned", "Unknown" }) fixture.Add(legacy);
        var locked = fixture.Add("WaitingForPayment");
        using var db = fixture.Factory.GetDatabase();
        db.Insert(new CheckoutPaymentOperationData { OrderId = locked, Owner = "checkout-in-progress" });
        var before = db.OrderData.OrderBy(x => x.ReferenceId).ToArray();
        var preview = await fixture.Service.PreviewAsync();
        Assert.Equal(0, db.OrderData.Count(x => x.StatusMappingVersion != 0));
        Assert.Equal(expected.Length, preview.Orders.Count(x => x.Eligible));
        var applied = await fixture.Service.MigrateAsync();
        Assert.Equal(expected.Length, applied.Migrated);
        Assert.Equal(preview.Orders.Select(x => (x.OrderId, x.Status.OrderStatus, x.Status.Revision)),
            applied.Orders.Select(x => (x.OrderId, x.Status.OrderStatus, x.Status.Revision)));
        var after = db.OrderData.OrderBy(x => x.ReferenceId).ToArray();
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Item2, after[i].Status.OrderStatus);
            Assert.Equal(expected[i].Item3, after[i].Status.PaymentStatus);
            Assert.Equal(expected[i].Item4, after[i].Status.FulfillmentStatus);
            Assert.Equal(expected[i].Item1, after[i].OriginalOrderStatusCol);
            Assert.Equal(1, after[i].StatusRevision);
        }
        Assert.Equal(before.Select(ProtectedFields), after.Select(ProtectedFields));
        Assert.All(after.Skip(expected.Length), row => Assert.Equal(0, row.StatusMappingVersion));
        Assert.Equal(0, (await fixture.Service.MigrateAsync()).Migrated);
        Assert.Empty(db.GetTable<OrderActivityLog>());
        fixture.Handler.VerifyNoOtherCalls();
        fixture.Stock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Pending")]
    [InlineData("WaitingForPayment")]
    public async Task SubmittedPaymentAttemptIsNotMigratedEvenWithoutAnOperationOwner(string legacyStatus)
    {
        using var fixture = new Fixture();
        var id = fixture.Add(legacyStatus);
        var attemptId = Guid.NewGuid();
        using var db = fixture.Factory.GetDatabase();
        db.Insert(new CheckoutPaymentAttemptData { OrderId = id, AttemptId = attemptId, State = CheckoutPaymentAttemptState.Submitted, CreatedUtc = DateTime.UtcNow });
        db.Insert(new CheckoutPaymentOperationData { OrderId = id, ActiveAttemptId = attemptId });
        Assert.False((await fixture.Service.PreviewAsync()).Orders.Single().Eligible);
        Assert.Equal(0, (await fixture.Service.MigrateAsync()).Migrated);
    }

    [Theory]
    [InlineData("Paid", "Fulfilled", true, false)]
    [InlineData("Authorized", "Packed", false, false)]
    [InlineData("Pending", "Unfulfilled", true, true)]
    [InlineData("Invalid", "Unfulfilled", false, true)]
    [InlineData("Paid", "Invalid", false, true)]
    public async Task CancelledPreservesEvidenceAndSkipsConflicts(string payment, string fulfillment, bool paid, bool review)
    {
        using var fixture = new Fixture();
        var id = fixture.Add("Cancelled");
        using var db = fixture.Factory.GetDatabase();
        db.OrderData.Where(x => x.UniqueId == id)
            .Set(x => x.PaymentStateCol, payment).Set(x => x.FulfillmentStateCol, fulfillment)
            .Set(x => x.PaidDate, paid ? DateTime.UtcNow : (DateTime?)null).Update();
        var preview = (await fixture.Service.PreviewAsync()).Orders.Single();
        Assert.Equal(review, preview.Status.RequiresReview);
        Assert.Equal(!review, preview.Eligible);
        Assert.Equal(review ? 0 : 1, (await fixture.Service.MigrateAsync()).Migrated);
        var saved = db.OrderData.Single();
        Assert.Equal(payment, saved.PaymentStateCol);
        Assert.Equal(fulfillment, saved.FulfillmentStateCol);
        Assert.Equal(review ? 0 : 1, saved.StatusMappingVersion);
    }

    [Fact]
    public async Task CancelledPreservesPaidDateAndPartialFulfillmentQuantities()
    {
        using var fixture = new Fixture();
        var id = fixture.Add("Cancelled");
        using var db = fixture.Factory.GetDatabase();
        var paidDate = DateTime.UtcNow;
        var quantities = System.Text.Json.JsonSerializer.Serialize(new Dictionary<Guid, decimal> { [Guid.NewGuid()] = 1 });
        db.OrderData.Where(x => x.UniqueId == id).Set(x => x.PaidDate, paidDate)
            .Set(x => x.FulfillmentStateCol, "PartiallyFulfilled")
            .Set(x => x.FulfilledQuantitiesJson, quantities).Update();
        Assert.Equal(1, (await fixture.Service.MigrateAsync()).Migrated);
        var saved = db.OrderData.Single();
        Assert.Equal("Cancelled", saved.OrderStateCol);
        Assert.Equal("Paid", saved.PaymentStateCol);
        Assert.Equal("PartiallyFulfilled", saved.FulfillmentStateCol);
        Assert.Equal(quantities, saved.FulfilledQuantitiesJson);
        Assert.Equal(paidDate, saved.PaidDate);
        Assert.Empty(db.GetTable<OrderActivityLog>());
        fixture.Handler.VerifyNoOtherCalls();
        fixture.Stock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CancelledWithActiveCheckoutOwnerIsNotMigrated()
    {
        using var fixture = new Fixture();
        var id = fixture.Add("Cancelled");
        using var db = fixture.Factory.GetDatabase();
        db.Insert(new CheckoutPaymentOperationData { OrderId = id, Owner = "active-checkout" });
        Assert.False((await fixture.Service.PreviewAsync()).Orders.Single().Eligible);
        Assert.Equal(0, (await fixture.Service.MigrateAsync()).Migrated);
    }

    [Fact]
    public async Task CancelledWithUnresolvedFulfilledQuantitiesIsNotMigrated()
    {
        using var fixture = new Fixture();
        var id = fixture.Add("Cancelled");
        using var db = fixture.Factory.GetDatabase();
        var quantities = System.Text.Json.JsonSerializer.Serialize(new Dictionary<Guid, decimal> { [Guid.NewGuid()] = 1 });
        db.OrderData.Where(x => x.UniqueId == id).Set(x => x.FulfilledQuantitiesJson, quantities).Update();
        Assert.True((await fixture.Service.PreviewAsync()).Orders.Single().Status.RequiresReview);
        Assert.Equal(0, (await fixture.Service.MigrateAsync()).Migrated);
        Assert.Equal(quantities, db.OrderData.Single().FulfilledQuantitiesJson);
    }

    [Fact]
    public async Task PagingDoesNotSkipOrdersAndRespectsStoreAccess()
    {
        using var fixture = new Fixture();
        for (var i = 0; i < 103; i++) fixture.Add("Pending");
        fixture.Add("Pending", "other-store");
        var first = await fixture.Service.MigrateAsync();
        Assert.True(first.HasMore);
        Assert.Equal(100, first.Migrated);
        var second = await fixture.Service.MigrateAsync(first.NextReferenceId);
        Assert.False(second.HasMore);
        Assert.Equal(3, second.Migrated);
        using var db = fixture.Factory.GetDatabase();
        Assert.Equal(0, db.OrderData.Single(x => x.StoreAlias == "other-store").StatusMappingVersion);
    }

    [Theory]
    [InlineData(true, OrderStatus.Closed)]
    [InlineData(false, OrderStatus.Open)]
    public async Task FulfillingSavedOrderClosesOnlyWhenConfigured(bool autoClose, OrderStatus expected)
    {
        using var basket = new CheckoutPaymentWindowFixture(reservations: false);
        using var source = basket.Database.Factory.GetDatabase();
        using var fixture = new Fixture(autoClose);
        var id = fixture.Add("ReadyForDispatch");
        using var db = fixture.Factory.GetDatabase();
        var payload = source.OrderData.Single(x => x.UniqueId == basket.OrderId).OrderInfo;
        db.OrderData.Where(x => x.UniqueId == id).Set(x => x.OrderInfo, payload).Update();
        await fixture.Service.MigrateAsync();
        var notFulfilled = await fixture.Service.ChangeAsync(id, new(1, "Fulfill", new Dictionary<Guid, decimal> { [basket.LineKey] = 0 }));
        Assert.Equal(OrderStatus.Open, notFulfilled.OrderStatus);
        var fulfilled = await fixture.Service.ChangeAsync(id, new(2, "Fulfill"));
        Assert.Equal(expected, fulfilled.OrderStatus);
        Assert.Equal(FulfillmentStatus.Fulfilled, fulfilled.FulfillmentStatus);
        Assert.Equal(1, fulfilled.FulfilledQuantities![basket.LineKey]);
        var saved = db.OrderData.Single(x => x.UniqueId == id);
        Assert.Equal(payload, saved.OrderInfo);
        Assert.Equal(123.45m, saved.TotalAmount);
        Assert.Equal(expected, saved.Status.OrderStatus);
        fixture.Stock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReopenPersistsSuppressionAndExplicitCloseChecksRevision()
    {
        using var fixture = new Fixture();
        var id = fixture.Add("Dispatched");
        await fixture.Service.MigrateAsync();
        var reopened = await fixture.Service.ChangeAsync(id, new(1, "Reopen"));
        Assert.Equal(OrderStatus.Open, reopened.OrderStatus);
        Assert.True(reopened.AutoCloseSuppressed);
        using var db = fixture.Factory.GetDatabase();
        var saved = db.OrderData.Single(x => x.UniqueId == id);
        OrderStateRules.AutoClose(saved, true);
        Assert.Equal(OrderStatus.Open, saved.Status.OrderStatus);
        await Assert.ThrowsAsync<EkomHttpException>(() => fixture.Service.ChangeAsync(id, new(1, "Close")));
        var closed = await fixture.Service.ChangeAsync(id, new(2, "Close"));
        Assert.Equal(OrderStatus.Closed, closed.OrderStatus);
        Assert.Equal(3, closed.Revision);
        Assert.Equal(2, db.GetTable<OrderActivityLog>().Count());
    }

    // Optional local rehearsal: caller supplies a disposable SQLite BACKUP, never a running site database.
    [Fact]
    public async Task RehearseCopiedSampleDatabaseWhenRequested()
    {
        var path = Environment.GetEnvironmentVariable("EKOM_STATUS_REHEARSAL_DB");
        if (string.IsNullOrEmpty(path)) return;
        Assert.StartsWith("/tmp/ekom-status-rehearsal-", path);
        using var fixture = new Fixture(path: path);
        using var db = fixture.Factory.GetDatabase();
        var before = db.OrderData.OrderBy(x => x.ReferenceId).ToArray();
        var report = new List<object>();
        var cursor = 0;
        do
        {
            var preview = await fixture.Service.PreviewAsync(cursor);
            var result = await fixture.Service.MigrateAsync(cursor);
            Assert.Equal(preview.Orders.Select(x => (x.OrderId, x.Status.OrderStatus, x.Status.PaymentStatus, x.Status.FulfillmentStatus, x.Eligible)),
                result.Orders.Select(x => (x.OrderId, x.Status.OrderStatus, x.Status.PaymentStatus, x.Status.FulfillmentStatus, x.Eligible)));
            report.AddRange(result.Orders.Select(x => new { x.ReferenceId, x.Status.LegacyStatus, Order = x.Status.OrderStatus?.ToString(), Payment = x.Status.PaymentStatus?.ToString(), Fulfillment = x.Status.FulfillmentStatus?.ToString(), x.Eligible }));
            cursor = result.NextReferenceId;
            if (!result.HasMore) break;
        } while (true);
        Assert.Equal(before.Select(ProtectedFields), db.OrderData.OrderBy(x => x.ReferenceId).ToArray().Select(ProtectedFields));
        Assert.Equal(0, (await fixture.Service.MigrateAsync()).Migrated);
        fixture.Handler.VerifyNoOtherCalls();
        fixture.Stock.VerifyNoOtherCalls();
        await File.WriteAllTextAsync(path + ".report.json", System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private static object ProtectedFields(OrderData row) => new { row.UniqueId, row.OrderStatusCol, row.OrderInfo, row.TotalAmount, row.Currency, row.PaidDate, row.CreateDate, row.UpdateDate };

    private sealed class Fixture : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        public DatabaseFactory Factory { get; }
        public OrderStateService Service { get; }
        public Mock<IOrderStatusPersistenceHandler> Handler { get; } = new(MockBehavior.Strict);
        public Mock<IStockReservationService> Stock { get; } = new(MockBehavior.Strict);

        public Fixture(bool autoClose = true, string? path = null)
        {
            var connectionString = path == null ? $"Data Source=status-migration-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False" : $"Data Source={path};Pooling=False";
            _connection = new SqliteConnection(connectionString);
            _connection.Open();
            var root = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoDbDSN"] = connectionString,
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
                ["Ekom:AutoClosePaidAndFulfilledOrders"] = autoClose.ToString(),
            }).Build();
            Factory = new DatabaseFactory(root, Mock.Of<IHostEnvironment>(x => x.ContentRootPath == AppContext.BaseDirectory));
            if (path == null) new DatabaseService(Factory, NullLogger<DatabaseService>.Instance, new StockReservationReadiness()).CreateTables();
            var access = new Mock<IManagerAccessService>();
            access.Setup(x => x.CanAccessManager()).Returns(true);
            access.Setup(x => x.CanAccessStore(It.IsAny<string>())).Returns<string>(x => x is "main" or "Store");
            access.Setup(x => x.GetAllowedStoreAliases()).Returns(new[] { "main", "Store" });
            var attempts = new CheckoutPaymentAttemptService(Factory, Stock.Object, new ActivityLogRepository(NullLogger<ActivityLogRepository>.Instance, Factory), NullLogger<CheckoutPaymentAttemptService>.Instance, _cache);
            Handler.Setup(x => x.PersistAsync(It.IsAny<DbContext>(), It.IsAny<OrderData>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Service = new(Factory, new Configuration(root), access.Object, _cache, attempts, new HttpContextAccessor(), new[] { Handler.Object });
        }

        public Guid Add(string status, string store = "main")
        {
            var id = Guid.NewGuid();
            using var db = Factory.GetDatabase();
            db.Insert(new OrderData { UniqueId = id, StoreAlias = store, OrderStatusCol = status, OrderInfo = "{}", TotalAmount = 123.45m, Currency = "ISK" });
            return id;
        }

        public void Dispose() { _cache.Dispose(); _connection.Dispose(); }
    }
}

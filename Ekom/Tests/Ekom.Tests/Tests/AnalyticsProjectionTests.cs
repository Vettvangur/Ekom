using Ekom.Analytics;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class AnalyticsProjectionTests
{
    [Fact]
    public async Task SchemaIsIdempotentAndPreservesHeldLease()
    {
        using var database = new AnalyticsDatabase();
        var schema = database.CreateSchema();
        Assert.True(await schema.EnsureReadyAsync());
        Guid owner = Guid.NewGuid();
        using (var db = database.Factory.GetDatabase())
        {
            db.GetTable<AnalyticsLeaseData>().Where(x => x.Id == 1)
                .Set(x => x.Owner, owner).Set(x => x.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(5)).Update();
        }
        Assert.True(await schema.EnsureReadyAsync());
        Assert.True(await database.CreateSchema().EnsureReadyAsync());
        using var verification = database.Factory.GetDatabase();
        Assert.Equal(owner, verification.GetTable<AnalyticsLeaseData>().Single().Owner);
        Assert.Empty(verification.GetTable<AnalyticsOrderData>());
        Assert.Empty(verification.GetTable<AnalyticsOrderLineData>());
        Assert.Empty(verification.GetTable<AnalyticsPromotionData>());
        Assert.Empty(verification.GetTable<AnalyticsJobData>());
    }

    [Fact]
    public async Task DisabledAndInvalidConfigurationDoNotInitializeSchema()
    {
        using var database = new AnalyticsDatabase();
        database.Options.Value.Enabled = false;
        Assert.False(await database.CreateSchema().EnsureReadyAsync());
        Assert.False((await database.CreateService().RefreshAsync(Guid.NewGuid())).Success);
        database.Options.Value.Enabled = true;
        database.Options.Value.BatchSize = 0;
        Assert.False(await database.CreateSchema().EnsureReadyAsync());
        Assert.False((await database.CreateService().RefreshAsync(Guid.NewGuid())).Success);
        using var connection = database.Factory.GetDbConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name LIKE 'EkomAnalytics%'";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrIncompleteOrderRemovesOnlyItsAnalytics(bool incomplete)
    {
        using var database = new AnalyticsDatabase();
        Assert.True(await database.CreateSchema().EnsureReadyAsync());
        Guid orderId = incomplete ? database.AddOrder("Incomplete", "not-json") : Guid.NewGuid();
        Guid otherId = Guid.NewGuid();
        database.SeedProjection(orderId);
        database.SeedProjection(otherId);

        AnalyticsRefreshResult result = await database.CreateService().RefreshAsync(orderId);

        Assert.True(result.Success);
        Assert.True(result.Removed);
        using var db = database.Factory.GetDatabase();
        Assert.Equal(otherId, db.GetTable<AnalyticsOrderData>().Single().OrderId);
        Assert.Equal(otherId, db.GetTable<AnalyticsOrderLineData>().Single().OrderId);
        Assert.Equal(otherId, db.GetTable<AnalyticsPromotionData>().Single().OrderId);
        Assert.Equal(incomplete ? 1 : 0, db.GetTable<OrderData>().Count());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unknown")]
    [InlineData("Property:")]
    public async Task InvalidIdentitySelectorDoesNotAccessStorage(string selector)
    {
        var options = Options.Create(new AnalyticsOptions { Enabled = true, CustomerIdentifier = selector });
        // Deliberately absent storage dependencies prove validation happens before accessing them.
        var schema = new AnalyticsSchema(null!, options, NullLogger<AnalyticsSchema>.Instance);
        var service = new AnalyticsProjectionService(null!, schema,
            new AnalyticsSnapshotMapper(new ConfiguredAnalyticsCustomerIdentityResolver(options), options),
            options, null!, NullLogger<AnalyticsProjectionService>.Instance);
        Assert.False(await schema.EnsureReadyAsync());
        var result = await service.RefreshAsync(Guid.NewGuid());
        Assert.False(result.Success);
        Assert.Equal("Analytics configuration is invalid.", result.Error);
    }

    [Fact]
    public async Task ExistingNotNullLegacyColumnWithoutSqlDefaultReceivesOneOnRefresh()
    {
        using var database = new AnalyticsDatabase();
        using (var db = database.Factory.GetDatabase())
        {
            // Represents the deployed table before a fresh schema service sees it.
            db.CreateTable<AnalyticsOrderData>();
            Assert.Equal(1, db.Execute<int>("SELECT [notnull] FROM pragma_table_info('EkomAnalyticsOrders') WHERE name = 'CustomerIdentityPolicyVersion'"));
            Assert.Null(db.Execute<string?>("SELECT dflt_value FROM pragma_table_info('EkomAnalyticsOrders') WHERE name = 'CustomerIdentityPolicyVersion'"));
        }
        Guid orderId = database.AddOrder("ReadyForDispatch", Snapshot(100m));
        Assert.True((await database.CreateService().RefreshAsync(orderId)).Success);
        Assert.True((await database.CreateService().RefreshAsync(orderId)).Success);
        using var verification = database.Factory.GetDatabase();
        Assert.Equal(1, verification.Execute<int>("SELECT CustomerIdentityPolicyVersion FROM EkomAnalyticsOrders"));
        Assert.Equal(1, verification.GetTable<AnalyticsOrderData>().Single().ProjectionVersion);
    }

    [Fact]
    public async Task RefreshReplacesAllRowsIdempotentlyFromSavedSnapshot()
    {
        using var database = new AnalyticsDatabase();
        Assert.True(await database.CreateSchema().EnsureReadyAsync());
        string snapshot = Snapshot(100m);
        Guid orderId = database.AddOrder("ReadyForDispatch", snapshot);
        database.SeedProjection(orderId);
        var service = database.CreateService();

        Assert.True((await service.RefreshAsync(orderId)).Success);
        Assert.True((await service.RefreshAsync(orderId)).Success);

        using var db = database.Factory.GetDatabase();
        Assert.Equal(100m, db.GetTable<AnalyticsOrderData>().Single().GrandTotal);
        Assert.Equal(100m, db.GetTable<AnalyticsOrderLineData>().Single().TotalWithVat);
        Assert.Equal("NEW", db.GetTable<AnalyticsPromotionData>().Single().CouponCode);
        Assert.Equal(snapshot, db.GetTable<OrderData>().Single().OrderInfo);
        Assert.Empty(db.GetTable<OrderActivityLog>());
    }

    [Fact]
    public async Task InsertFailureRollsBackHeaderLinesAndPromotions()
    {
        using var database = new AnalyticsDatabase();
        Assert.True(await database.CreateSchema().EnsureReadyAsync());
        Guid orderId = database.AddOrder("ReadyForDispatch", Snapshot(100m));
        database.SeedProjection(orderId);
        using (var db = database.Factory.GetDatabase())
        {
            db.Execute("CREATE TRIGGER RejectAnalyticsLine BEFORE INSERT ON EkomAnalyticsOrderLines BEGIN SELECT RAISE(ABORT, 'test failure'); END");
        }

        Assert.False((await database.CreateService().RefreshAsync(orderId)).Success);

        using var verification = database.Factory.GetDatabase();
        Assert.Equal(42m, verification.GetTable<AnalyticsOrderData>().Single().GrandTotal);
        Assert.Single(verification.GetTable<AnalyticsOrderLineData>());
        Assert.Equal("OLD", verification.GetTable<AnalyticsPromotionData>().Single().CouponCode);
    }

    [Fact]
    public async Task ConcurrentServiceInstancesDoNotDuplicateProjectionRows()
    {
        using var database = new AnalyticsDatabase();
        Assert.True(await database.CreateSchema().EnsureReadyAsync());
        Guid orderId = database.AddOrder("ReadyForDispatch", Snapshot(100m));

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => database.CreateService().RefreshAsync(orderId)));

        Assert.All(results, result => Assert.True(result.Success));
        using var db = database.Factory.GetDatabase();
        Assert.Single(db.GetTable<AnalyticsOrderData>());
        Assert.Single(db.GetTable<AnalyticsOrderLineData>());
        Assert.Single(db.GetTable<AnalyticsPromotionData>());
    }

    [Fact]
    public async Task MappingFailurePreservesPreviousProjectionAndLogsSafeActivity()
    {
        using var database = new AnalyticsDatabase();
        Assert.True(await database.CreateSchema().EnsureReadyAsync());
        Guid orderId = database.AddOrder("ReadyForDispatch", "invalid secret@example.com");
        database.SeedProjection(orderId);

        AnalyticsRefreshResult result = await database.CreateService().RefreshAsync(orderId);

        Assert.False(result.Success);
        Assert.DoesNotContain("secret", result.Error!, StringComparison.Ordinal);
        using var db = database.Factory.GetDatabase();
        Assert.Equal(42m, db.GetTable<AnalyticsOrderData>().Single().GrandTotal);
        Assert.Single(db.GetTable<AnalyticsOrderLineData>());
        Assert.Single(db.GetTable<AnalyticsPromotionData>());
        var activity = db.GetTable<OrderActivityLog>().Single();
        Assert.Equal(orderId, activity.Key);
        Assert.Equal("Analytics", activity.UserName);
        Assert.DoesNotContain("secret", activity.Log, StringComparison.Ordinal);
        Assert.Equal("invalid secret@example.com", db.GetTable<OrderData>().Single().OrderInfo);
    }

    [Fact]
    public async Task ActivityLoggingFailureDoesNotEscapeRefresh()
    {
        using var database = new AnalyticsDatabase();
        Assert.True(await database.CreateSchema().EnsureReadyAsync());
        Guid orderId = database.AddOrder("ReadyForDispatch", "invalid-json");
        using (var db = database.Factory.GetDatabase()) db.DropTable<OrderActivityLog>();
        Assert.False((await database.CreateService().RefreshAsync(orderId)).Success);
    }

    [Fact]
    public async Task CancellationPropagatesWithoutRemovingProjection()
    {
        using var database = new AnalyticsDatabase();
        Assert.True(await database.CreateSchema().EnsureReadyAsync());
        Guid orderId = Guid.NewGuid();
        database.SeedProjection(orderId);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => database.CreateService().RefreshAsync(orderId, cancellation.Token));
        using var db = database.Factory.GetDatabase();
        Assert.Single(db.GetTable<AnalyticsOrderData>());
    }

    private static string Snapshot(decimal amount) => JsonSerializer.Serialize(new
    {
        GrandTotal = new { Value = amount },
        GrandTotalWithOutVat = new { Value = amount },
        ChargedAmount = new { Value = amount },
        Coupon = "NEW",
        OrderLines = new[]
        {
            new
            {
                Key = Guid.NewGuid(),
                Quantity = 1,
                Amount = new { Value = amount, WithoutVat = new { Value = amount } },
            },
        },
    });

    private sealed class AnalyticsDatabase : IDisposable
    {
        private readonly SqliteConnection _connection;

        public AnalyticsDatabase()
        {
            string connectionString = $"Data Source=analytics-projection-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False";
            _connection = new SqliteConnection(connectionString);
            _connection.Open();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoDbDSN"] = connectionString,
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
            }).Build();
            Factory = new DatabaseFactory(configuration, Mock.Of<IHostEnvironment>(x => x.ContentRootPath == AppContext.BaseDirectory));
            Options = Microsoft.Extensions.Options.Options.Create(new AnalyticsOptions { Enabled = true });
            using var db = Factory.GetDatabase();
            db.CreateTable<OrderData>();
            db.CreateTable<OrderActivityLog>();
        }

        public DatabaseFactory Factory { get; }
        public IOptions<AnalyticsOptions> Options { get; }
        public AnalyticsSchema CreateSchema() => new(Factory, Options, NullLogger<AnalyticsSchema>.Instance);
        public AnalyticsProjectionService CreateService() => new(Factory, CreateSchema(),
            new AnalyticsSnapshotMapper(new ConfiguredAnalyticsCustomerIdentityResolver(Options), Options), Options,
            new ActivityLogRepository(NullLogger<ActivityLogRepository>.Instance, Factory),
            NullLogger<AnalyticsProjectionService>.Instance);

        public Guid AddOrder(string status, string snapshot)
        {
            Guid id = Guid.NewGuid();
            using var db = Factory.GetDatabase();
            db.Insert(new OrderData
            {
                UniqueId = id,
                StoreAlias = "main",
                Currency = "en-US",
                OrderStatusCol = status,
                OrderInfo = snapshot,
            });
            return id;
        }

        public void SeedProjection(Guid orderId)
        {
            using var db = Factory.GetDatabase();
            db.Insert(new AnalyticsOrderData { OrderId = orderId, GrandTotal = 42m, StoreAlias = "main", CurrencyCode = "USD" });
            db.Insert(new AnalyticsOrderLineData { OrderId = orderId, OrderLineKey = Guid.NewGuid(), Quantity = 1 });
            db.Insert(new AnalyticsPromotionData { OrderId = orderId, ApplicationId = Guid.NewGuid(), CouponCode = "OLD" });
        }

        public void Dispose() => _connection.Dispose();
    }
}

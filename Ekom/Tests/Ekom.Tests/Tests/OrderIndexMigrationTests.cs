using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class OrderIndexMigrationTests
{
    [Fact]
    public void FreshCreateTablesCreatesReferenceIdPrimaryKeyAndUniqueIdIndex()
    {
        using var fixture = new Fixture();

        fixture.Service.CreateTables();

        fixture.AssertRequiredIndexes();
        var before = fixture.OrderSchema();
        fixture.Service.CreateTables();
        Assert.Equal(before, fixture.OrderSchema());
    }

    [Fact]
    public void ExistingMappedTableIsRepairedIdempotentlyWithoutChangingOrders()
    {
        using var fixture = new Fixture();
        fixture.CreateOrderTable();
        fixture.InsertOrder(Guid.NewGuid(), "first payload");
        fixture.InsertOrder(Guid.NewGuid(), "second payload");
        var orders = fixture.Orders();
        Assert.Equal(0, fixture.CanonicalIndexCount());

        fixture.Service.EnsureOrderIndexes();

        fixture.AssertRequiredIndexes();
        Assert.Equal(orders, fixture.Orders());
        var schema = fixture.OrderSchema();
        fixture.Service.EnsureOrderIndexes();
        Assert.Equal(schema, fixture.OrderSchema());
        Assert.Equal(orders, fixture.Orders());
    }

    [Fact]
    public void CreateTablesRepairsAnExistingMappedTable()
    {
        using var fixture = new Fixture();
        fixture.CreateOrderTable();
        fixture.InsertOrder(Guid.NewGuid(), "existing order");
        var orders = fixture.Orders();

        fixture.Service.CreateTables();

        fixture.AssertRequiredIndexes();
        Assert.Equal(orders, fixture.Orders());
    }

    [Theory]
    [InlineData("CREATE UNIQUE INDEX LegacyOrderUniqueId ON EkomOrders (UniqueId)")]
    [InlineData("CREATE UNIQUE INDEX LegacyOrderUniqueId ON EkomOrders (UniqueId DESC)")]
    public void EquivalentAlternateIndexIsPreservedWithoutCreatingCanonicalIndex(string sql)
    {
        using var fixture = new Fixture();
        fixture.CreateOrderTable();
        fixture.Execute(sql);
        fixture.InsertOrder(Guid.NewGuid(), "existing order");
        var schema = fixture.OrderSchema();
        var orders = fixture.Orders();

        fixture.Service.EnsureOrderIndexes();
        fixture.Service.EnsureOrderIndexes();

        Assert.Equal(0, fixture.CanonicalIndexCount());
        Assert.Equal(schema, fixture.OrderSchema());
        Assert.Equal(orders, fixture.Orders());
    }

    [Theory]
    [InlineData("CREATE UNIQUE INDEX LegacyOrderUniqueId ON EkomOrders (UniqueId, ReferenceId)")]
    [InlineData("CREATE UNIQUE INDEX LegacyOrderUniqueId ON EkomOrders (UniqueId) WHERE ReferenceId > 0")]
    [InlineData("CREATE INDEX LegacyOrderUniqueId ON EkomOrders (UniqueId)")]
    public void CompositePartialOrNonUniqueAlternateIndexDoesNotReplaceRequiredIndex(string sql)
    {
        using var fixture = new Fixture();
        fixture.CreateOrderTable();
        fixture.Execute(sql);
        fixture.InsertOrder(Guid.NewGuid(), "existing order");
        var orders = fixture.Orders();

        fixture.Service.EnsureOrderIndexes();

        fixture.AssertRequiredIndexes();
        Assert.Equal(1, fixture.Scalar<int>("SELECT COUNT(*) FROM pragma_index_list('EkomOrders') WHERE name = 'LegacyOrderUniqueId'"));
        Assert.Equal(orders, fixture.Orders());
    }

    [Theory]
    [InlineData("CREATE INDEX IX_EkomOrders_UniqueId ON EkomOrders (UniqueId)", false)]
    [InlineData("CREATE UNIQUE INDEX IX_EkomOrders_UniqueId ON EkomOrders (ReferenceId)", false)]
    [InlineData("CREATE UNIQUE INDEX IX_EkomOrders_UniqueId ON EkomOrders (UniqueId, ReferenceId)", false)]
    [InlineData("CREATE UNIQUE INDEX IX_EkomOrders_UniqueId ON EkomOrders (UniqueId) WHERE ReferenceId > 0", false)]
    [InlineData("CREATE INDEX IX_EkomOrders_UniqueId ON EkomOrders (UniqueId)", true)]
    [InlineData("CREATE UNIQUE INDEX IX_EkomOrders_UniqueId ON EkomOrders (ReferenceId)", true)]
    [InlineData("CREATE UNIQUE INDEX IX_EkomOrders_UniqueId ON EkomOrders (UniqueId, ReferenceId)", true)]
    [InlineData("CREATE UNIQUE INDEX IX_EkomOrders_UniqueId ON EkomOrders (UniqueId) WHERE ReferenceId > 0", true)]
    public void ConflictingCanonicalIndexFailsWithoutMutationEvenWhenAlternateIsValid(string sql, bool alternate)
    {
        using var fixture = new Fixture();
        fixture.CreateOrderTable();
        fixture.Execute(sql);
        if (alternate)
        {
            fixture.Execute("CREATE UNIQUE INDEX LegacyOrderUniqueId ON EkomOrders (UniqueId)");
        }
        fixture.InsertOrder(Guid.NewGuid(), "existing order");
        var schema = fixture.OrderSchema();
        var orders = fixture.Orders();

        Assert.Throws<InvalidOperationException>(() => fixture.Service.EnsureOrderIndexes());

        Assert.Equal(schema, fixture.OrderSchema());
        Assert.Equal(orders, fixture.Orders());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("CREATE UNIQUE INDEX LegacyOrderUniqueId ON EkomOrders (UniqueId, ReferenceId)")]
    [InlineData("CREATE UNIQUE INDEX LegacyOrderUniqueId ON EkomOrders (UniqueId) WHERE ReferenceId < 0")]
    public void DuplicateUniqueIdsFailWithoutSchemaOrDataMutation(string? sql)
    {
        using var fixture = new Fixture();
        fixture.CreateOrderTable();
        if (sql != null)
        {
            fixture.Execute(sql);
        }
        var id = Guid.NewGuid();
        fixture.InsertOrder(id, "first duplicate");
        fixture.InsertOrder(id, "second duplicate");
        var schema = fixture.OrderSchema();
        var orders = fixture.Orders();

        Assert.Throws<InvalidOperationException>(() => fixture.Service.EnsureOrderIndexes());

        Assert.Equal(schema, fixture.OrderSchema());
        Assert.Equal(orders, fixture.Orders());
        Assert.Equal(0, fixture.CanonicalIndexCount());
    }

    [Fact]
    public void CreateTablesPropagatesOrderRepairFailure()
    {
        using var fixture = new Fixture();
        fixture.CreateOrderTable();
        var id = Guid.NewGuid();
        fixture.InsertOrder(id, "first duplicate");
        fixture.InsertOrder(id, "second duplicate");
        var schema = fixture.OrderSchema();
        var orders = fixture.Orders();

        Assert.Throws<InvalidOperationException>(() => fixture.Service.CreateTables());

        Assert.Equal(schema, fixture.OrderSchema());
        Assert.Equal(orders, fixture.Orders());
    }

    [Theory]
    [InlineData("ReferenceId INTEGER NOT NULL, UniqueId TEXT NOT NULL")]
    [InlineData("ReferenceId INTEGER NOT NULL, UniqueId TEXT NOT NULL PRIMARY KEY")]
    [InlineData("ReferenceId INTEGER NOT NULL, UniqueId TEXT NOT NULL, PRIMARY KEY (ReferenceId, UniqueId)")]
    public void MissingOrMalformedReferenceIdPrimaryKeyFailsWithoutMutation(string columns)
    {
        using var fixture = new Fixture();
        fixture.Execute($"CREATE TABLE EkomOrders ({columns})");
        fixture.Execute("INSERT INTO EkomOrders (ReferenceId, UniqueId) VALUES (7, 'existing-order')");
        var schema = fixture.OrderSchema();

        Assert.Throws<InvalidOperationException>(() => fixture.Service.EnsureOrderIndexes());

        Assert.Equal(schema, fixture.OrderSchema());
        Assert.Equal(1, fixture.Scalar<int>("SELECT COUNT(*) FROM EkomOrders"));
        Assert.Equal("existing-order", fixture.Scalar<string>("SELECT UniqueId FROM EkomOrders WHERE ReferenceId = 7"));
        Assert.Equal(0, fixture.CanonicalIndexCount());
    }

    [Fact]
    public async Task ConcurrentRepairsInstallOneValidIndexWithoutChangingOrders()
    {
        using var fixture = new Fixture();
        fixture.CreateOrderTable();
        fixture.InsertOrder(Guid.NewGuid(), "existing order");
        var orders = fixture.Orders();
        using var start = new ManualResetEventSlim();
        var repairs = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            start.Wait();
            fixture.Service.EnsureOrderIndexes();
        })).ToArray();

        start.Set();
        await Task.WhenAll(repairs);

        fixture.AssertRequiredIndexes();
        Assert.Equal(orders, fixture.Orders());
    }

    [Fact]
    public void RepairedIndexEnforcesUniqueIdsAndSupportsLookup()
    {
        using var fixture = new Fixture();
        fixture.CreateOrderTable();
        fixture.Service.EnsureOrderIndexes();
        var id = Guid.NewGuid();
        fixture.InsertOrder(id, "original order");

        Assert.Throws<SqliteException>(() => fixture.InsertOrder(id, "duplicate order"));

        using var db = fixture.Factory.GetDatabase();
        Assert.Equal("original order", db.OrderData.Single(x => x.UniqueId == id).OrderInfo);
        using var connection = fixture.Factory.GetDbConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN SELECT * FROM EkomOrders WHERE UniqueId = @id";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = id;
        command.Parameters.Add(parameter);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Contains("IX_EkomOrders_UniqueId", reader.GetString(3), StringComparison.Ordinal);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"ekom-order-index-{Guid.NewGuid():N}.sqlite");

        public Fixture()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoDbDSN"] = $"Data Source={_path};Pooling=False;Default Timeout=2",
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
            }).Build();
            var host = new Mock<IHostEnvironment>();
            host.SetupGet(x => x.ContentRootPath).Returns(Path.GetTempPath());
            Factory = new DatabaseFactory(configuration, host.Object);
            Service = new DatabaseService(Factory, NullLogger<DatabaseService>.Instance, new StockReservationReadiness());
        }

        public DatabaseFactory Factory { get; }
        public DatabaseService Service { get; }

        public void CreateOrderTable()
        {
            using var db = Factory.GetDatabase();
            db.CreateTable<OrderData>();
        }

        public void InsertOrder(Guid id, string payload)
        {
            using var db = Factory.GetDatabase();
            db.Insert(new OrderData
            {
                UniqueId = id,
                OrderInfo = payload,
                OrderNumber = payload,
                OrderStatus = OrderStatus.Incomplete,
                StoreAlias = "main",
                Currency = "USD",
                CustomerEmail = "test@example.invalid",
                CustomerUsername = "test",
                ShippingCountry = "IS",
                TotalAmount = 123.45m,
                CreateDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdateDate = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            });
        }

        public string Orders()
        {
            using var db = Factory.GetDatabase();
            return JsonSerializer.Serialize(db.OrderData.OrderBy(x => x.ReferenceId).ToArray());
        }

        public string[] OrderSchema()
        {
            using var db = Factory.GetDatabase();
            return db.Query<string>("SELECT type || ':' || name || ':' || COALESCE(sql, '') FROM sqlite_master WHERE tbl_name = 'EkomOrders' ORDER BY type, name").ToArray();
        }

        public void Execute(string sql)
        {
            using var db = Factory.GetDatabase();
            db.Execute(sql);
        }

        public T Scalar<T>(string sql)
        {
            using var db = Factory.GetDatabase();
            return db.Execute<T>(sql);
        }

        public int CanonicalIndexCount() => Scalar<int>("SELECT COUNT(*) FROM pragma_index_list('EkomOrders') WHERE name = 'IX_EkomOrders_UniqueId'");

        public void AssertRequiredIndexes()
        {
            Assert.Equal(1, Scalar<int>("SELECT COUNT(*) FROM pragma_table_info('EkomOrders') WHERE pk > 0"));
            Assert.Equal(1, Scalar<int>("SELECT pk FROM pragma_table_info('EkomOrders') WHERE name = 'ReferenceId'"));
            Assert.Equal(1, Scalar<int>("SELECT COUNT(*) FROM pragma_index_list('EkomOrders') WHERE name = 'IX_EkomOrders_UniqueId' AND \"unique\" = 1 AND partial = 0"));
            Assert.Equal(1, Scalar<int>("SELECT COUNT(*) FROM pragma_index_info('IX_EkomOrders_UniqueId')"));
            Assert.Equal("UniqueId", Scalar<string>("SELECT name FROM pragma_index_info('IX_EkomOrders_UniqueId') WHERE seqno = 0"));
        }

        public void Dispose() => File.Delete(_path);
    }
}

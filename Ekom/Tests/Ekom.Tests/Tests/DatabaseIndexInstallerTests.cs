using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using System.Text.Json;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class DatabaseIndexInstallerTests
{
    public static IEnumerable<object[]> IndexDefinitions()
    {
        yield return new object[] { "EkomOrders", "IX_EkomOrders_CustomerId", new[] { "CustomerId" } };
        yield return new object[] { "EkomOrders", "IX_EkomOrders_CustomerUsername", new[] { "CustomerUsername" } };
        yield return new object[] { "EkomOrdersActivityLog", "IX_EkomOrdersActivityLog_Key_Date", new[] { "Key", "Date" } };
    }

    [Theory]
    [MemberData(nameof(IndexDefinitions))]
    public void FreshAndExistingMappedTablesAreIndexedIdempotentlyWithoutChangingData(string table, string name, string[] columns)
    {
        using var fixture = new Fixture();
        fixture.Ensure(table, name, columns);
        fixture.AssertIndex(table, name, columns);
        fixture.Seed();
        var data = fixture.Data();
        var schema = fixture.Schema();

        fixture.Ensure(table, name, columns);
        fixture.Ensure(table, name, columns);

        Assert.Equal(schema, fixture.Schema());
        Assert.Equal(data, fixture.Data());

        // Exercise the upgrade path separately: the table already contains rows before creation.
        using var existing = new Fixture();
        existing.Seed();
        var existingData = existing.Data();
        existing.Ensure(table, name, columns);
        var repairedSchema = existing.Schema();
        existing.Ensure(table, name, columns);

        existing.AssertIndex(table, name, columns);
        Assert.Equal(repairedSchema, existing.Schema());
        Assert.Equal(existingData, existing.Data());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void EquivalentAlternateNamesAndDescendingKeysAreRetainedRegardlessOfUniqueness(bool unique, bool descending)
    {
        using var fixture = new Fixture();
        fixture.Seed();
        foreach (var definition in IndexDefinitions())
        {
            var table = (string)definition[0];
            var name = (string)definition[1];
            var columns = (string[])definition[2];
            var keys = string.Join(", ", columns.Select(column => $"[{column}]" + (descending ? " DESC" : string.Empty)));
            fixture.Execute($"CREATE {(unique ? "UNIQUE " : string.Empty)}INDEX [Legacy_{name}] ON [{table}] ({keys})");
        }
        var schema = fixture.Schema();
        var data = fixture.Data();

        foreach (var definition in IndexDefinitions())
        {
            var table = (string)definition[0];
            var name = (string)definition[1];
            var columns = (string[])definition[2];
            fixture.Ensure(table, name, columns);
            fixture.Ensure(table, name, columns);
            Assert.Equal(0, fixture.IndexCount(name));
            fixture.AssertIndex(table, $"Legacy_{name}", columns);
        }

        Assert.Equal(schema, fixture.Schema());
        Assert.Equal(data, fixture.Data());
    }

    [Fact]
    public void PerformanceIndexAllowsDuplicateKeys()
    {
        using var fixture = new Fixture();
        fixture.Seed();
        fixture.Ensure("EkomOrders", "CustomerLookup", "CustomerId", "StoreAlias");

        fixture.Execute("INSERT INTO EkomOrders (UniqueId, OrderStatusCol, CustomerId, StoreAlias, TotalAmount, CreateDate, UpdateDate) SELECT UniqueId, OrderStatusCol, CustomerId, StoreAlias, TotalAmount, CreateDate, UpdateDate FROM EkomOrders WHERE CustomerId = 7");

        Assert.Equal(2, fixture.Scalar<int>("SELECT COUNT(*) FROM EkomOrders WHERE CustomerId = 7 AND StoreAlias = 'main'"));
        Assert.Equal(0, fixture.Scalar<int>("SELECT [unique] FROM pragma_index_list('EkomOrders') WHERE name = 'CustomerLookup'"));
        fixture.Ensure("EkomOrders", "CustomerLookup", "CustomerId", "StoreAlias");
    }

    [Theory]
    [InlineData("CustomerId, StoreAlias", " WHERE CustomerId > 0")]
    [InlineData("CustomerId + 0, StoreAlias", "")]
    [InlineData("CustomerId, StoreAlias, CreateDate", "")]
    [InlineData("StoreAlias, CustomerId", "")]
    [InlineData("CustomerId, StoreAlias COLLATE NOCASE", "")]
    public void PartialExpressionExtraKeyWrongOrderOrWrongCollationIsNotEquivalent(string keys, string suffix)
    {
        using var fixture = new Fixture();
        fixture.Seed();
        fixture.Execute($"CREATE INDEX LegacyCustomerLookup ON EkomOrders ({keys}){suffix}");
        var legacySql = fixture.Scalar<string>("SELECT sql FROM sqlite_master WHERE name = 'LegacyCustomerLookup'");
        var data = fixture.Data();

        fixture.Ensure("EkomOrders", "CustomerLookup", "CustomerId", "StoreAlias");
        var schema = fixture.Schema();
        fixture.Ensure("EkomOrders", "CustomerLookup", "CustomerId", "StoreAlias");

        fixture.AssertIndex("EkomOrders", "CustomerLookup", new[] { "CustomerId", "StoreAlias" });
        Assert.Equal(legacySql, fixture.Scalar<string>("SELECT sql FROM sqlite_master WHERE name = 'LegacyCustomerLookup'"));
        Assert.Equal(schema, fixture.Schema());
        Assert.Equal(data, fixture.Data());
    }

    [Theory]
    [InlineData("CustomerId, StoreAlias", " WHERE CustomerId > 0")]
    [InlineData("CustomerId + 0, StoreAlias", "")]
    [InlineData("CustomerId, StoreAlias, CreateDate", "")]
    [InlineData("StoreAlias, CustomerId", "")]
    [InlineData("CustomerId, StoreAlias COLLATE NOCASE", "")]
    public void ConflictingCanonicalFailsEvenWithEquivalentAlternateWithoutMutation(string keys, string suffix)
    {
        using var fixture = new Fixture();
        fixture.Seed();
        fixture.Execute($"CREATE INDEX CustomerLookup ON EkomOrders ({keys}){suffix}");
        fixture.Execute("CREATE INDEX LegacyCustomerLookup ON EkomOrders (CustomerId, StoreAlias)");
        var schema = fixture.Schema();
        var data = fixture.Data();

        var exception = Assert.Throws<InvalidOperationException>(() => fixture.Ensure("EkomOrders", "CustomerLookup", "CustomerId", "StoreAlias"));

        Assert.Contains("CustomerLookup", exception.Message, StringComparison.Ordinal);
        Assert.Equal(schema, fixture.Schema());
        Assert.Equal(data, fixture.Data());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanonicalNameOnAnotherTableFailsEvenWithEquivalentAlternate(bool alternate)
    {
        using var fixture = new Fixture();
        fixture.Seed();
        // Names are database-wide in SQLite, including case-insensitive collisions.
        fixture.Execute("CREATE INDEX customerlookup ON EkomOrdersActivityLog ([Key], [Date])");
        if (alternate)
        {
            fixture.Execute("CREATE INDEX LegacyCustomerLookup ON EkomOrders (CustomerId, StoreAlias)");
        }
        var schema = fixture.Schema();
        var data = fixture.Data();

        Assert.Throws<InvalidOperationException>(() => fixture.Ensure("EkomOrders", "CustomerLookup", "CustomerId", "StoreAlias"));

        Assert.Equal(schema, fixture.Schema());
        Assert.Equal(data, fixture.Data());
    }

    [Fact]
    public async Task ConcurrentIdenticalCreationInstallsOneIndexWithoutChangingData()
    {
        using var fixture = new Fixture();
        fixture.Seed();
        var data = fixture.Data();
        using var start = new ManualResetEventSlim();
        var installers = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            start.Wait();
            // Each installer owns its connection; no DbContext is shared between tasks.
            fixture.Ensure("EkomOrders", "CustomerLookup", "CustomerId", "StoreAlias");
        })).ToArray();

        start.Set();
        await Task.WhenAll(installers);

        Assert.Equal(1, fixture.IndexCount("CustomerLookup"));
        fixture.AssertIndex("EkomOrders", "CustomerLookup", new[] { "CustomerId", "StoreAlias" });
        Assert.Equal(data, fixture.Data());
    }

    [Fact]
    public void CustomerAndActivityQueriesUseIndexesWithoutChangingResults()
    {
        using var fixture = new Fixture();
        fixture.Seed();
        var before = fixture.LookupResults();
        var data = fixture.Data();
        foreach (var definition in IndexDefinitions())
        {
            fixture.Ensure((string)definition[0], (string)definition[1], (string[])definition[2]);
        }

        var after = fixture.LookupResults();
        Assert.Equal(before.CustomerIds, after.CustomerIds);
        Assert.Equal(before.CustomerUsernames, after.CustomerUsernames);
        Assert.Equal(before.Activities, after.Activities);
        Assert.Equal(data, fixture.Data());
        Assert.Equal(new[] { 1 }, before.CustomerIds);
        Assert.Equal(new[] { 1 }, before.CustomerUsernames);
        Assert.Equal(new[] { "second activity", "first activity" }, before.Activities);
        Assert.Contains(fixture.Plan("SELECT * FROM EkomOrders WHERE CustomerId = 7 AND StoreAlias = 'main'"),
            detail => detail.Contains("SEARCH", StringComparison.Ordinal) && detail.Contains("IX_EkomOrders_CustomerId", StringComparison.Ordinal));
        Assert.Contains(fixture.Plan("SELECT * FROM EkomOrders WHERE CustomerUsername = 'customer-7' AND StoreAlias = 'main'"),
            detail => detail.Contains("SEARCH", StringComparison.Ordinal) && detail.Contains("IX_EkomOrders_CustomerUsername", StringComparison.Ordinal));
        var logPlan = fixture.Plan("SELECT * FROM EkomOrdersActivityLog WHERE [Key] = (SELECT UniqueId FROM EkomOrders WHERE ReferenceId = 1) ORDER BY [Date] DESC");
        Assert.Contains(logPlan, detail => detail.Contains("SEARCH", StringComparison.Ordinal) && detail.Contains("IX_EkomOrdersActivityLog_Key_Date", StringComparison.Ordinal));
        Assert.DoesNotContain(logPlan, detail => detail.Contains("TEMP B-TREE", StringComparison.Ordinal));
    }

    private sealed record Results(int[] CustomerIds, int[] CustomerUsernames, string[] Activities);

    private sealed class Fixture : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"ekom-performance-index-{Guid.NewGuid():N}.sqlite");
        private readonly DatabaseFactory _factory;
        private readonly Guid _orderId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        public Fixture()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoDbDSN"] = $"Data Source={_path};Pooling=False;Default Timeout=30",
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
            }).Build();
            var host = new Mock<IHostEnvironment>();
            host.SetupGet(x => x.ContentRootPath).Returns(Path.GetTempPath());
            _factory = new DatabaseFactory(configuration, host.Object);
            using var db = _factory.GetDatabase();
            db.CreateTable<OrderData>();
            db.CreateTable<OrderActivityLog>();
        }

        public void Seed()
        {
            using var db = _factory.GetDatabase();
            for (var ordinal = 0; ordinal < 3; ordinal++)
            {
                var date = new DateTime(2026, 1, ordinal + 1, 0, 0, 0, DateTimeKind.Utc);
                db.Insert(new OrderData
                {
                    UniqueId = ordinal == 0 ? _orderId : Guid.Parse($"22222222-2222-2222-2222-{ordinal:D12}"),
                    OrderInfo = $"payload-{ordinal}",
                    OrderNumber = $"order-{ordinal}",
                    OrderStatus = OrderStatus.Incomplete,
                    StoreAlias = ordinal == 2 ? "other" : "main",
                    CustomerId = ordinal + 7,
                    CustomerUsername = $"customer-{ordinal + 7}",
                    CustomerEmail = "test@example.invalid",
                    Currency = "USD",
                    ShippingCountry = "IS",
                    TotalAmount = 123.45m,
                    CreateDate = date,
                    UpdateDate = date,
                    PaidDate = ordinal == 2 ? null : date.AddHours(1),
                });
                db.Insert(new OrderActivityLog
                {
                    UniqueID = Guid.Parse($"33333333-3333-3333-3333-{ordinal:D12}"),
                    Key = ordinal == 2 ? Guid.Parse("44444444-4444-4444-4444-444444444444") : _orderId,
                    Log = new[] { "first activity", "second activity", "unrelated activity" }[ordinal],
                    UserName = "test",
                    Date = date,
                });
            }
        }

        public void Ensure(string table, string name, params string[] columns)
        {
            using var db = _factory.GetDatabase();
            DatabaseIndexInstaller.EnsureIndex(db, false, table, name, columns);
        }

        public void Execute(string sql)
        {
            using var db = _factory.GetDatabase();
            db.Execute(sql);
        }

        public T Scalar<T>(string sql)
        {
            using var db = _factory.GetDatabase();
            return db.Execute<T>(sql);
        }

        public int IndexCount(string name) => Scalar<int>($"SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = '{name}' COLLATE NOCASE");

        public void AssertIndex(string table, string name, string[] columns)
        {
            Assert.Equal(1, Scalar<int>($"SELECT COUNT(*) FROM pragma_index_list('{table}') WHERE name = '{name}' AND partial = 0"));
            using var db = _factory.GetDatabase();
            Assert.Equal(columns, db.Query<string>($"SELECT name FROM pragma_index_xinfo('{name}') WHERE [key] = 1 ORDER BY seqno").ToArray());
            Assert.Equal(columns.Length, Scalar<int>($"SELECT COUNT(*) FROM pragma_index_xinfo('{name}') WHERE [key] = 1 AND coll = 'BINARY'"));
        }

        public string[] Schema()
        {
            using var db = _factory.GetDatabase();
            return db.Query<string>("SELECT type || ':' || name || ':' || COALESCE(sql, '') FROM sqlite_master ORDER BY type, name").ToArray();
        }

        public string Data()
        {
            using var db = _factory.GetDatabase();
            return JsonSerializer.Serialize(new
            {
                Orders = db.OrderData.OrderBy(row => row.ReferenceId).ToArray(),
                Logs = db.OrderActivityLog.OrderBy(row => row.UniqueID).ToArray(),
            });
        }

        public Results LookupResults()
        {
            using var db = _factory.GetDatabase();
            return new Results(
                db.OrderData.Where(row => row.CustomerId == 7 && row.StoreAlias == "main").OrderBy(row => row.ReferenceId).Select(row => row.ReferenceId).ToArray(),
                db.OrderData.Where(row => row.CustomerUsername == "customer-7" && row.StoreAlias == "main").OrderBy(row => row.ReferenceId).Select(row => row.ReferenceId).ToArray(),
                db.OrderActivityLog.Where(row => row.Key == _orderId).OrderByDescending(row => row.Date).Select(row => row.Log).ToArray());
        }

        public string[] Plan(string sql)
        {
            using var connection = _factory.GetDbConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            // Only fixed queries from this test class are run against its isolated database.
#pragma warning disable CA2100
            command.CommandText = $"EXPLAIN QUERY PLAN {sql}";
#pragma warning restore CA2100
            using var reader = command.ExecuteReader();
            var details = new List<string>();
            while (reader.Read())
            {
                details.Add(reader.GetString(3));
            }
            return details.ToArray();
        }

        public void Dispose() => File.Delete(_path);
    }
}

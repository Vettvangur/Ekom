using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using Ekom.Tracking;
using Ekom.Utilities;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Diagnostics;
using Xunit;

namespace Ekom.Tests.Tests;

// The LINQ SQL trace hook is global; do not change it while other tests are running.
[CollectionDefinition("Customer history SQL", DisableParallelization = true)]
public sealed class CustomerHistorySqlCollection;

[Collection("Customer history SQL")]
public sealed class CustomerOrderHistoryTests
{
    [Theory]
    [InlineData(true, null)]
    [InlineData(false, null)]
    [InlineData(true, "")]
    [InlineData(false, "")]
    [InlineData(true, "main")]
    [InlineData(false, "main")]
    [InlineData(true, "MAIN")]
    [InlineData(false, "MAIN")]
    [InlineData(true, "main ")]
    [InlineData(false, "main ")]
    [InlineData(true, " ")]
    [InlineData(false, " ")]
    [InlineData(true, "missing")]
    [InlineData(false, "missing")]
    public async Task HistoryPreservesExactStoreAndCompleteStatusFiltering(bool byUsername, string? storeAlias)
    {
        using var fixture = new Fixture();
        foreach (string? alias in new string?[] { "main", "other", "MAIN", "main ", " ", "", null })
        {
            foreach (OrderStatus status in Enum.GetValues<OrderStatus>())
            {
                fixture.Add(alias, status);
            }
        }
        // Both customer conditions must survive the additional store predicate.
        fixture.Add("main", OrderStatus.Dispatched, customerId: 99, username: "someone-else");
        fixture.Add("main", OrderStatus.Dispatched, customerId: 99);
        fixture.Add("main", OrderStatus.Dispatched, username: "someone-else");
        using var db = fixture.Factory.GetDatabase();
        var baseline = await db.OrderData.ToListAsync();
        var expected = baseline.Where(x =>
                (byUsername ? x.CustomerUsername == "customer" : x.CustomerId == 42)
                && x.OrderStatus is OrderStatus.ReadyForDispatch or OrderStatus.OfflinePayment or OrderStatus.Dispatched
                && (string.IsNullOrEmpty(storeAlias) || x.StoreAlias == storeAlias))
            .Select(x => x.UniqueId).OrderBy(x => x).ToList();
        fixture.Commands.Clear();

        var history = byUsername
            ? await fixture.Service.GetCompleteCustomerOrdersAsync("customer", storeAlias: storeAlias)
            : await fixture.Service.GetCompleteCustomerOrdersAsync(42, storeAlias: storeAlias);

        Assert.Equal(expected, history.Select(x => x.UniqueId).OrderBy(x => x));
        string sql = Assert.Single(fixture.Commands);
        string where = sql[sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase)..];
        Assert.Contains("OrderStatusCol", where, StringComparison.Ordinal);
        Assert.Contains(byUsername ? "CustomerUsername" : "CustomerId", where, StringComparison.Ordinal);
        if (string.IsNullOrEmpty(storeAlias))
        {
            Assert.DoesNotContain("StoreAlias", where, StringComparison.Ordinal);
        }
        else
        {
            Assert.Matches(@"StoreAlias[\]""`]?\s*=\s*@", where);
        }
    }

    [Fact]
    public async Task DatabasePrefilterIsBroaderThanExactStoreMatching()
    {
        using var fixture = new Fixture();
        var exact = fixture.Add("main", OrderStatus.Dispatched);
        fixture.Add("MAIN", OrderStatus.Dispatched);
        fixture.Add("main ", OrderStatus.Dispatched);
        fixture.Add("other", OrderStatus.Dispatched);
        using var db = fixture.Factory.GetDatabase();

        // Test-only collation emulates the relevant SQL Server equality behavior.
        Assert.Equal(3, await db.OrderData.CountAsync(x => x.StoreAlias == "main"));
        Assert.Equal(exact.UniqueId, Assert.Single(await fixture.Service.GetCompleteCustomerOrdersAsync("customer", storeAlias: "main")).UniqueId);
        Assert.Equal(exact.UniqueId, Assert.Single(await fixture.Service.GetCompleteCustomerOrdersAsync(42, storeAlias: "main")).UniqueId);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        private readonly Action<TraceInfo> _previousTrace = DataConnection.DefaultOnTraceConnection;
        private readonly TraceLevel _previousLevel = DataConnection.TraceSwitch.Level;

        public Fixture()
        {
            string connectionString = $"Data Source=history-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False";
            _connection = new SqliteConnection(connectionString);
            _connection.Open();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoDbDSN"] = connectionString,
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
            }).Build();
            Factory = new DatabaseFactory(configuration, Mock.Of<IHostEnvironment>(x => x.ContentRootPath == AppContext.BaseDirectory));
            DataConnection.DefaultOnTraceConnection = info =>
            {
                if (info.TraceInfoStep != TraceInfoStep.BeforeExecute) return;
                if (info.DataConnection.Connection is SqliteConnection connection)
                {
                    connection.CreateCollation("HistoryStore", (left, right) =>
                        StringComparer.OrdinalIgnoreCase.Compare(left?.TrimEnd(' '), right?.TrimEnd(' ')));
                }
                if (info.Command?.CommandText is string sql && sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                {
                    Commands.Add(sql);
                }
            };
            try
            {
                DataConnection.TraceSwitch.Level = TraceLevel.Info;
                using var db = Factory.GetDatabase();
                db.CreateTable<OrderData>();
                // Change only the test database, never the production schema or collation.
                db.Execute("ALTER TABLE EkomOrders RENAME COLUMN StoreAlias TO UnusedStoreAlias");
                db.Execute("ALTER TABLE EkomOrders ADD COLUMN StoreAlias TEXT COLLATE HistoryStore");
                Service = CreateService(new Configuration(configuration), Factory, _cache);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private static OrderService CreateService(Configuration config, DatabaseFactory factory, IMemoryCache cache)
        {
            var repository = new OrderRepository(NullLogger<OrderRepository>.Instance, config, factory, cache);
            return new OrderService(config, repository, null!, Mock.Of<IOrderActivityLogService>(),
                NullLogger<OrderService>.Instance, Mock.Of<IStoreService>(), cache,
                Mock.Of<IMemberService>(), null!, Mock.Of<IOrderTrackingService>());
        }

        public DatabaseFactory Factory { get; }
        public OrderService Service { get; }
        public List<string> Commands { get; } = new();

        public OrderData Add(string? alias, OrderStatus status, int customerId = 42, string username = "customer")
        {
            var order = new OrderData
            {
                UniqueId = Guid.NewGuid(),
                StoreAlias = alias!,
                OrderStatus = status,
                CustomerId = customerId,
                CustomerUsername = username,
                Currency = "USD",
            };
            using var db = Factory.GetDatabase();
            db.Insert(order);
            return order;
        }

        public void Dispose()
        {
            DataConnection.DefaultOnTraceConnection = _previousTrace;
            DataConnection.TraceSwitch.Level = _previousLevel;
            _cache.Dispose();
            _connection.Dispose();
        }
    }
}

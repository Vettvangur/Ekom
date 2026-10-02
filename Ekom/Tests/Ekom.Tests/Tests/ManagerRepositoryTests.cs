using Ekom;
using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Repositories;
using Ekom.Services;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace Ekom.Tests.Tests;

public class ManagerRepositoryTests
{
    [Fact]
    public void GenerateWhereClause_IncludesUniqueIdForFullGuidQuery()
    {
        var repository = CreateRepository();
        string query = Guid.NewGuid().ToString();

        string whereClause = GenerateWhereClause(repository, query);

        Assert.Contains("UniqueId = @queryUniqueId", whereClause, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateWhereClause_DoesNotIncludeUniqueIdForPartialGuidQuery()
    {
        var repository = CreateRepository();

        string whereClause = GenerateWhereClause(repository, "abc123");

        Assert.DoesNotContain("UniqueId = @queryUniqueId", whereClause, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GenerateWhereClause_UsesParameterizedOrderLevelCouponAndShippingProvider(bool sqlite)
    {
        var repository = CreateRepository(sqlite);
        string shippingProvider = Guid.NewGuid().ToString();
        const string couponCode = " SAVE%' OR 1=1 -- ";

        string whereClause = GenerateWhereClause(repository, string.Empty, couponCode, shippingProvider);

        Assert.Contains(sqlite
            ? "json_extract(OrderInfo, '$.Coupon') COLLATE Ekom_ManagerCoupon_OrdinalIgnoreCase = @couponCode"
            : "LOWER(JSON_VALUE(OrderInfo, '$.Coupon')) = LOWER(@couponCode)", whereClause, StringComparison.Ordinal);
        Assert.Contains(sqlite
            ? "json_extract(OrderInfo, '$.ShippingProvider.Key') = @shippingProvider"
            : "JSON_VALUE(OrderInfo, '$.ShippingProvider.Key') = @shippingProvider", whereClause, StringComparison.Ordinal);
        Assert.DoesNotContain(couponCode, whereClause, StringComparison.Ordinal);
        Assert.DoesNotContain(shippingProvider, whereClause, StringComparison.Ordinal);
        Assert.DoesNotContain("OrderLines", whereClause, StringComparison.Ordinal);
        Assert.DoesNotContain("LIKE", whereClause, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, null, null)]
    [InlineData(false, null, null)]
    [InlineData(true, "", "")]
    [InlineData(false, "", "")]
    [InlineData(true, " \t ", "not-a-guid")]
    [InlineData(false, " \t ", "not-a-guid")]
    public void GenerateWhereClause_IgnoresBlankCouponAndInvalidShippingProvider(bool sqlite, string? couponCode, string? shippingProvider)
    {
        string whereClause = GenerateWhereClause(CreateRepository(sqlite), string.Empty, couponCode, shippingProvider);

        Assert.DoesNotContain("@couponCode", whereClause, StringComparison.Ordinal);
        Assert.DoesNotContain("@shippingProvider", whereClause, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GenerateWhereClause_CombinesNewAndExistingFilters(bool sqlite)
    {
        string whereClause = GenerateWhereClause(CreateRepository(sqlite), "customer", "SAVE", Guid.NewGuid().ToString(),
            paymentProvider: Guid.NewGuid().ToString(), productSku: "SKU", trackingSource: "newsletter");

        Assert.Contains("@query", whereClause, StringComparison.Ordinal);
        Assert.Contains("@paymentProvider", whereClause, StringComparison.Ordinal);
        Assert.Contains("@productSku", whereClause, StringComparison.Ordinal);
        Assert.Contains("@trackingSource", whereClause, StringComparison.Ordinal);
        Assert.Contains("@couponCode", whereClause, StringComparison.Ordinal);
        Assert.Contains("@shippingProvider", whereClause, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAndExport_FilterOrderCouponAndShippingWithConsistentTotals()
    {
        using var database = new OrderDatabase();
        Guid shippingProvider = Guid.NewGuid();
        Guid paymentProvider = Guid.NewGuid();
        Guid first = database.Add("Save10", shippingProvider, 10m, paymentProvider: paymentProvider);
        Guid second = database.Add("SAVE10", shippingProvider, 30m, paymentProvider: paymentProvider);
        database.Add(null, shippingProvider, 100m, lineCoupon: "Save10", paymentProvider: paymentProvider);
        database.Add("Save10-extra", shippingProvider, 200m, paymentProvider: paymentProvider);
        database.Add("Save10", Guid.NewGuid(), 300m, paymentProvider: paymentProvider);
        database.Add("Other", shippingProvider, 400m, paymentProvider: paymentProvider);

        string providerFilter = $" {shippingProvider.ToString().ToUpperInvariant()} ";
        OrderListData couponOnly = await SearchAsync(database.Repository, " save10 ", null);
        Assert.Equal(3, couponOnly.Count);
        OrderListData shippingOnly = await SearchAsync(database.Repository, null, providerFilter);
        Assert.Equal(5, shippingOnly.Count);

        OrderListData results = await SearchAsync(database.Repository, " save10 ", providerFilter, "1");

        Assert.Equal(second, Assert.Single(results.Orders).UniqueId);
        Assert.Equal(2, results.Count);
        Assert.Equal(2, results.TotalPages);
        Assert.Equal(40m.ToString("C", CultureInfo.GetCultureInfo("en-US")), results.GrandTotal);
        Assert.Equal(20m.ToString("C", CultureInfo.GetCultureInfo("en-US")), results.AverageAmount);

        OrderListData combined = await SearchAsync(database.Repository, " save10 ", providerFilter, "30", paymentProvider.ToString(), " sku ", " EMAIL ");
        Assert.Equal(2, combined.Count);
        Assert.Equal(new[] { second, first }, combined.Orders.Select(x => x.UniqueId));

        foreach (bool includeOrderInfo in new[] { false, true })
        {
            List<OrderData> export = await database.Repository.GetOrdersForExportAsync(database.Date, database.Date, string.Empty,
                "main", "AllOrders", paymentProvider.ToString(), " sku ", " EMAIL ", "", "", "", "", "", includeOrderInfo,
                couponCode: " save10 ", shippingProvider: providerFilter);

            Assert.Equal(new[] { second, first }, export.Select(x => x.UniqueId));
            Assert.All(export, x => Assert.Equal(includeOrderInfo, !string.IsNullOrEmpty(x.OrderInfo)));
        }
    }

    [Fact]
    public async Task SearchAndExport_IgnoreBlankFiltersAndMatchCouponLiterally()
    {
        using var database = new OrderDatabase();
        Guid provider = Guid.NewGuid();
        Guid literal = database.Add("SAVE%_'", provider, 10m);
        database.Add("SAVEanything", provider, 20m);
        database.Add(null, provider, 30m, lineCoupon: "SAVE%_'");

        OrderListData blank = await SearchAsync(database.Repository, " \t ", "invalid");
        Assert.Equal(3, blank.Count);
        Assert.Equal(3, blank.Orders.Count());

        OrderListData exact = await SearchAsync(database.Repository, " save%_' ", null);
        Assert.Equal(literal, Assert.Single(exact.Orders).UniqueId);
        Assert.Equal(1, exact.Count);

        List<OrderData> exactExport = await database.Repository.GetOrdersForExportAsync(database.Date, database.Date, "", "main", "AllOrders",
            "", "", "", "", "", "", "", "", false, couponCode: " save%_' ");
        Assert.Equal(literal, Assert.Single(exactExport).UniqueId);

        List<OrderData> export = await database.Repository.GetOrdersForExportAsync(database.Date, database.Date, "", "main", "AllOrders",
            "", "", "", "", "", "", "", "", false, couponCode: " \t ", shippingProvider: "invalid");
        Assert.Equal(3, export.Count);

        OrderListData missing = await SearchAsync(database.Repository, "missing", provider.ToString());
        Assert.Empty(missing.Orders);
        Assert.Equal(0, missing.Count);
    }

    [Fact]
    public async Task SearchAndExport_MatchUnicodeCouponCaseWithConsistentTotals()
    {
        using var database = new OrderDatabase();
        Guid provider = Guid.NewGuid();
        Guid first = database.Add("SUMAR\u00c1", provider, 10m);
        Guid second = database.Add("sumar\u00e1", provider, 30m);
        database.Add("SUMARA", provider, 100m);
        database.Add("SUMAR\u00c1-extra", provider, 200m);
        database.Add(null, provider, 300m, lineCoupon: "SUMAR\u00c1");
        database.Add("SUMAR\u00c1", Guid.NewGuid(), 400m);

        OrderListData results = await SearchAsync(database.Repository, " sumar\u00e1 ", provider.ToString(), "1");

        Assert.Equal(second, Assert.Single(results.Orders).UniqueId);
        Assert.Equal(2, results.Count);
        Assert.Equal(2, results.TotalPages);
        Assert.Equal(40m.ToString("C", CultureInfo.GetCultureInfo("en-US")), results.GrandTotal);
        Assert.Equal(20m.ToString("C", CultureInfo.GetCultureInfo("en-US")), results.AverageAmount);

        foreach (bool includeOrderInfo in new[] { false, true })
        {
            List<OrderData> export = await database.Repository.GetOrdersForExportAsync(database.Date, database.Date, "", "main", "AllOrders",
                "", "", "", "", "", "", "", "", includeOrderInfo, couponCode: " sumar\u00e1 ", shippingProvider: provider.ToString());
            Assert.Equal(new[] { second, first }, export.Select(x => x.UniqueId));
        }
    }

    [Fact]
    public async Task CouponCollation_IsRegisteredOnAsyncAndSyncReconnectsWithoutOverridingBuiltIns()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:umbracoDbDSN"] = "Data Source=:memory:;Pooling=False",
            ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
        }).Build();
        var factory = new DatabaseFactory(configuration, Mock.Of<IHostEnvironment>(x => x.ContentRootPath == AppContext.BaseDirectory));
        await using var db = factory.GetDatabase();
        MethodInfo configure = typeof(ManagerRepository).GetMethod("ConfigureCouponComparison", BindingFlags.NonPublic | BindingFlags.Instance)!;
        configure.Invoke(CreateRepository(), new object[] { db, "sumar\u00e1" });
        const string sql = "SELECT @upper COLLATE Ekom_ManagerCoupon_OrdinalIgnoreCase = @lower";
        var parameters = new { upper = "SUMAR\u00c1", lower = "sumar\u00e1" };

        Assert.Equal(1, await db.ExecuteAsync<int>(sql, parameters));
        await db.CloseAsync();
        Assert.Equal(1, await db.ExecuteAsync<int>(sql, parameters));
        db.Close();
        Assert.Equal(1, db.Execute<int>(sql, parameters));
        Assert.Equal(0, db.Execute<int>("SELECT @upper COLLATE NOCASE = @lower", parameters));
        Assert.Equal("sumar\u00c1", db.Execute<string>("SELECT lower(@upper)", parameters));
    }

    private static Task<OrderListData> SearchAsync(ManagerRepository repository, string? couponCode, string? shippingProvider,
        string pageSize = "30", string paymentProvider = "", string productSku = "", string trackingSource = "")
        => repository.SearchOrdersAsync(OrderDatabase.OrderDate, OrderDatabase.OrderDate, "", "main", "AllOrders", paymentProvider,
            productSku, trackingSource, "", "", "", "", "", "1", pageSize, couponCode, shippingProvider);

    private static string GenerateWhereClause(ManagerRepository repository, string query, string? couponCode = null,
        string? shippingProvider = null, string paymentProvider = "", string productSku = "", string trackingSource = "")
    {
        MethodInfo method = typeof(ManagerRepository).GetMethod(
            "GenerateWhereClause",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        return (string)method.Invoke(repository, new object?[]
        {
            string.Empty,
            query,
            string.Empty,
            paymentProvider,
            productSku,
            trackingSource,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            couponCode,
            shippingProvider,
        })!;
    }

    private static ManagerRepository CreateRepository(bool sqlite = true)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoDbDSN"] = "Data Source=:memory:",
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = sqlite ? "Microsoft.Data.Sqlite" : "Microsoft.Data.SqlClient",
            })
            .Build();

        return new ManagerRepository(
            NullLogger<ManagerRepository>.Instance,
            new Configuration(configuration),
            new DatabaseFactory(configuration, Mock.Of<IHostEnvironment>(x => x.ContentRootPath == AppContext.BaseDirectory)),
            Mock.Of<IStoreService>());
    }

    private sealed class OrderDatabase : IDisposable
    {
        public static readonly DateTime OrderDate = new(2026, 1, 15);
        private readonly SqliteConnection _connection;
        private readonly DatabaseFactory _factory;

        public OrderDatabase()
        {
            string connectionString = $"Data Source=manager-filters-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False";
            _connection = new SqliteConnection(connectionString);
            _connection.Open();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoDbDSN"] = connectionString,
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
            }).Build();
            _factory = new DatabaseFactory(configuration, Mock.Of<IHostEnvironment>(x => x.ContentRootPath == AppContext.BaseDirectory));
            var store = Mock.Of<IStore>(x => x.Currency == new CurrencyModel { CurrencyValue = "en-US" });
            var stores = new Mock<IStoreService>();
            stores.Setup(x => x.GetStoreByAlias("main")).Returns(store);
            Repository = new ManagerRepository(NullLogger<ManagerRepository>.Instance, new Configuration(configuration), _factory, stores.Object);
            using var db = _factory.GetDatabase();
            db.CreateTable<OrderData>();
        }

        public ManagerRepository Repository { get; }
        public DateTime Date => OrderDate;

        public Guid Add(string? coupon, Guid shippingProvider, decimal amount, string? lineCoupon = null, Guid? paymentProvider = null)
        {
            Guid id = Guid.NewGuid();
            using var db = _factory.GetDatabase();
            db.Insert(new OrderData
            {
                UniqueId = id,
                OrderStatusCol = "Incomplete",
                StoreAlias = "main",
                Currency = "en-US",
                CreateDate = OrderDate.AddHours(12),
                TotalAmount = amount,
                OrderInfo = JsonSerializer.Serialize(new
                {
                    Coupon = coupon,
                    ShippingProvider = new { Key = shippingProvider },
                    PaymentProvider = new { Key = paymentProvider },
                    Tracking = new { Source = "email" },
                    OrderLines = new[] { new { Coupon = lineCoupon, Product = new { SKU = "SKU" } } },
                }),
            });
            return id;
        }

        public void Dispose() => _connection.Dispose();
    }
}

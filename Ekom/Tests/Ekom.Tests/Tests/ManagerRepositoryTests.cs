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
    public void GenerateWhereClause_UsesParameterizedExactGiftcardCode(bool sqlite)
    {
        const string giftcardCode = " GIFT%_' OR 1=1 -- ";

        string whereClause = GenerateWhereClause(CreateRepository(sqlite), string.Empty, giftcardCode: giftcardCode);

        Assert.Contains(sqlite
            ? "EXISTS (SELECT 1 FROM json_each(OrderInfo, '$.Giftcards') AS giftcards WHERE lower(json_extract(giftcards.value, '$.Code')) = lower(@giftcardCode))"
            : "EXISTS (SELECT 1 FROM OPENJSON(OrderInfo, '$.Giftcards') WITH (Code nvarchar(max) '$.Code') AS giftcards WHERE LOWER(giftcards.Code) = LOWER(@giftcardCode))",
            whereClause, StringComparison.Ordinal);
        Assert.DoesNotContain(giftcardCode, whereClause, StringComparison.Ordinal);
        Assert.DoesNotContain("LIKE", whereClause, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, null)]
    [InlineData(true, "")]
    [InlineData(false, "")]
    [InlineData(true, " \t ")]
    [InlineData(false, " \t ")]
    public void GenerateWhereClause_IgnoresBlankGiftcardCode(bool sqlite, string? giftcardCode)
    {
        string whereClause = GenerateWhereClause(CreateRepository(sqlite), string.Empty, giftcardCode: giftcardCode);

        Assert.DoesNotContain("@giftcardCode", whereClause, StringComparison.Ordinal);
        Assert.DoesNotContain("Giftcards", whereClause, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GenerateWhereClause_CombinesNewAndExistingFilters(bool sqlite)
    {
        string whereClause = GenerateWhereClause(CreateRepository(sqlite), "customer", "SAVE", Guid.NewGuid().ToString(),
            paymentProvider: Guid.NewGuid().ToString(), productSku: "SKU", trackingSource: "newsletter", giftcardCode: "GIFT");

        Assert.Contains("@query", whereClause, StringComparison.Ordinal);
        Assert.Contains("@paymentProvider", whereClause, StringComparison.Ordinal);
        Assert.Contains("@productSku", whereClause, StringComparison.Ordinal);
        Assert.Contains("@trackingSource", whereClause, StringComparison.Ordinal);
        Assert.Contains("@couponCode", whereClause, StringComparison.Ordinal);
        Assert.Contains("@shippingProvider", whereClause, StringComparison.Ordinal);
        Assert.Contains("@giftcardCode", whereClause, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAndExport_FilterGiftcardsOncePerOrderWithConsistentTotals()
    {
        using var database = new OrderDatabase();
        Guid shippingProvider = Guid.NewGuid();
        Guid paymentProvider = Guid.NewGuid();
        Guid first = database.Add("SAVE", shippingProvider, 10m, paymentProvider: paymentProvider,
            giftcardCodes: new[] { "other", "Gift123", "GIFT123" });
        Guid second = database.Add("SAVE", shippingProvider, 30m, paymentProvider: paymentProvider,
            giftcardCodes: new[] { "gift123" });
        database.Add("SAVE", shippingProvider, 100m, giftcardCodes: new[] { "Gift123-extra" });
        database.Add("SAVE", shippingProvider, 200m, giftcardCodes: Array.Empty<string>());
        database.Add("Gift123", shippingProvider, 300m);
        database.Add("SAVE", Guid.NewGuid(), 400m, giftcardCodes: new[] { "Gift123" });

        OrderListData giftcardOnly = await SearchAsync(database.Repository, null, null, giftcardCode: " gift123 ");
        Assert.Equal(3, giftcardOnly.Count);

        OrderListData results = await SearchAsync(database.Repository, " save ", shippingProvider.ToString(), "1",
            giftcardCode: " gift123 ");

        Assert.Equal(second, Assert.Single(results.Orders).UniqueId);
        Assert.Equal(2, results.Count);
        Assert.Equal(2, results.TotalPages);
        Assert.Equal(40m.ToString("C", CultureInfo.GetCultureInfo("en-US")), results.GrandTotal);
        Assert.Equal(20m.ToString("C", CultureInfo.GetCultureInfo("en-US")), results.AverageAmount);

        OrderListData combined = await SearchAsync(database.Repository, " save ", shippingProvider.ToString(), "30",
            paymentProvider.ToString(), " sku ", " EMAIL ", giftcardCode: " gift123 ");
        Assert.Equal(2, combined.Count);
        Assert.Equal(new[] { second, first }, combined.Orders.Select(x => x.UniqueId));

        foreach (bool includeOrderInfo in new[] { false, true })
        {
            List<OrderData> export = await database.Repository.GetOrdersForExportAsync(database.Date, database.Date, "", "main", "AllOrders",
                paymentProvider.ToString(), " sku ", " EMAIL ", "", "", "", "", "", includeOrderInfo,
                couponCode: " save ", shippingProvider: shippingProvider.ToString(), giftcardCode: " gift123 ");

            Assert.Equal(new[] { second, first }, export.Select(x => x.UniqueId));
            Assert.All(export, x => Assert.Equal(includeOrderInfo, !string.IsNullOrEmpty(x.OrderInfo)));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public async Task SearchAndExport_IgnoreBlankGiftcardCode(string? giftcardCode)
    {
        using var database = new OrderDatabase();
        database.Add(null, Guid.NewGuid(), 10m, giftcardCodes: new[] { "GIFT" });
        database.Add(null, Guid.NewGuid(), 20m);
        database.Add(null, Guid.NewGuid(), 30m, giftcardCodes: Array.Empty<string>());

        OrderListData results = await SearchAsync(database.Repository, null, null, giftcardCode: giftcardCode);
        Assert.Equal(3, results.Count);
        Assert.Equal(3, results.Orders.Count());

        List<OrderData> export = await database.Repository.GetOrdersForExportAsync(database.Date, database.Date, "", "main", "AllOrders",
            "", "", "", "", "", "", "", "", false, giftcardCode: giftcardCode);
        Assert.Equal(3, export.Count);
    }

    [Theory]
    [InlineData(" gift%_' or 1=1 -- ", true)]
    [InlineData("missing", false)]
    [InlineData("gift", false)]
    public async Task SearchAndExport_MatchGiftcardCodeLiterally(string giftcardCode, bool matches)
    {
        using var database = new OrderDatabase();
        Guid literal = database.Add(null, Guid.NewGuid(), 10m, giftcardCodes: new[] { "GIFT%_' OR 1=1 --" });
        database.Add(null, Guid.NewGuid(), 20m, giftcardCodes: new[] { "GIFTanything" });
        database.Add(null, Guid.NewGuid(), 30m);

        OrderListData results = await SearchAsync(database.Repository, null, null, giftcardCode: giftcardCode);
        List<OrderData> export = await database.Repository.GetOrdersForExportAsync(database.Date, database.Date, "", "main", "AllOrders",
            "", "", "", "", "", "", "", "", false, giftcardCode: giftcardCode);

        Assert.Equal(matches ? 1 : 0, results.Count);
        if (matches)
        {
            Assert.Equal(literal, Assert.Single(results.Orders).UniqueId);
            Assert.Equal(literal, Assert.Single(export).UniqueId);
        }
        else
        {
            Assert.Empty(results.Orders);
            Assert.Empty(export);
        }
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

    [Fact]
    public async Task MostSoldProducts_SumsSavedLineAmountsWithoutMultiplyingQuantityAgain()
    {
        using var database = new OrderDatabase();
        database.Add(null, Guid.Empty, 25m);
        database.Add(null, Guid.Empty, 12.5m);

        var result = await database.Repository.MostSoldProductsPaged(
            database.Date, database.Date, "main", "AllOrders", 1, 20);

        var product = Assert.Single(result.Products);
        Assert.Equal(4m, product.ProductCount);
        Assert.Equal(37.5m, product.Total);
        Assert.Equal(1, result.Count);
    }

    [Fact]
    public async Task MostSoldProducts_GroupsProductsAndPaginatesByQuantity()
    {
        using var database = new OrderDatabase();
        // Saved amounts already reflect discounts and tax; quantity must not multiply them again.
        database.Add(null, Guid.Empty, 15m, productId: 1, quantity: 3m);
        database.Add(null, Guid.Empty, 7.5m, productId: 1, quantity: 1.5m);
        database.Add(null, Guid.Empty, 100m, productId: 2, quantity: 1m);

        var firstPage = await database.Repository.MostSoldProductsPaged(
            database.Date, database.Date, "main", "AllOrders", 1, 1);
        var secondPage = await database.Repository.MostSoldProductsPaged(
            database.Date, database.Date, "main", "AllOrders", 2, 1);
        var first = Assert.Single(firstPage.Products);
        var second = Assert.Single(secondPage.Products);
        Assert.Equal(1, first.Id);
        Assert.Equal(4.5m, first.ProductCount);
        Assert.Equal(22.5m, first.Total);
        Assert.Equal(2, second.Id);
        Assert.Equal(100m, second.Total);
        Assert.Equal(2, firstPage.Count);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(2, secondPage.Page);
    }

    private static Task<OrderListData> SearchAsync(ManagerRepository repository, string? couponCode, string? shippingProvider,
        string pageSize = "30", string paymentProvider = "", string productSku = "", string trackingSource = "", string? giftcardCode = null)
        => repository.SearchOrdersAsync(OrderDatabase.OrderDate, OrderDatabase.OrderDate, "", "main", "AllOrders", paymentProvider,
            productSku, trackingSource, "", "", "", "", "", "1", pageSize, couponCode, shippingProvider, giftcardCode);

    private static string GenerateWhereClause(ManagerRepository repository, string query, string? couponCode = null,
        string? shippingProvider = null, string paymentProvider = "", string productSku = "", string trackingSource = "", string? giftcardCode = null)
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
            giftcardCode,
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

        public Guid Add(string? coupon, Guid shippingProvider, decimal amount, string? lineCoupon = null, Guid? paymentProvider = null,
            string[]? giftcardCodes = null, int productId = 1, decimal quantity = 2m)
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
                    Giftcards = giftcardCodes?.Select(code => new { Code = code }).ToArray(),
                    ShippingProvider = new { Key = shippingProvider },
                    PaymentProvider = new { Key = paymentProvider },
                    Tracking = new { Source = "email" },
                    OrderLines = new[] { new { Coupon = lineCoupon, Product = new { Id = productId, SKU = "SKU", Title = "Product" }, Quantity = quantity, Amount = new { Value = amount } } },
                }),
            });
            return id;
        }

        public void Dispose() => _connection.Dispose();
    }
}

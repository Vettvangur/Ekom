using Ekom.Models;
using Ekom.Services;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class OrderPerformanceIndexMigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FreshAndExistingTablesReceiveTheSamePerformanceIndexes(bool existing)
    {
        using var fixture = new Fixture();
        if (existing)
        {
            using var db = fixture.Factory.GetDatabase();
            db.CreateTable<OrderData>();
            db.CreateTable<OrderActivityLog>();
            fixture.Service.EnsureOrderIndexes();
            db.Insert(new OrderData
            {
                UniqueId = Guid.NewGuid(),
                StoreAlias = "main",
                CustomerId = 42,
                CustomerUsername = "customer",
                OrderStatusCol = "Dispatched",
                OrderInfo = "preserved payload",
            });
            fixture.Service.EnsureOrderPerformanceIndexes();
        }
        else
        {
            fixture.Service.CreateTables();
        }

        fixture.AssertIndexes();
        string[] schema = fixture.Schema();
        fixture.Service.CreateTables();
        fixture.Service.EnsureOrderPerformanceIndexes();
        Assert.Equal(schema, fixture.Schema());
        using var check = fixture.Factory.GetDatabase();
        Assert.Equal(existing ? 1 : 0, check.OrderData.Count());
        if (existing)
        {
            Assert.Equal("preserved payload", check.OrderData.Single().OrderInfo);
        }
    }

    [Fact]
    public void StatusColumnsUpgradeLegacySqliteTableIdempotently()
    {
        using var fixture = new Fixture();
        using var db = fixture.Factory.GetDatabase();
        db.Execute("CREATE TABLE EkomOrders (ReferenceId INTEGER PRIMARY KEY, OrderStatusCol TEXT, OrderInfo TEXT)");
        db.Execute("INSERT INTO EkomOrders VALUES (1, 'ReadyForDispatch', 'preserved payload')");
        fixture.Service.EnsureOrderStateColumns();
        fixture.Service.EnsureOrderStateColumns();
        Assert.Equal(0, db.Execute<int>("SELECT StatusMappingVersion FROM EkomOrders WHERE ReferenceId = 1"));
        Assert.Equal("ReadyForDispatch", db.Execute<string>("SELECT OrderStatusCol FROM EkomOrders WHERE ReferenceId = 1"));
        Assert.Equal("preserved payload", db.Execute<string>("SELECT OrderInfo FROM EkomOrders WHERE ReferenceId = 1"));
        Assert.Equal(1, db.Execute<int>("SELECT COUNT(*) FROM pragma_table_info('EkomOrders') WHERE name = 'FulfilledQuantitiesJson'"));
    }

    [Fact]
    public void LaterIndexConflictLeavesEarlierIndexesAndCanBeRetriedAfterRepair()
    {
        using var fixture = new Fixture();
        using var db = fixture.Factory.GetDatabase();
        db.CreateTable<OrderData>();
        db.CreateTable<OrderActivityLog>();
        fixture.Service.EnsureOrderIndexes();
        db.Execute("CREATE INDEX IX_EkomOrdersActivityLog_Key_Date ON EkomOrdersActivityLog ([Date], [Key])");

        Assert.Throws<InvalidOperationException>(() => fixture.Service.EnsureOrderPerformanceIndexes());

        Assert.Equal(new[] { "CustomerId" }, db.Query<string>("SELECT name FROM pragma_index_info('IX_EkomOrders_CustomerId') ORDER BY seqno").ToArray());
        Assert.Equal(new[] { "CustomerUsername" }, db.Query<string>("SELECT name FROM pragma_index_info('IX_EkomOrders_CustomerUsername') ORDER BY seqno").ToArray());
        Assert.Equal(new[] { "Date", "Key" }, db.Query<string>("SELECT name FROM pragma_index_info('IX_EkomOrdersActivityLog_Key_Date') ORDER BY seqno").ToArray());
        // Repair only this isolated fixture's deliberate conflicting definition.
        db.Execute("DROP INDEX IX_EkomOrdersActivityLog_Key_Date");

        fixture.Service.EnsureOrderPerformanceIndexes();

        fixture.AssertIndexes();
    }

    [Fact]
    public void MissingPerformanceIndexIsRepairedWithoutReplacingTheOthers()
    {
        using var fixture = new Fixture();
        fixture.Service.CreateTables();
        using var db = fixture.Factory.GetDatabase();
        db.Execute("DROP INDEX IX_EkomOrdersActivityLog_Key_Date");
        string[] before = fixture.Schema();

        fixture.Service.EnsureOrderPerformanceIndexes();

        fixture.AssertIndexes();
        Assert.All(before, definition => Assert.Contains(definition, fixture.Schema()));
    }

    [Fact]
    public void LookupIndexesAreAddedToFreshAndExistingTablesWithoutChangingData()
    {
        using var fixture = new Fixture();
        fixture.Service.CreateTables();
        using var db = fixture.Factory.GetDatabase();

        string[] Keys(string name) => db.Query<string>("SELECT name FROM pragma_index_info(@indexName) ORDER BY seqno", new DataParameter("indexName", name)).ToArray();
        void AssertLookupIndexes()
        {
            Assert.Equal(new[] { "CouponCode" }, Keys("IX_EkomCoupon_CouponCode"));
            Assert.Equal(new[] { "DiscountId", "CouponCode" }, Keys("IX_EkomCoupon_DiscountId_CouponCode"));
            Assert.Equal(new[] { "CouponKey" }, Keys("IX_EkomCoupon_CouponKey"));
            Assert.Equal(new[] { "OrderId" }, Keys("IX_EkomCheckoutPaymentAttempt_OrderId"));
            Assert.Equal(new[] { "OrderId" }, Keys("IX_EkomStockReservation_OrderId"));
        }

        AssertLookupIndexes();

        // A table from before this migration: rows present, lookup indexes absent.
        foreach (string index in new[] { "IX_EkomCoupon_CouponCode", "IX_EkomCoupon_DiscountId_CouponCode", "IX_EkomCoupon_CouponKey", "IX_EkomCheckoutPaymentAttempt_OrderId", "IX_EkomStockReservation_OrderId" })
        {
            db.Execute($"DROP INDEX [{index}]");
        }
        var discount = Guid.NewGuid();
        db.Insert(new CouponData { CouponCode = "SPRING", NumberAvailable = 3, CouponKey = Guid.NewGuid(), DiscountId = discount, Date = new DateTime(2026, 1, 5) });
        Assert.Empty(Keys("IX_EkomCoupon_CouponCode"));

        fixture.Service.EnsureLookupIndexes();
        AssertLookupIndexes();
        var coupon = Assert.Single(db.CouponData);
        Assert.Equal("SPRING", coupon.CouponCode);
        Assert.Equal(discount, coupon.DiscountId);
        Assert.Equal(3, coupon.NumberAvailable);

        string[] schema = db.Query<string>("SELECT name || ':' || sql FROM sqlite_master WHERE type = 'index' AND sql IS NOT NULL ORDER BY name").ToArray();
        fixture.Service.EnsureLookupIndexes();
        fixture.Service.CreateTables();
        Assert.Equal(schema, db.Query<string>("SELECT name || ':' || sql FROM sqlite_master WHERE type = 'index' AND sql IS NOT NULL ORDER BY name").ToArray());
    }

    [Fact]
    public void ALookupIndexThatCannotBeCreatedIsSkippedAndTheOthersStillInstall()
    {
        using var fixture = new Fixture();
        fixture.Service.CreateTables();
        using var db = fixture.Factory.GetDatabase();
        db.Execute("DROP INDEX [IX_EkomCoupon_CouponKey]");
        db.Execute("DROP INDEX [IX_EkomCoupon_CouponCode]");
        // The name is taken by an index on another column, which the installer refuses to replace.
        db.Execute("CREATE INDEX [IX_EkomCoupon_CouponKey] ON [EkomCoupon] ([NumberAvailable])");

        fixture.Service.EnsureLookupIndexes();

        Assert.Equal(new[] { "NumberAvailable" }, db.Query<string>("SELECT name FROM pragma_index_info('IX_EkomCoupon_CouponKey') ORDER BY seqno").ToArray());
        Assert.Equal(new[] { "CouponCode" }, db.Query<string>("SELECT name FROM pragma_index_info('IX_EkomCoupon_CouponCode') ORDER BY seqno").ToArray());
        fixture.Service.CreateTables();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly SqliteConnection _connection;

        public Fixture()
        {
            string connectionString = $"Data Source=performance-migration-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False";
            _connection = new SqliteConnection(connectionString);
            _connection.Open();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoDbDSN"] = connectionString,
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
            }).Build();
            Factory = new DatabaseFactory(configuration, Mock.Of<IHostEnvironment>(x => x.ContentRootPath == AppContext.BaseDirectory));
            Service = new DatabaseService(Factory, NullLogger<DatabaseService>.Instance, new StockReservationReadiness());
        }

        public DatabaseFactory Factory { get; }
        public DatabaseService Service { get; }

        public string[] Schema()
        {
            using var db = Factory.GetDatabase();
            return db.Query<string>("SELECT name || ':' || sql FROM sqlite_master WHERE type = 'index' AND sql IS NOT NULL AND tbl_name IN ('EkomOrders', 'EkomOrdersActivityLog') ORDER BY name").ToArray();
        }

        public void AssertIndexes()
        {
            using var db = Factory.GetDatabase();
            Assert.Equal(new[] { "CustomerId" }, Keys(db, "IX_EkomOrders_CustomerId"));
            Assert.Equal(new[] { "CustomerUsername" }, Keys(db, "IX_EkomOrders_CustomerUsername"));
            Assert.Equal(new[] { "Key", "Date" }, Keys(db, "IX_EkomOrdersActivityLog_Key_Date"));
            Assert.Equal(1, db.Execute<int>("SELECT [unique] FROM pragma_index_list('EkomOrders') WHERE name = 'IX_EkomOrders_UniqueId'"));
            Assert.Equal(0, db.Execute<int>("SELECT COUNT(*) FROM pragma_index_list('EkomOrders') WHERE name IN ('IX_EkomOrders_StoreAlias_CreateDate', 'IX_EkomOrders_StoreAlias_PaidDate')"));
        }

        private static string[] Keys(LinqToDB.Data.DataConnection db, string name)
            => db.Query<string>("SELECT name FROM pragma_index_info(@indexName) ORDER BY seqno", new DataParameter("indexName", name)).ToArray();

        public void Dispose() => _connection.Dispose();
    }
}

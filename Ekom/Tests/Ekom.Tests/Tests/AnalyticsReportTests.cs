using Ekom.Analytics;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using LinqToDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class AnalyticsReportTests
{
    [Theory]
    [InlineData("", "USD")]
    [InlineData("main", "")]
    [InlineData(" ", "USD")]
    public async Task StoreAndCurrencyAreRequired(string store, string currency)
    {
        using var fixture = new AnalyticsReportDatabase();
        var filter = fixture.Filter();
        filter.Store = store;
        filter.Currency = currency;
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Reports.SalesAsync(filter));
    }

    [Fact]
    public async Task SalesAreStoreAndCurrencyIsolatedAndAlwaysExcludeIncompleteOrders()
    {
        using var fixture = new AnalyticsReportDatabase();
        await fixture.InitializeAsync();
        fixture.AddOrder(100);
        var foreignStore = fixture.NewOrder(200);
        foreignStore.StoreAlias = "foreign";
        fixture.Insert(foreignStore);
        var foreignCurrency = fixture.NewOrder(300);
        foreignCurrency.CurrencyCode = "EUR";
        fixture.Insert(foreignCurrency);
        var incomplete = fixture.NewOrder(400);
        incomplete.OrderStatus = "Incomplete";
        fixture.Insert(incomplete);
        var filter = fixture.Filter();
        filter.Status = "AllOrders";

        var report = await fixture.Reports.SalesAsync(filter);
        var orders = await fixture.Reports.OrdersAsync(filter);

        Assert.Equal(1, report.Totals.Orders);
        Assert.Equal(100m, report.Totals.Sales);
        Assert.Equal(1, orders.Total);
        Assert.Equal("main", Assert.Single(orders.Items).StoreAlias);
    }

    [Fact]
    public async Task GiftCardSalesUseGrandTotalAndOrderTotalsAreNotMultipliedByLines()
    {
        using var fixture = new AnalyticsReportDatabase();
        await fixture.InitializeAsync();
        var order = fixture.NewOrder(120);
        order.GrandTotalWithoutVat = 100;
        order.ChargedAmount = 0;
        order.MerchandiseTotalWithVat = 100;
        order.ShippingAmountWithVat = 15;
        order.PaymentFeeWithVat = 5;
        order.TotalQuantity = 3;
        order.DiscountAmount = 10;
        fixture.Insert(order);
        fixture.AddLine(order.OrderId, Guid.NewGuid(), Guid.NewGuid(), 40, 1);
        fixture.AddLine(order.OrderId, Guid.NewGuid(), Guid.NewGuid(), 60, 2);

        var totals = (await fixture.Reports.SalesAsync(fixture.Filter())).Totals;

        Assert.Equal(1, totals.Orders);
        Assert.Equal(120m, totals.Sales);
        Assert.Equal(100m, totals.SalesWithoutVat);
        Assert.Equal(0m, totals.ChargedAmount);
        Assert.Equal(100m, totals.MerchandiseSales);
        Assert.Equal(15m, totals.Shipping);
        Assert.Equal(5m, totals.PaymentFees);
        Assert.Equal(3m, totals.Items);
        Assert.Equal(10m, totals.SavedDiscountAmount);
        Assert.Equal(120m, totals.AverageOrder);
    }

    [Fact]
    public async Task ProductsAggregateVariantsAndRevenueSharesUseAllFilteredMerchandise()
    {
        using var fixture = new AnalyticsReportDatabase();
        await fixture.InitializeAsync();
        var order = fixture.AddOrder(110);
        var product = Guid.NewGuid();
        var firstVariant = Guid.NewGuid();
        fixture.AddLine(order.OrderId, product, firstVariant, 20, 1);
        fixture.AddLine(order.OrderId, product, firstVariant, 20, 1);
        fixture.AddLine(order.OrderId, product, Guid.NewGuid(), 40, 2);
        fixture.AddLine(order.OrderId, Guid.NewGuid(), null, 20, 1);
        var foreign = fixture.NewOrder(900);
        foreign.StoreAlias = "foreign";
        fixture.Insert(foreign);
        fixture.AddLine(foreign.OrderId, product, firstVariant, 900, 1);

        var products = await fixture.Reports.ProductsAsync(fixture.Filter(), false, pageSize: 1);
        var aggregate = Assert.Single(products.Items);
        Assert.Equal(2, products.Total);
        Assert.Equal(product, aggregate.ProductKey);
        Assert.Null(aggregate.VariantKey);
        Assert.Equal(80m, aggregate.Sales);
        Assert.Equal(4m, aggregate.Quantity);
        Assert.Equal(0.8m, aggregate.MerchandiseRevenueShare);
        var variants = await fixture.Reports.ProductsAsync(fixture.Filter(), true);
        Assert.Equal(3, variants.Total);
        Assert.Equal(1m, variants.Items.Sum(x => x.MerchandiseRevenueShare));
        var variant = Assert.Single(variants.Items, x => x.VariantKey == firstVariant);
        Assert.Equal(40m, variant.Sales);
        Assert.Equal(2m, variant.Quantity);
    }

    [Fact]
    public async Task DuplicateCouponApplicationsCountEachOrderSalesOnlyOnce()
    {
        using var fixture = new AnalyticsReportDatabase();
        await fixture.InitializeAsync();
        var first = fixture.AddOrder(100);
        var second = fixture.AddOrder(50);
        var discount = Guid.NewGuid();
        foreach (var order in new[] { first, first, first, second })
        {
            fixture.Insert(new AnalyticsPromotionData
            {
                ApplicationId = Guid.NewGuid(), OrderId = order.OrderId,
                OrderLineKey = Guid.NewGuid(), Scope = "Line", DiscountKey = discount,
                CouponCode = "SAVE", Title = "Saved coupon",
            });
        }

        var promotions = await fixture.Reports.PromotionsAsync(fixture.Filter());

        var promotion = Assert.Single(promotions.Items);
        Assert.Equal(1, promotions.Total);
        Assert.Equal(2, promotion.Orders);
        Assert.Equal(150m, promotion.AssociatedSales);
    }

    [Fact]
    public async Task ProductAndVariantGuidsTakePriorityOverChangingNumericIds()
    {
        using var fixture = new AnalyticsReportDatabase();
        await fixture.InitializeAsync();
        var order = fixture.AddOrder(100);
        var productKey = Guid.NewGuid();
        var variantKey = Guid.NewGuid();
        for (var id = 1; id <= 4; id++)
        {
            fixture.Insert(new AnalyticsOrderLineData
            {
                OrderId = order.OrderId,
                OrderLineKey = Guid.NewGuid(),
                ProductKey = id <= 2 ? productKey : null,
                VariantKey = id <= 2 ? variantKey : null,
                ProductId = id,
                VariantId = id <= 2 ? id + 10 : null,
                TotalWithVat = 25,
                Quantity = 1,
            });
        }

        var result = await fixture.Reports.ProductsAsync(fixture.Filter(), true);

        Assert.Equal(3, result.Total);
        var stable = Assert.Single(result.Items, x => x.ProductKey == productKey);
        Assert.Equal(variantKey, stable.VariantKey);
        Assert.Equal(50m, stable.Sales);
        Assert.Equal(2m, stable.Quantity);
        Assert.Equal(2, result.Items.Count(x => x.ProductKey == null));
    }

    [Fact]
    public async Task MissingCustomerIdentityIsExcludedAndRepeatedIdentityCountsOnce()
    {
        using var fixture = new AnalyticsReportDatabase();
        await fixture.InitializeAsync();
        foreach (var identity in new string?[] { null, null, "customer-a", "customer-a", "customer-b" })
        {
            var order = fixture.NewOrder(10);
            order.CustomerIdentityKey = identity;
            fixture.Insert(order);
        }

        var report = await fixture.Reports.SalesAsync(fixture.Filter());
        var customerOrders = await fixture.Reports.OrdersAsync(fixture.Filter(), "customer-a");

        Assert.Equal(5, report.Totals.Orders);
        Assert.Equal(2, report.Customers);
        Assert.Equal(2, customerOrders.Total);
        Assert.All(customerOrders.Items, x => Assert.Equal("customer-a", x.CustomerIdentityKey));
    }

    [Theory]
    [InlineData("Created")]
    [InlineData("Paid")]
    public async Task DateBasisUsesInclusiveStartExclusiveEndAndEqualPreviousPeriod(string basis)
    {
        using var fixture = new AnalyticsReportDatabase();
        await fixture.InitializeAsync();
        var filter = fixture.Filter();
        filter.DateBasis = basis;
        var previousStart = filter.Start - (filter.End - filter.Start);
        var dates = new[] { previousStart.AddSeconds(-1), previousStart, filter.Start.AddSeconds(-1), filter.Start, filter.End.AddSeconds(-1), filter.End };
        for (var i = 0; i < dates.Length; i++)
        {
            var order = fixture.NewOrder(i + 1);
            order.CreateDate = basis == "Created" ? dates[i] : filter.End.AddDays(10);
            order.PaidDate = basis == "Paid" ? dates[i] : null;
            fixture.Insert(order);
        }
        if (basis == "Paid") fixture.AddOrder(1000);

        var report = await fixture.Reports.SalesAsync(filter);
        var daily = await fixture.Reports.DailySalesAsync(filter);

        Assert.Equal(2, report.Totals.Orders);
        Assert.Equal(9m, report.Totals.Sales);
        Assert.Equal(2, report.PreviousPeriod.Orders);
        Assert.Equal(5m, report.PreviousPeriod.Sales);
        Assert.Equal(2, daily.Sum(x => x.Orders));
        Assert.Equal(9m, daily.Sum(x => x.Sales));
        Assert.All(daily, x => Assert.InRange(x.Date, filter.Start.Date, filter.End.AddTicks(-1).Date));
    }

    [Theory]
    [InlineData("payment")]
    [InlineData("shipping")]
    [InlineData("shipping-method")]
    public async Task NullProvidersRemainInDistributions(string dimension)
    {
        using var fixture = new AnalyticsReportDatabase();
        await fixture.InitializeAsync();
        fixture.AddOrder(10);
        var known = fixture.NewOrder(20);
        known.PaymentProviderKey = Guid.NewGuid();
        known.PaymentProviderTitle = "Card";
        known.ShippingProviderKey = Guid.NewGuid();
        known.ShippingProviderTitle = "Courier";
        known.ShippingMethod = "Delivery";
        fixture.Insert(known);

        var distribution = await fixture.Reports.DistributionAsync(fixture.Filter(), dimension);

        Assert.Equal(2, distribution.Count);
        Assert.Equal(2, distribution.Sum(x => x.Orders));
        Assert.Equal(1m, distribution.Sum(x => x.Share));
        Assert.Single(distribution, x => string.IsNullOrEmpty(x.Key));
        Assert.All(distribution, x => Assert.Equal(0.5m, x.Share));
    }
}

internal sealed class AnalyticsReportDatabase : IDisposable
{
    private readonly string _path;
    private int _reference;
    public DatabaseFactory Factory { get; }
    public IOptions<AnalyticsOptions> Options { get; }
    public AnalyticsSchema Schema { get; }
    public AnalyticsReportRepository Reports { get; }

    public AnalyticsReportDatabase()
    {
        var directory = Path.Combine(Path.GetTempPath(), "opencode");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, $"ekom-analytics-reports-{Guid.NewGuid():N}.sqlite");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:umbracoDbDSN"] = $"Data Source={_path};Pooling=False;Default Timeout=2",
            ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
        }).Build();
        Factory = new DatabaseFactory(configuration, Mock.Of<IHostEnvironment>(x => x.ContentRootPath == directory));
        Options = Microsoft.Extensions.Options.Options.Create(new AnalyticsOptions { Enabled = true });
        Schema = new AnalyticsSchema(Factory, Options, NullLogger<AnalyticsSchema>.Instance);
        Reports = new AnalyticsReportRepository(Factory, Schema, Options);
    }

    public async Task InitializeAsync()
    {
        Assert.True(await Schema.EnsureReadyAsync());
    }

    public AnalyticsReportFilter Filter() => new()
    {
        Store = "main", Currency = "USD",
        Start = new DateTime(2026, 1, 10), End = new DateTime(2026, 1, 12),
    };

    public AnalyticsOrderData NewOrder(decimal sales) => new()
    {
        OrderId = Guid.NewGuid(), ReferenceId = ++_reference,
        StoreAlias = "main", CurrencyCode = "USD", OrderStatus = "ReadyForDispatch",
        CreateDate = Filter().Start, GrandTotal = sales, ChargedAmount = sales,
        ProjectedAtUtc = new DateTime(2026, 2, 1),
    };

    public AnalyticsOrderData AddOrder(decimal sales)
    {
        var order = NewOrder(sales);
        Insert(order);
        return order;
    }

    public void AddLine(Guid orderId, Guid productKey, Guid? variantKey, decimal sales, decimal quantity)
        => Insert(new AnalyticsOrderLineData
        {
            OrderId = orderId, OrderLineKey = Guid.NewGuid(), ProductKey = productKey,
            VariantKey = variantKey, TotalWithVat = sales, Quantity = quantity,
            ProductTitle = "Product", ProductSku = "SKU",
        });

    public void Insert<T>(T item) where T : class
    {
        using var db = Factory.GetDatabase();
        db.Insert(item);
    }

    public AnalyticsProjectionService Projection() => new(Factory, Schema,
        new AnalyticsSnapshotMapper(new EmailAnalyticsCustomerIdentityResolver(), Options), Options,
        new ActivityLogRepository(NullLogger<ActivityLogRepository>.Instance, Factory),
        NullLogger<AnalyticsProjectionService>.Instance);

    public void Dispose() => File.Delete(_path);
}

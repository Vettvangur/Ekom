using Ekom.Repositories;
using Ekom.Services;
using Ekom.Utilities;
using LinqToDB;
using Microsoft.Extensions.Options;

namespace Ekom.Analytics;

/// <summary>Reads reporting facts only; never expands operational order JSON.</summary>
public sealed class AnalyticsReportRepository
{
    private static readonly string[] CompletedStatuses =
    [
        nameof(OrderStatus.ReadyForDispatch), nameof(OrderStatus.OfflinePayment),
        nameof(OrderStatus.ReadyForDispatchWhenStockArrives), nameof(OrderStatus.Dispatched),
        nameof(OrderStatus.Closed), nameof(OrderStatus.ReadyForPickup),
    ];

    private readonly DatabaseFactory _database;
    private readonly AnalyticsSchema _schema;
    private readonly AnalyticsOptions _options;

    public AnalyticsReportRepository(DatabaseFactory database, AnalyticsSchema schema, IOptions<AnalyticsOptions> options)
    {
        _database = database;
        _schema = schema;
        _options = options.Value;
    }

    private async Task<DbContext> OpenAsync(AnalyticsReportFilter filter, CancellationToken ct)
    {
        filter.Validate();
        if (!_options.Enabled || !_options.IsValid(out _) || !await _schema.EnsureReadyAsync(ct).ConfigureAwait(false))
            throw new InvalidOperationException("Analytics is disabled or unavailable.");
        var db = _database.GetDatabase();
        db.CommandTimeout = _options.DatabaseCommandTimeoutSeconds;
        return db;
    }

    private static IQueryable<AnalyticsOrderData> Orders(DbContext db, AnalyticsReportFilter filter)
    {
        var query = db.GetTable<AnalyticsOrderData>().Where(x =>
            x.StoreAlias == filter.Store && x.CurrencyCode == filter.Currency &&
            x.OrderStatus != nameof(OrderStatus.Incomplete));
        query = filter.DateBasis == "Paid"
            ? query.Where(x => x.PaidDate >= filter.Start && x.PaidDate < filter.End)
            : query.Where(x => x.CreateDate >= filter.Start && x.CreateDate < filter.End);
        if (filter.Status == "CompletedOrders") query = query.Where(x => Enumerable.Contains(CompletedStatuses, x.OrderStatus));
        else if (filter.Status != "AllOrders") query = query.Where(x => x.OrderStatus == filter.Status);
        return query;
    }

    private static async Task<AnalyticsSalesTotals> TotalsAsync(IQueryable<AnalyticsOrderData> query, CancellationToken ct)
        => await query.GroupBy(_ => 1).Select(g => new AnalyticsSalesTotals
        {
            Orders = g.Count(),
            Sales = g.Sum(x => x.GrandTotal),
            SalesWithoutVat = g.Sum(x => x.GrandTotalWithoutVat),
            ChargedAmount = g.Sum(x => x.ChargedAmount),
            MerchandiseSales = g.Sum(x => x.MerchandiseTotalWithVat),
            Shipping = g.Sum(x => x.ShippingAmountWithVat),
            PaymentFees = g.Sum(x => x.PaymentFeeWithVat),
            Items = g.Sum(x => x.TotalQuantity),
            SavedDiscountAmount = g.Sum(x => x.DiscountAmount),
            SavedDiscountAmountWithoutVat = g.Sum(x => x.DiscountAmountWithoutVat),
        }).FirstOrDefaultAsync(ct).ConfigureAwait(false) ?? new AnalyticsSalesTotals();

    public async Task<AnalyticsSalesReport> SalesAsync(AnalyticsReportFilter filter, CancellationToken ct = default)
    {
        using var db = await OpenAsync(filter, ct).ConfigureAwait(false);
        var query = Orders(db, filter);
        var totals = await TotalsAsync(query, ct).ConfigureAwait(false);
        var previous = new AnalyticsReportFilter
        {
            Store = filter.Store, Currency = filter.Currency, Status = filter.Status, DateBasis = filter.DateBasis,
            Start = filter.Start - (filter.End - filter.Start), End = filter.Start,
        };
        var previousTotals = await TotalsAsync(Orders(db, previous), ct).ConfigureAwait(false);
        var customers = await query.Where(x => x.CustomerIdentityKey != null)
            .Select(x => x.CustomerIdentityKey).Distinct().CountAsync(ct).ConfigureAwait(false);
        var latest = await query.Select(x => (DateTime?)x.ProjectedAtUtc).MaxAsync(ct).ConfigureAwait(false);
        return new AnalyticsSalesReport(totals, previousTotals, customers, latest);
    }

    public async Task<IReadOnlyList<AnalyticsDailySales>> DailySalesAsync(AnalyticsReportFilter filter, CancellationToken ct = default)
    {
        using var db = await OpenAsync(filter, ct).ConfigureAwait(false);
        return await Orders(db, filter).GroupBy(x => filter.DateBasis == "Paid" ? x.PaidDate!.Value.Date : x.CreateDate.Date)
            .Select(g => new AnalyticsDailySales { Date = g.Key, Orders = g.Count(), Sales = g.Sum(x => x.GrandTotal) })
            .OrderBy(x => x.Date).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<AnalyticsPage<AnalyticsProductSales>> ProductsAsync(AnalyticsReportFilter filter, bool variants,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        ValidatePage(page, pageSize);
        using var db = await OpenAsync(filter, ct).ConfigureAwait(false);
        var lines = from order in Orders(db, filter)
                    join line in db.GetTable<AnalyticsOrderLineData>() on order.OrderId equals line.OrderId
                    select line;
        var grouped = lines.GroupBy(x => new
        {
            x.ProductKey,
            ProductId = x.ProductKey.HasValue ? (int?)null : x.ProductId,
            VariantKey = variants ? x.VariantKey : null,
            VariantId = variants && !x.VariantKey.HasValue ? x.VariantId : null,
        }).Select(g => new AnalyticsProductSales
        {
            ProductKey = g.Key.ProductKey, ProductId = g.Max(x => x.ProductId),
            VariantKey = g.Key.VariantKey, VariantId = variants ? g.Max(x => x.VariantId) : null,
            Title = g.Max(x => x.ProductTitle), Sku = g.Max(x => x.ProductSku),
            VariantTitle = variants ? g.Max(x => x.VariantTitle) : null,
            VariantSku = variants ? g.Max(x => x.VariantSku) : null,
            Quantity = g.Sum(x => x.Quantity), Sales = g.Sum(x => x.TotalWithVat),
        });
        var total = await grouped.CountAsync(ct).ConfigureAwait(false);
        var denominator = await lines.SumAsync(x => (decimal?)x.TotalWithVat, ct).ConfigureAwait(false) ?? 0;
        var items = await grouped.OrderByDescending(x => x.Sales).ThenBy(x => x.ProductKey).ThenBy(x => x.ProductId)
            .ThenBy(x => x.VariantKey).ThenBy(x => x.VariantId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct).ConfigureAwait(false);
        foreach (var item in items) item.MerchandiseRevenueShare = denominator == 0 ? 0 : item.Sales / denominator;
        return new AnalyticsPage<AnalyticsProductSales>(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<AnalyticsDistribution>> DistributionAsync(AnalyticsReportFilter filter, string dimension,
        CancellationToken ct = default)
    {
        if (dimension != "status" && dimension != "payment" && dimension != "shipping" && dimension != "shipping-method")
            throw new ArgumentException("Unknown distribution dimension.");
        using var db = await OpenAsync(filter, ct).ConfigureAwait(false);
        var query = Orders(db, filter);
        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query.GroupBy(x => new
        {
            Key = dimension == "status" ? x.OrderStatus : dimension == "payment" ? x.PaymentProviderKey.ToString() :
                dimension == "shipping-method" ? x.ShippingMethod : x.ShippingProviderKey.ToString(),
        }).Select(g => new AnalyticsDistribution
        {
            Key = g.Key.Key,
            Title = dimension == "status" || dimension == "shipping-method" ? g.Key.Key :
                dimension == "payment" ? g.Max(x => x.PaymentProviderTitle) : g.Max(x => x.ShippingProviderTitle),
            Orders = g.Count(),
        }).OrderByDescending(x => x.Orders).ToListAsync(ct).ConfigureAwait(false);
        foreach (var item in items) item.Share = total == 0 ? 0 : (decimal)item.Orders / total;
        return items;
    }

    public async Task<AnalyticsPage<AnalyticsPromotionSales>> PromotionsAsync(AnalyticsReportFilter filter,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        ValidatePage(page, pageSize);
        using var db = await OpenAsync(filter, ct).ConfigureAwait(false);
        // Deduplicate order/promotion pairs before summing order sales across line associations.
        var associations = from order in Orders(db, filter)
                     join promotion in db.GetTable<AnalyticsPromotionData>() on order.OrderId equals promotion.OrderId
                     select new { promotion.DiscountKey, promotion.CouponCode, promotion.Title, order.OrderId, order.GrandTotal };
        var pairs = associations.GroupBy(x => new { x.DiscountKey, x.CouponCode, x.OrderId, x.GrandTotal })
            .Select(g => new { g.Key.DiscountKey, g.Key.CouponCode, g.Key.OrderId, g.Key.GrandTotal, Title = g.Max(x => x.Title) });
        var grouped = pairs.GroupBy(x => new { x.DiscountKey, x.CouponCode }).Select(g => new AnalyticsPromotionSales
        {
            DiscountKey = g.Key.DiscountKey, CouponCode = g.Key.CouponCode,
            Title = g.Max(x => x.Title), Orders = g.Select(x => x.OrderId).Distinct().Count(),
            AssociatedSales = g.Sum(x => x.GrandTotal),
        });
        var total = await grouped.CountAsync(ct).ConfigureAwait(false);
        var items = await grouped.OrderByDescending(x => x.Orders).ThenBy(x => x.CouponCode).ThenBy(x => x.DiscountKey)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct).ConfigureAwait(false);
        return new AnalyticsPage<AnalyticsPromotionSales>(items, total, page, pageSize);
    }

    public async Task<AnalyticsPage<AnalyticsOrderData>> OrdersAsync(AnalyticsReportFilter filter,
        string? customerIdentityKey = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        ValidatePage(page, pageSize);
        using var db = await OpenAsync(filter, ct).ConfigureAwait(false);
        var query = Orders(db, filter);
        if (!string.IsNullOrWhiteSpace(customerIdentityKey)) query = query.Where(x => x.CustomerIdentityKey == customerIdentityKey);
        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query.OrderByDescending(x => x.ReferenceId).Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct).ConfigureAwait(false);
        return new AnalyticsPage<AnalyticsOrderData>(items, total, page, pageSize);
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize < 1 || pageSize > 200 || page > int.MaxValue / pageSize)
            throw new ArgumentException("Page must be positive and pageSize between 1 and 200.");
    }
}

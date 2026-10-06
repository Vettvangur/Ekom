using Ekom.Utilities;

namespace Ekom.Analytics;

public sealed class AnalyticsReportFilter
{
    public string Store { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string Status { get; set; } = "CompletedOrders";
    public string DateBasis { get; set; } = "Created";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Store) || string.IsNullOrWhiteSpace(Currency))
            throw new ArgumentException("Store and ISO currency are required.");
        if (Start == default || End <= Start)
            throw new ArgumentException("Supply a start and exclusive end date, with end after start.");
        if (DateBasis != "Created" && DateBasis != "Paid")
            throw new ArgumentException("DateBasis must be Created or Paid.");
        if (Status != "CompletedOrders" && Status != "AllOrders" &&
            (!Enum.TryParse<OrderStatus>(Status, out var status) || !Enum.IsDefined(status) || status == OrderStatus.Incomplete))
            throw new ArgumentException("Supply a valid status, CompletedOrders or AllOrders. Incomplete is excluded.");
    }
}

public sealed class AnalyticsSalesTotals
{
    public int Orders { get; set; }
    public decimal Sales { get; set; }
    public decimal SalesWithoutVat { get; set; }
    public decimal ChargedAmount { get; set; }
    public decimal MerchandiseSales { get; set; }
    public decimal Shipping { get; set; }
    public decimal PaymentFees { get; set; }
    public decimal Items { get; set; }
    public decimal SavedDiscountAmount { get; set; }
    public decimal SavedDiscountAmountWithoutVat { get; set; }
    public decimal AverageOrder => Orders == 0 ? 0 : Sales / Orders;
}

public sealed record AnalyticsSalesReport(
    AnalyticsSalesTotals Totals,
    AnalyticsSalesTotals PreviousPeriod,
    int Customers,
    DateTime? LastProjectionAtUtc);

public sealed class AnalyticsDailySales
{
    public DateTime Date { get; set; }
    public int Orders { get; set; }
    public decimal Sales { get; set; }
}

public sealed class AnalyticsProductSales
{
    public Guid? ProductKey { get; set; }
    public int? ProductId { get; set; }
    public Guid? VariantKey { get; set; }
    public int? VariantId { get; set; }
    public string? Title { get; set; }
    public string? Sku { get; set; }
    public string? VariantTitle { get; set; }
    public string? VariantSku { get; set; }
    public decimal Quantity { get; set; }
    public decimal Sales { get; set; }
    public decimal MerchandiseRevenueShare { get; set; }
}

public sealed class AnalyticsDistribution
{
    public string? Key { get; set; }
    public string? Title { get; set; }
    public int Orders { get; set; }
    public decimal Share { get; set; }
}

public sealed class AnalyticsPromotionSales
{
    public Guid? DiscountKey { get; set; }
    public string? CouponCode { get; set; }
    public string? Title { get; set; }
    public int Orders { get; set; }
    public decimal AssociatedSales { get; set; }
}

public sealed record AnalyticsPage<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

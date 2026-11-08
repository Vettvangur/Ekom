namespace Ekom.Models.Manager;

public sealed class CustomerSummary
{
    public string Identity { get; set; } = "";
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Username { get; set; }
    public string? ShippingCountry { get; set; }
    public DateTime FirstOrder { get; set; }
    public DateTime LastOrder { get; set; }
    public string? LastStatus { get; set; }
    public int Orders { get; set; }
    public int DaysSinceLastOrder => Math.Max(0, (DateTime.Now.Date - LastOrder.Date).Days);
    public double? AverageReorderDays => Orders > 1 ? (LastOrder - FirstOrder).TotalDays / (Orders - 1) : null;
    public IReadOnlyList<CustomerSpend> Spend { get; set; } = Array.Empty<CustomerSpend>();
}

public sealed class CustomerSpend
{
    public string Identity { get; set; } = "";
    public string Currency { get; set; } = "";
    public decimal Total { get; set; }
    public int Orders { get; set; }
    public int Customers { get; set; }
    public decimal AverageOrder => Orders == 0 ? 0 : Total / Orders;
    public decimal AverageLifetime => Customers == 0 ? 0 : Total / Customers;
}

public sealed class CustomerTotals
{
    public int Customers { get; set; }
    public int Returning { get; set; }
    public int Orders { get; set; }
    public double? AverageReorderDays { get; set; }
    public double ReturningPercent => Customers == 0 ? 0 : 100.0 * Returning / Customers;
    public double AverageOrders => Customers == 0 ? 0 : (double)Orders / Customers;
}

public sealed class CustomerSearchResult
{
    public IReadOnlyList<CustomerSummary> Customers { get; set; } = Array.Empty<CustomerSummary>();
    public CustomerTotals Totals { get; set; } = new();
    public IReadOnlyList<CustomerSpend> Spend { get; set; } = Array.Empty<CustomerSpend>();
    public int MissingIdentifiers { get; set; }
    public string IdentityField { get; set; } = "";
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

namespace Ekom.Models.Manager;

public sealed class CustomerAnalytics
{
    public int UniqueCustomers { get; set; }
    public int NewCustomers { get; set; }
    public int ReturningCustomers { get; set; }
    public int FirstOrders { get; set; }
    public int RepeatOrders { get; set; }
    public decimal FirstOrderAmount { get; set; }
    public decimal RepeatOrderAmount { get; set; }
    public int MissingIdentifiers { get; set; }
    public double? ReturningCustomerRate => UniqueCustomers == 0 ? null : 100.0 * ReturningCustomers / UniqueCustomers;
    public string IdentityField { get; set; } = "";
    public IReadOnlyList<CustomerOrderTime> OrderTimes { get; set; } = Array.Empty<CustomerOrderTime>();
}

public sealed class CustomerOrderTime
{
    public int Weekday { get; set; }
    public int Hour { get; set; }
    public int Orders { get; set; }
}

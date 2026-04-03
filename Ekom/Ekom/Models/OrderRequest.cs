using Ekom.Utilities;namespace Ekom.Models;

public class OrderRequest
{
    public required Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string StoreAlias { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public OrderAction? Action { get; set; } = OrderAction.AddOrUpdate;
    public OrderConsent? Consent { get; set; }
    public OrderTracking? Tracking { get; set; }
}

public class OrderlineRequest
{
    public required Guid lineId { get; set; }
    public string storeAlias { get; set; } = string.Empty;
    [MinimumValue(0)]
    public int quantity { get; set; } = 1;
}

public class CouponRequest
{
    public required string coupon { get; set; }
    public string storeAlias { get; set; } = string.Empty;
}

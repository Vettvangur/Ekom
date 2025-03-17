using Ekom.Utilities;

namespace Ekom.Models;

public class OrderRequest
{
    public required Guid productId { get; set; }
    public Guid? variantId { get; set; }
    public string storeAlias { get; set; } = string.Empty;
    public decimal quantity { get; set; } = 1;
    public OrderAction? action { get; set; } = OrderAction.AddOrUpdate;
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

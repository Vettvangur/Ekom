namespace Ekom.Models;

public class OrderDiscountCalculationRequest
{
    public required string CouponCode { get; set; }
    public string StoreAlias { get; set; } = string.Empty;
    public IReadOnlyList<OrderDiscountCalculationLineRequest> Lines { get; set; } = Array.Empty<OrderDiscountCalculationLineRequest>();
}

public class OrderDiscountCalculationLineRequest
{
    public string? ClientLineId { get; set; }
    public required string Sku { get; set; }
    public string? VariantSku { get; set; }
    public Guid? VariantKey { get; set; }
    public decimal Quantity { get; set; } = 1;
    public Dictionary<string, string>? PricingContext { get; set; }
}

public class OrderDiscountStockUpdateRequest
{
    public Guid Key { get; set; }
    public int Value { get; set; }
    public string? Coupon { get; set; }
}

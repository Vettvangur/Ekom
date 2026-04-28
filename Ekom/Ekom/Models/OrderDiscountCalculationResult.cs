namespace Ekom.Models;

public class OrderDiscountCalculationResult
{
    public bool Applied { get; set; }
    public required string CouponCode { get; set; }
    public Guid? DiscountId { get; set; }
    public string? DiscountTitle { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public IReadOnlyList<OrderDiscountCalculationLineResult> Lines { get; set; } = Array.Empty<OrderDiscountCalculationLineResult>();
    public IReadOnlyList<string> Messages { get; set; } = Array.Empty<string>();
}

public class OrderDiscountCalculationLineResult
{
    public required string Sku { get; set; }
    public string? VariantSku { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPriceBeforeDiscount { get; set; }
    public decimal LineTotalBeforeDiscount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotalAfterDiscount { get; set; }
    public decimal Vat { get; set; }
    public bool DiscountApplied { get; set; }
}

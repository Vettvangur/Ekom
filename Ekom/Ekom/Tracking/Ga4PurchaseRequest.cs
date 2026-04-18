namespace Ekom.Tracking;

public sealed class Ga4PurchaseRequest
{
    public Guid OrderUniqueId { get; set; }
    public string StoreAlias { get; set; } = string.Empty;
    public string? ClientId { get; set; }
    public long? SessionId { get; set; }
    public string EventName { get; set; } = "purchase";
    public string? TransactionId { get; set; }
    public decimal Value { get; set; }
    public decimal Shipping { get; set; }
    public decimal Tax { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? PaymentType { get; set; }
    public string? ShippingTier { get; set; }
    public string? Source { get; set; }
    public string? Medium { get; set; }
    public string? Campaign { get; set; }
    public string? Term { get; set; }
    public string? Content { get; set; }
    public string? Gclid { get; set; }
    public Dictionary<string, object?> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<Ga4PurchaseItem> Items { get; set; } = [];
}

public sealed class Ga4PurchaseItem
{
    public string? ItemId { get; set; }
    public string? ItemName { get; set; }
    public string? ItemCategory { get; set; }
    public string? ItemCategory2 { get; set; }
    public decimal Price { get; set; }
    public decimal Discount { get; set; }
    public int Quantity { get; set; }
    public string? ItemVariant { get; set; }
}

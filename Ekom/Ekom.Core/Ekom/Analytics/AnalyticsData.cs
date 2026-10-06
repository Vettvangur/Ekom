using LinqToDB;
using LinqToDB.Mapping;

namespace Ekom.Analytics;

[Table(Name = "EkomAnalyticsOrders")]
public sealed class AnalyticsOrderData
{
    [PrimaryKey, NotNull] public Guid OrderId { get; set; }
    [Column, NotNull] public int ReferenceId { get; set; }
    [Column(Length = 100), NotNull] public string OrderNumber { get; set; } = string.Empty;
    [Column(Length = 100), NotNull] public string StoreAlias { get; set; } = string.Empty;
    [Column(Length = 10), NotNull] public string CurrencyCode { get; set; } = string.Empty;
    [Column(Length = 100), NotNull] public string OrderStatus { get; set; } = string.Empty;
    [Column, NotNull] public DateTime CreateDate { get; set; }
    [Column] public DateTime? PaidDate { get; set; }
    [Column(DataType = DataType.Decimal, Precision = 19, Scale = 4), NotNull] public decimal GrandTotal { get; set; }
    [Column(DataType = DataType.Decimal, Precision = 19, Scale = 4), NotNull] public decimal GrandTotalWithoutVat { get; set; }
    [Column(DataType = DataType.Decimal, Precision = 19, Scale = 4), NotNull] public decimal ChargedAmount { get; set; }
    [Column(DataType = DataType.Decimal, Precision = 19, Scale = 4), NotNull] public decimal MerchandiseTotalWithVat { get; set; }
    [Column(DataType = DataType.Decimal, Precision = 19, Scale = 4), NotNull] public decimal MerchandiseTotalWithoutVat { get; set; }
    [Column(DataType = DataType.Decimal, Precision = 19, Scale = 4), NotNull] public decimal ShippingAmountWithVat { get; set; }
    [Column(DataType = DataType.Decimal, Precision = 19, Scale = 4), NotNull] public decimal ShippingAmountWithoutVat { get; set; }
    [Column(DataType = DataType.Decimal, Precision = 19, Scale = 4), NotNull] public decimal PaymentFeeWithVat { get; set; }
    [Column(DataType = DataType.Decimal, Precision = 19, Scale = 4), NotNull] public decimal PaymentFeeWithoutVat { get; set; }
    [Column(DataType = DataType.Decimal, Precision = 19, Scale = 4), NotNull] public decimal DiscountAmount { get; set; }
    [Column(DataType = DataType.Decimal, Precision = 19, Scale = 4), NotNull] public decimal DiscountAmountWithoutVat { get; set; }
    [Column(DataType = DataType.Decimal, Precision = 19, Scale = 4), NotNull] public decimal TotalQuantity { get; set; }
    [Column(Length = 32)] public string? CustomerIdentityType { get; set; }
    [Column(Length = 64)] public string? CustomerIdentityKey { get; set; }
    [Column, NotNull] public int CustomerIdentityPolicyVersion { get; set; }
    [Column(Length = 255)] public string? CustomerName { get; set; }
    [Column(Length = 320)] public string? CustomerEmail { get; set; }
    [Column(Length = 255)] public string? SourceCustomerId { get; set; }
    [Column] public Guid? PaymentProviderKey { get; set; }
    [Column(Length = 255)] public string? PaymentProviderTitle { get; set; }
    [Column] public Guid? ShippingProviderKey { get; set; }
    [Column(Length = 255)] public string? ShippingProviderTitle { get; set; }
    [Column(Length = 255)] public string? ShippingMethod { get; set; }
    [Column, NotNull] public DateTime ProjectedAtUtc { get; set; }
    [Column, NotNull] public int ProjectionVersion { get; set; } = 1;
}

[Table(Name = "EkomAnalyticsOrderLines")]
public sealed class AnalyticsOrderLineData
{
    [PrimaryKey(0), NotNull] public Guid OrderId { get; set; }
    [PrimaryKey(1), NotNull] public Guid OrderLineKey { get; set; }
    [Column] public Guid? ProductKey { get; set; }
    [Column] public Guid? VariantKey { get; set; }
    [Column] public int? ProductId { get; set; }
    [Column] public int? VariantId { get; set; }
    [Column(Length = 255)] public string? ProductTitle { get; set; }
    [Column(Length = 255)] public string? VariantTitle { get; set; }
    [Column(Length = 255)] public string? ProductSku { get; set; }
    [Column(Length = 255)] public string? VariantSku { get; set; }
    [Column(DataType = DataType.Decimal, Precision = 19, Scale = 4), NotNull] public decimal Quantity { get; set; }
    [Column, NotNull] public bool CountToTotal { get; set; } = true;
    [Column(DataType = DataType.Decimal, Precision = 19, Scale = 4), NotNull] public decimal TotalWithVat { get; set; }
    [Column(DataType = DataType.Decimal, Precision = 19, Scale = 4), NotNull] public decimal TotalWithoutVat { get; set; }
    [Column(DataType = DataType.Decimal, Precision = 19, Scale = 4), NotNull] public decimal DiscountAmount { get; set; }
    [Column(DataType = DataType.Decimal, Precision = 19, Scale = 4), NotNull] public decimal DiscountAmountWithoutVat { get; set; }
}

[Table(Name = "EkomAnalyticsPromotionApplications")]
public sealed class AnalyticsPromotionData
{
    [PrimaryKey, NotNull] public Guid ApplicationId { get; set; }
    [Column, NotNull] public Guid OrderId { get; set; }
    [Column] public Guid? OrderLineKey { get; set; }
    [Column(Length = 16), NotNull] public string Scope { get; set; } = string.Empty;
    [Column] public Guid? DiscountKey { get; set; }
    [Column(Length = 255)] public string? Title { get; set; }
    [Column(Length = 255)] public string? CouponCode { get; set; }
    [Column(Length = 32)] public string? DiscountType { get; set; }
    [Column(DataType = DataType.Decimal, Precision = 19, Scale = 4)] public decimal? RuleAmount { get; set; }
    [Column(DataType = DataType.Decimal, Precision = 19, Scale = 4)] public decimal? RealizedAmount { get; set; }
    [Column(Length = 32), NotNull] public string Attribution { get; set; } = string.Empty;
}

public sealed record AnalyticsProjection(
    AnalyticsOrderData Order,
    IReadOnlyList<AnalyticsOrderLineData> Lines,
    IReadOnlyList<AnalyticsPromotionData> Promotions);

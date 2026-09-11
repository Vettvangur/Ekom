using LinqToDB.Mapping;

namespace Ekom.Models;

[Table(Name = "EkomStockReservation")]
public sealed class StockReservationData
{
    [PrimaryKey, Column(Length = 36), NotNull]
    public string Id { get; set; } = "";
    // Hashed caller key, or a fresh generated key when no idempotency was requested.
    [Column(Length = 66), NotNull]
    public string CreationKey { get; set; } = "";
    [Column(Length = 64), NotNull]
    public string PayloadHash { get; set; } = "";
    [Column(Length = 255), NotNull]
    public string StockUniqueId { get; set; } = "";
    [Column, NotNull]
    public Guid Key { get; set; }
    [Column, NotNull]
    public bool IsDiscount { get; set; }
    [Column(Length = 255)]
    public string? StoreAlias { get; set; }
    [Column(Length = 255)]
    public string? Coupon { get; set; }
    [Column(DbType = "decimal(18,2)"), NotNull]
    public decimal Quantity { get; set; }
    [Column(Length = 255)]
    public string? OrderId { get; set; }
    [Column(Length = 255)]
    public string? PaymentAttemptId { get; set; }
    [Column, NotNull]
    public StockReservationState State { get; set; }
    [Column, NotNull]
    public DateTime CreatedUtc { get; set; }
    [Column, NotNull]
    public DateTime ExpiresUtc { get; set; }
    [Column]
    public DateTime? CompletedUtc { get; set; }
    [Column]
    public DateTime? RetryAfterUtc { get; set; }
    [Column, NotNull]
    public int ExpiryFailures { get; set; }
}

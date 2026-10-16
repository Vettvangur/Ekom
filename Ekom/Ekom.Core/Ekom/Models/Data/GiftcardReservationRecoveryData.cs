using LinqToDB.Mapping;

namespace Ekom.Models;

// Separate from submitted snapshots and OrderInfo.Giftcards: evidence never changes the payable amount.
[Table(Name = "EkomGiftcardReservationRecovery")]
internal sealed class GiftcardReservationRecoveryData
{
    [PrimaryKey, NotNull]
    public Guid AttemptId { get; set; }
    [Column, NotNull]
    public Guid OrderId { get; set; }
    [Column, NotNull]
    public GiftcardReservationRecoveryState State { get; set; }
    [Column, NotNull]
    public Guid Revision { get; set; }
    [Column(Length = 36), NotNull]
    public string Owner { get; set; } = string.Empty;
    [Column(Length = 36)]
    public string? PreparationOwner { get; set; }
    [Column(Length = int.MaxValue), NotNull]
    public string SelectionsJson { get; set; } = "[]";
    [Column(Length = int.MaxValue), NotNull]
    public string EvidenceJson { get; set; } = "{}";
    [Column(Length = 512), NotNull]
    public string Reason { get; set; } = string.Empty;
    [Column, NotNull]
    public DateTime CreatedUtc { get; set; }
    [Column, NotNull]
    public DateTime UpdatedUtc { get; set; }
}

internal enum GiftcardReservationRecoveryState
{
    Pending,
    Accepted,
    Unresolved,
    Resolved,
}

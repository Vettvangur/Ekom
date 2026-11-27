using Ekom.Brevo.Models;
using LinqToDB;
using LinqToDB.Mapping;

namespace Ekom.Brevo.Persistence;

[Table(Name = "EkomBrevoEmail")]
internal sealed class BrevoEmailRow
{
    [PrimaryKey, Column(Length = 32), NotNull] public string Id { get; set; } = "";
    [Column(Length = 255), NotNull] public string Store { get; set; } = "";
    [Column(Length = 255)] public string? OrderId { get; set; }
    [Column(Length = 64)] public string? DedupKey { get; set; }
    [Column(Length = 64), NotNull] public string PayloadHash { get; set; } = "";
    [Column(DataType = DataType.NText), NotNull] public string Payload { get; set; } = "";
    [Column] public long Bytes { get; set; }
    [Column] public BrevoQueueState State { get; set; }
    [Column] public int Attempts { get; set; }
    [Column] public DateTime CreatedUtc { get; set; }
    [Column] public DateTime UpdatedUtc { get; set; }
    [Column] public DateTime DueUtc { get; set; }
    [Column] public DateTime? LeaseUntilUtc { get; set; }
    [Column(Length = 32)] public string? LeaseId { get; set; }
    [Column(Length = 320)] public string? MessageId { get; set; }
    [Column] public int? HttpStatus { get; set; }
}

[Table(Name = "EkomBrevoEvent")]
internal sealed class BrevoEventRow
{
    [PrimaryKey, Column(Length = 64), NotNull] public string Id { get; set; } = "";
    [Column(Length = 32), NotNull] public string EmailId { get; set; } = "";
    [Column(Length = 320), NotNull] public string Recipient { get; set; } = "";
    [Column(Length = 32), NotNull] public string Event { get; set; } = "";
    [Column] public DateTime OccurredUtc { get; set; }
    [Column(Length = 1000)] public string? Reason { get; set; }
}

// A database row lock serializes admission, claims and webhook writes across application instances.
[Table(Name = "EkomBrevoLock")]
internal sealed class BrevoLockRow
{
    [PrimaryKey, Column] public int Id { get; set; }
    [Column] public int Marker { get; set; }
}

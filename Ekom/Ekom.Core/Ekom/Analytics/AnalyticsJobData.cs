using LinqToDB.Mapping;

namespace Ekom.Analytics;

[Table(Name = "EkomAnalyticsJobs")]
public sealed class AnalyticsJobData
{
    [PrimaryKey, NotNull] public Guid JobId { get; set; }
    [Column(Length = 100), NotNull] public string StoreAlias { get; set; } = string.Empty;
    [Column(Length = 32), NotNull] public string Kind { get; set; } = "Rebuild";
    [Column(Length = 32), NotNull] public string Status { get; set; } = "Pending";
    [Column, NotNull] public DateTime StartedAtUtc { get; set; }
    [Column, NotNull] public DateTime UpdatedAtUtc { get; set; }
    [Column] public DateTime? CompletedAtUtc { get; set; }
    [Column, NotNull] public int MaximumReferenceId { get; set; }
    [Column, NotNull] public int LastReferenceId { get; set; }
    [Column, NotNull] public long ProcessedCount { get; set; }
    [Column, NotNull] public long SucceededCount { get; set; }
    [Column, NotNull] public long FailedCount { get; set; }
    [Column, NotNull] public long RemovedCount { get; set; }
    [Column] public DateTime? WindowStart { get; set; }
    [Column] public Guid? LeaseOwner { get; set; }
    [Column] public DateTime? LeaseUntilUtc { get; set; }
    [Column(Length = 32)] public string? Control { get; set; }
}

[Table(Name = "EkomAnalyticsLease")]
public sealed class AnalyticsLeaseData
{
    [PrimaryKey, NotNull] public int Id { get; set; }
    [Column] public Guid? Owner { get; set; }
    [Column] public DateTime? ExpiresAtUtc { get; set; }
}

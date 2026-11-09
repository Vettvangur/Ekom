using LinqToDB.Mapping;

namespace Ekom.Printer.Storage;

[Table("EkomPrinterDelivery")]
internal sealed class PrinterDelivery
{
    [PrimaryKey, Column(Length = 64), NotNull] public string JobId { get; set; } = string.Empty;
    [Column] public Guid ClaimToken { get; set; }
    [Column(Length = 100), NotNull] public string StationAlias { get; set; } = string.Empty;
    [Column] public DateTime ClaimedUtc { get; set; }
    [Column] public DateTime ExpiresUtc { get; set; }
    [Column, Nullable] public DateTime? ReportedUtc { get; set; }
    [Column, Nullable] public bool? Submitted { get; set; }
    [Column(Length = 100), Nullable] public string? SpoolJobId { get; set; }
}

[Table("EkomPrinterStation")]
internal sealed class PrinterStation
{
    [PrimaryKey, Column(Length = 100), NotNull] public string Alias { get; set; } = string.Empty;
    [Column] public DateTime LastSeenUtc { get; set; }
    [Column] public int ProtocolVersion { get; set; }
    [Column(Length = 100), NotNull] public string AgentVersion { get; set; } = string.Empty;
    [Column(Length = 255), NotNull] public string PrinterName { get; set; } = string.Empty;
}

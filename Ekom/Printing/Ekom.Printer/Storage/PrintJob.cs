using LinqToDB;
using LinqToDB.Mapping;

namespace Ekom.Printer.Storage;

public enum PrintJobState
{
    PendingRender,
    Ready,
    Claimed,
    Submitted,
    NeedsReview,
    Cancelled,
    RenderFailed,
}

[Table("EkomPrinterJob")]
public sealed class PrintJob
{
    [PrimaryKey, Column(Length = 64), NotNull] public string Id { get; set; } = string.Empty;
    [Column] public Guid OrderId { get; set; }
    [Column(Length = 100), NotNull] public string StationAlias { get; set; } = string.Empty;
    [Column] public PrintJobState State { get; set; }
    [Column] public int Copies { get; set; }
    [Column] public DateTime CreatedUtc { get; set; }
    [Column, Nullable] public DateTime? CompletedUtc { get; set; }
    [Column(DataType = DataType.NText), Nullable] public string? SnapshotJson { get; set; }
    [Column(DataType = DataType.NText), Nullable] public string? Html { get; set; }
    [Column(Length = 100), Nullable] public string? ErrorCode { get; set; }
}

[Table("EkomPrinterSchema")]
internal sealed class PrinterSchemaVersion
{
    [PrimaryKey] public int Id { get; set; }
    [Column] public int Version { get; set; }
}

using Ekom.Printer.Configuration;
using Ekom.Utilities;
using LinqToDB;
using LinqToDB.Mapping;

namespace Ekom.Printer.Storage;

[Table("EkomPrinterCapturedStatus")]
internal sealed class CapturedStatus
{
    [PrimaryKey, Column(Length = 64), NotNull] public string Id { get; set; } = string.Empty;
    [Column(Length = 100), NotNull] public string StationAlias { get; set; } = string.Empty;
    [Column] public OrderStatus Status { get; set; }
    [Column] public PrintPolicy PrintPolicy { get; set; }
    [Column] public int Copies { get; set; }
    [Column] public DateTime CreatedUtc { get; set; }
    [Column] public DateTime RetryAfterUtc { get; set; }
    [Column(DataType = DataType.NText), NotNull] public string OrderJson { get; set; } = string.Empty;
    [Column(Length = 100), Nullable] public string? ErrorCode { get; set; }
}

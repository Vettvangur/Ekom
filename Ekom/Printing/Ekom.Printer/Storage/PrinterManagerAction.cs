using LinqToDB.Mapping;

namespace Ekom.Printer.Storage;

[Table("EkomPrinterManagerAction")]
internal sealed class PrinterManagerAction
{
    [PrimaryKey] public Guid Id { get; set; }
    [Column(Length = 64), NotNull] public string JobId { get; set; } = string.Empty;
    [Column(Length = 64), Nullable] public string? NewJobId { get; set; }
    [Column(Length = 30), NotNull] public string Action { get; set; } = string.Empty;
    [Column(Length = 255), NotNull] public string Actor { get; set; } = string.Empty;
    [Column] public DateTime CreatedUtc { get; set; }
}

namespace Ekom.Models.Import;

/// <summary>
/// Represents a warehouse stock balance for an imported product or variant.
/// </summary>
public class ImportWarehouseStock
{
    public required string StoreAlias { get; set; }
    public required Guid WarehouseKey { get; set; }
    public required decimal Balance { get; set; }
}

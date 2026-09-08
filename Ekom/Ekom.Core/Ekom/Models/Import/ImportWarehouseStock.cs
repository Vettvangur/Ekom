namespace Ekom.Models.Import;

/// <summary>
/// Represents a warehouse stock balance for an imported product or variant.
/// </summary>
public class ImportWarehouseStock
{
    public required string StoreAlias { get; set; }
    public required Guid WarehouseKey { get; set; }
    public decimal Balance { get; set; }
    public bool Clear { get; set; }
}

namespace Ekom.Models;

/// <summary>
/// A visible warehouse definition combined with the balance for one SKU.
/// </summary>
public sealed class WarehouseStockLevel
{
    public string StoreAlias { get; init; } = string.Empty;
    public Guid WarehouseKey { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public string Sku { get; init; } = string.Empty;
    public decimal? Balance { get; init; }
    public DateTime? UpdateDate { get; init; }
}

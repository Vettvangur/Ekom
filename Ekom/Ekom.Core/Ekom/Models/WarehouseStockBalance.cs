namespace Ekom.Models;

/// <summary>
/// The stock balance for a SKU in a warehouse.
/// </summary>
public sealed class WarehouseStockBalance
{
    /// <summary>
    /// Store alias that owns the balance.
    /// </summary>
    public string StoreAlias { get; init; } = string.Empty;

    /// <summary>
    /// Warehouse identifier.
    /// </summary>
    public Guid WarehouseKey { get; init; }

    /// <summary>
    /// Normalized SKU.
    /// </summary>
    public string Sku { get; init; } = string.Empty;

    /// <summary>
    /// Available warehouse balance.
    /// </summary>
    public decimal Balance { get; init; }

    /// <summary>
    /// When the balance was last set.
    /// </summary>
    public DateTime UpdateDate { get; init; }
}

namespace Ekom.Models;

/// <summary>
/// A warehouse SKU balance to set.
/// </summary>
public sealed class WarehouseStockBalanceRequest
{
    /// <summary>
    /// SKU to set the balance for.
    /// </summary>
    public string Sku { get; init; } = string.Empty;

    /// <summary>
    /// Warehouse balance.
    /// </summary>
    public decimal Balance { get; init; }
}

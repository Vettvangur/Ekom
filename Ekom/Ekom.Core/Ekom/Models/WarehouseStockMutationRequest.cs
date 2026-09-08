namespace Ekom.Models;

/// <summary>
/// A warehouse stock mutation that sets or clears one SKU balance.
/// </summary>
public sealed class WarehouseStockMutationRequest
{
    public string StoreAlias { get; init; } = string.Empty;
    public Guid WarehouseKey { get; init; }
    public string Sku { get; init; } = string.Empty;
    public WarehouseStockMutationOperation Operation { get; init; }
    public decimal? Balance { get; init; }
}

public enum WarehouseStockMutationOperation
{
    Set,
    Clear,
}

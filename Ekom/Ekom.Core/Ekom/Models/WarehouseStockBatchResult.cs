namespace Ekom.Models;

/// <summary>
/// The partial-success result of a warehouse stock batch.
/// </summary>
public sealed class WarehouseStockBatchResult
{
    public IReadOnlyList<WarehouseStockMutationResult> Entries { get; init; } = Array.Empty<WarehouseStockMutationResult>();
    public int Inserted => Entries.Count(entry => entry.Status == WarehouseStockMutationStatus.Inserted);
    public int Updated => Entries.Count(entry => entry.Status == WarehouseStockMutationStatus.Updated);
    public int Cleared => Entries.Count(entry => entry.Status == WarehouseStockMutationStatus.Cleared);
    public int Unchanged => Entries.Count(entry => entry.Status == WarehouseStockMutationStatus.Unchanged);
    public int Failed => Entries.Count(entry => entry.Status == WarehouseStockMutationStatus.Failed);
}

public sealed class WarehouseStockMutationResult
{
    public int Index { get; init; }
    public string StoreAlias { get; init; } = string.Empty;
    public Guid WarehouseKey { get; init; }
    public string Sku { get; init; } = string.Empty;
    public WarehouseStockMutationStatus Status { get; init; }
    public decimal? Balance { get; init; }
    public DateTime? UpdateDate { get; init; }
    public string? Error { get; init; }
    internal Exception? Exception { get; init; }
}

public enum WarehouseStockMutationStatus
{
    Inserted,
    Updated,
    Cleared,
    Unchanged,
    Failed,
}

namespace Ekom.Models;

public sealed class WarehouseStockEditorValue
{
    public string Sku { get; init; } = string.Empty;
    public IReadOnlyList<WarehouseStockEditorItem> Items { get; init; } = Array.Empty<WarehouseStockEditorItem>();
}

public sealed class WarehouseStockEditorItem
{
    public string StoreAlias { get; init; } = string.Empty;
    public Guid WarehouseKey { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool Visible { get; init; }
    public decimal? Balance { get; init; }
}

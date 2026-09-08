namespace Ekom.Models;

public sealed class WarehouseDefinition
{
    public Guid Key { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public bool Visible { get; init; }
}

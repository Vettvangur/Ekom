public sealed class EkomOptions
{
    public ManagerOptions Manager { get; set; } = new();
}

public sealed class ManagerOptions
{
    public string? SectionAccessGroup { get; set; }

    /// <summary>
    /// Key = store alias (e.g. "store1")
    /// Value = allowed groups for that store
    /// </summary>
    public Dictionary<string, string[]> StoreGroupPermissions { get; set; } = new();
}

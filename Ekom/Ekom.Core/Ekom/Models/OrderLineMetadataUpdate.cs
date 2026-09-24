namespace Ekom.Models;

/// <summary>
/// Metadata to merge into an existing order line identified by its line key.
/// </summary>
public sealed class OrderLineMetadataUpdate
{
    public required Guid LineId { get; init; }

    public IReadOnlyDictionary<string, string> Properties { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

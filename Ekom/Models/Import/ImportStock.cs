namespace Ekom.Models.Import;

/// <summary>
/// Represents the stock level of an item for a specific store.
/// </summary>
public class ImportStock
{
    /// <summary>
    /// Gets or sets the alias of the store where this stock level is applicable.
    /// </summary>
    /// <value>The store alias as a string.</value>
    public required string StoreAlias { get; set; }

    /// <summary>
    /// Gets or sets the stock level of the item.
    /// </summary>
    /// <value>The stock level as an integer.</value>
    public required int Stock { get; set; }
}

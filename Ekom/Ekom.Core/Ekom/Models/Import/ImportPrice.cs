namespace Ekom.Models.Import;

/// <summary>
/// Represents the pricing information of an item for a specific store.
/// </summary>
public class ImportPrice
{
    /// <summary>
    /// Gets or sets the alias of the store where this price is applicable.
    /// </summary>
    /// <value>The store alias as a string.</value>
    public required string StoreAlias { get; set; }

    /// <summary>
    /// Gets or sets the price of the item.
    /// </summary>
    /// <value>The price as a decimal number.</value>
    public required decimal Price { get; set; }

    /// <summary>
    /// Gets or sets the currency code for the price.
    /// </summary>
    /// <value>The currency code as a string.</value>
    public required string Currency { get; set; }
}

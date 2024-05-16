namespace Ekom.Models.Import;

/// <summary>
/// Defines a specific variant within a variant group, detailing unique attributes like SKU, pricing, and stock levels.
/// Variants allow customers to select different options for a product based on their preferences or requirements.
/// </summary>
public class ImportVariant : ImportBase
{
    public string? SKU { get; set; }
    public Dictionary<string, object>? Description { get; set; } = new Dictionary<string, object>();
    public List<ImportPrice> Price { get; set; } = new List<ImportPrice>();
    public List<ImportStock> Stock { get; set; } = new List<ImportStock>();
    public bool EnableBackorder { get; set; }
    public List<IImportMedia> Files { get; set; } = new List<IImportMedia>();
    public decimal? Vat { get; set; }

    private string? _identifier;
    /// <summary>
    /// Optional Identifer value if other then SKU. Represents a unique identifier corresponding to this entity in an external system, facilitating data synchronization and matching operations. 
    /// This identifier can be a unique ID, or any other distinctive code used by the external system to uniquely identify 
    /// the entity. If <see cref="IdentiferPropertyAlias"/> is not "sku" then the value will be saved to that property
    /// </summary>
    public string? Identifier
    {
        get => _identifier ?? SKU;  // Return _identifier if it's set; otherwise, return SKU.
        set => _identifier = value;
    }
    /// <summary>
    /// Specifies an alias for the property to be used as the unique identifier for the entity. This property allows for flexibility in determining which attribute should serve as the entity's key identifier, supporting custom import logic or integration needs. If this property is left unset, the 'SKU' property is used as the default identifier. This approach provides a customizable mechanism to map unique identifiers according to specific data models or external system requirements, ensuring seamless data integration and consistency.
    /// </summary>
    public string IdentiferPropertyAlias { get; set; } = "sku";

}

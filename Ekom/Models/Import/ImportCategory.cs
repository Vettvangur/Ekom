namespace Ekom.Models.Import;

/// <summary>
/// Represents a category in the import data, including its hierarchy, associated products, and metadata like images and virtual URL flags.
/// Categories can be nested to represent a category tree structure.
/// </summary>
public class ImportCategory : ImportBase
{
    /// <summary>
    /// Represents a URL-friendly version of the <see cref="Title"/> used for creating more readable and SEO-optimized web addresses. 
    /// If this property is not explicitly set, it will be automatically generated based on the <see cref="Title"/> property, 
    /// ensuring that the resulting slug is both human-readable and suitable for use in URLs. This automatic generation typically 
    /// involves converting the title to lowercase, replacing spaces with hyphens, and removing any special characters that are not 
    /// URL-friendly, thereby facilitating better web standards compliance and user experience.
    /// </summary>
    public Dictionary<string, object>? Slug { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Determines whether the URL associated with this entity should be excluded from the navigational structure 
    /// of categories/products that descend from it. When set to <c>true</c>, this entity acts as a virtual node, 
    /// meaning its associated URL will not be generated or included in the hierarchical path for any descendant 
    /// categories or products. This is useful for organizing content or products under virtual groupings that do 
    /// not require direct web access via a URL. If this property is left unset or is set to <c>false</c>, the 
    /// entity will be treated as a standard navigational node, with its URL contributing to the path construction 
    /// of descendant entities.
    /// </summary>
    public string? SKU { get; set; }
    public bool? VirtualUrl { get; set; }
    public Dictionary<string, object>? Description { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, bool> Disabled = new Dictionary<string, bool>();
    public List<ImportCategory> SubCategories { get; set; } = new List<ImportCategory>();

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

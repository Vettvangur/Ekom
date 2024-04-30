namespace Ekom.Models.Import;

/// <summary>
/// Represents a product with detailed information including pricing, stock levels, backorder capabilities, and categorization for import.
/// Products are associated with variant groups for more complex product configurations.
/// </summary>
public class ImportProduct : ImportBase
{
    /// <summary>
    /// Represents a URL-friendly version of the <see cref="Title"/> used for creating more readable and SEO-optimized web addresses. 
    /// If this property is not explicitly set, it will be automatically generated based on the <see cref="Title"/> property, 
    /// ensuring that the resulting slug is both human-readable and suitable for use in URLs. This automatic generation typically 
    /// involves converting the title to lowercase, replacing spaces with hyphens, and removing any special characters that are not 
    /// URL-friendly, thereby facilitating better web standards compliance and user experience.
    /// </summary>
    public Dictionary<string, object>? Slug { get; set; } = new Dictionary<string, object>();
    public string? SKU { get; set; }
    public Dictionary<string, object>? Description { get; set; } = new Dictionary<string, object>();
    public List<ImportPrice> Price { get; set; } = new List<ImportPrice>();
    public List<ImportStock> Stock { get; set; } = new List<ImportStock>();
    public bool EnableBackorder { get; set; }
    public decimal? Vat { get; set; }
    public List<IImportMedia> Files { get; set; } = new List<IImportMedia>();

    /// <summary>
    /// Represents a collection of category identifiers associated with a product. The first identifier
    /// in the list denotes the primary category of the product, while subsequent identifiers represent
    /// additional categories to which the product is linked. This categorization aids in organizing
    /// products within different classifications for easier access and management.
    /// </summary>
    public List<string> Categories { get; set; } = new List<string>();

    public Dictionary<string, bool> Disabled = new Dictionary<string, bool>();
    public List<ImportVariantGroup> VariantGroups { get; set; } = new List<ImportVariantGroup>();

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

    /// <summary>
    /// Specifies a template
    /// </summary>
    public int? TemplateId { get; set; }
}
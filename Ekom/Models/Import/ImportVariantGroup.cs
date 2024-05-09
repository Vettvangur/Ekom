namespace Ekom.Models.Import;

/// <summary>
/// Groups variants of a product together, providing a mechanism to associate different versions of a product under a single product listing.
/// Each variant group can contain multiple variants, differentiated by attributes like size, color, or material.
/// </summary>
public class ImportVariantGroup : ImportBase
{
    public List<ImportVariant> Variants { get; set; } = new List<ImportVariant>();

    /// <summary>
    /// Represents a unique identifier corresponding to this entity in an external system, facilitating data synchronization and matching operations. 
    /// This identifier can be a unique ID, or any other distinctive code used by the external system to uniquely identify 
    /// the entity.
    /// </summary>
    public required string Identifier { get; set; }

    /// <summary>
    /// Specifies an alias for the property to be used as the unique identifier for the entity. This property allows for flexibility in determining which attribute should serve as the entity's key identifier, supporting custom import logic or integration needs. If this property is left unset, the identiferAlias set in the sync function will be used as the default identifier. This approach provides a customizable mechanism to map unique identifiers according to specific data models or external system requirements, ensuring seamless data integration and consistency.
    /// </summary>
    public string? IdentiferPropertyAlias { get; set; }
}

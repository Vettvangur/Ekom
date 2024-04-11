namespace Ekom.Models.Import;

/// <summary>
/// Groups variants of a product together, providing a mechanism to associate different versions of a product under a single product listing.
/// Each variant group can contain multiple variants, differentiated by attributes like size, color, or material.
/// </summary>
public class ImportVariantGroup : ImportBase
{
    public List<ImportVariant> Variants { get; set; } = new List<ImportVariant>();
}

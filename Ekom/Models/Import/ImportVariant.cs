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

}

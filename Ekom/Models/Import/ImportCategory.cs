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

    public string? SKU { get; set; }
    public bool? VirtualUrl { get; set; }
    public Dictionary<string, object>? Description { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, bool> Disabled = new Dictionary<string, bool>();
    public List<ImportCategory>? SubCategories { get; set; } = new List<ImportCategory>();

    /// <summary>
    /// Specifies a template
    /// </summary>
    public int? TemplateId { get; set; }

}

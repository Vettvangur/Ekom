namespace Ekom.Models.Import
{
    /// <summary>
    /// Root container for all import data, representing the complete set of categories to be imported.
    /// </summary>
    public class ImportData
    {
        /// <summary>
        /// A collection of categories to be imported. Each entry in this list represents a category, complete with its hierarchy, products, and associated details, as defined by the <see cref="ImportCategory"/> class. This comprehensive model supports importing a rich, nested structure of categories and products, facilitating complex updates and additions to the e-commerce platform's catalog. The ability to define subcategories and products within each category allows for a deep, tree-structured import, mimicking the natural organization of an e-commerce catalog.
        /// </summary>
        public List<ImportCategory> Categories { get; set; } = new List<ImportCategory>();

        /// <summary>
        /// Represents a collection of products to be imported. Each entry in this list corresponds to a product,
        /// as detailed by the <see cref="ImportProduct"/> class.
        /// </summary>
        public List<ImportProduct> Products { get; set; } = new List<ImportProduct>();

        /// <summary>
        /// The GUID key of the media root node in Umbraco to which images are imported. This key identifies
        /// the specific location within the media library where new images will be stored.
        /// </summary>
        public required Guid MediaRootKey { get; set; }
    }
}

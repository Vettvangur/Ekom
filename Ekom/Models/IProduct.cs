namespace Ekom.Models
{
    /// <summary>
    /// 
    /// </summary>
    public interface IProduct : INodeEntityWithUrl, IPerStoreNodeEntity
    {
        /// <summary>
        /// Gets the price.
        /// </summary>
        /// <value>
        /// The price.
        /// </value>
        IPrice Price { get; }

        /// <summary>
        /// Gets the prices.
        /// </summary>
        /// <value>
        /// The price.
        /// </value>
        List<IPrice> Prices { get; }

        /// <summary>
        /// Gets the orignal price.
        /// </summary>
        /// <value>
        /// The original price.
        /// </value>
        IPrice OriginalPrice { get; }

        /// <summary>
        /// Gets the Vat.
        /// </summary>
        /// <value>
        /// The vat.
        /// </value>
        decimal Vat { get; }

        List<Metavalue> Metafields { get; }

        /// <summary>
        /// Gets the stock.
        /// </summary>
        /// <value>
        /// The stock.
        /// </value>
        int Stock { get; }

        /// <summary>
        /// Gets the availability of the product and variants.
        /// </summary>
        /// <value>
        /// The availability.
        /// </value>
        bool Available { get; }

        /// <summary>
        /// All ancestor categories this <see cref="Product"/> belongs to from the primary category.
        /// </summary>
        /// <returns></returns>
        IEnumerable<ICategory> CategoryAncestors { get; }

        /// <summary>
        /// All categories product belongs to, includes parent category and related categories.
        /// </summary>
        IEnumerable<ICategory> Categories { get; }

        /// <summary>
        /// All ID's of categories product belongs to, includes parent category and related categories.
        /// </summary>
        IEnumerable<Guid> CategoriesIds { get; }
        
        /// <summary>
        /// 
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Get Backorder status
        /// </summary>
        bool Backorder { get; }

        /// <summary>
        /// Product images
        /// </summary>
        IEnumerable<Image> Images { get; }

        /// <summary>
        /// A product can have multiple variant groups, 
        /// therefore we allow to configure a default/primary variant group.
        /// If none is configured, we return the first possible item.
        /// </summary>
        IVariantGroup? PrimaryVariantGroup { get; }

        /// <summary>
        /// Select the Primary variant.
        /// First Variant in the primary variant group that is available, if none are available, return the first variant.
        /// </summary>
        IVariant? PrimaryVariant { get; }

        /// <summary>
        /// Product Stock Keeping Unit.
        /// </summary>
        string SKU { get; }
        /// <summary>
        /// 
        /// </summary>
        string Summary { get; }
        /// <summary>
        /// All child variant groups of this product
        /// </summary>
        IEnumerable<IVariantGroup> VariantGroups { get; }
        /// <summary>
        /// All variants belonging to product.
        /// </summary>
        IEnumerable<IVariant> AllVariants { get; }

        /// <summary>
        /// A discount specific to this product populated after product discount cache is filled.
        /// </summary>
        IDiscount ProductDiscount(string price = null);

        /// <summary>
        /// Get related products
        /// </summary>
        IEnumerable<IProduct> RelatedProducts(int count = 4);

        void InvalidateCache();
    }
}

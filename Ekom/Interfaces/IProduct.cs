using Ekom.Models;
using System;
using System.Collections.Generic;

namespace Ekom.Interfaces
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
        /// Gets the stock.
        /// </summary>
        /// <value>
        /// The stock.
        /// </value>
        int Stock { get; }

        /// <summary>
        /// All categories product belongs to, includes parent category.
        /// Does not include categories product is an indirect child of.
        /// </summary>
        IEnumerable<ICategory> Categories();

        /// <summary>
        /// All ID's of categories product belongs to, includes parent category.
        /// Does not include categories product is an indirect child of.
        /// </summary>
        IEnumerable<Guid> CategoriesIds { get; }
        /// <summary>
        /// 
        /// </summary>
        string Description { get; }
        /// <summary>
        /// Product images
        /// </summary>
        IEnumerable<Image> Images { get; }
        IVariantGroup PrimaryVariantGroup { get; }
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
        /// All categories this <see cref="Product"/> belongs to.
        /// Found by traversing up the examine tree and then matching examine items to cached <see cref="ICategory"/>'s
        /// </summary>
        /// <returns></returns>
        List<ICategory> CategoryAncestors();
        /// <summary>
        /// Best discount mapped to product, populated after discount cache fills.
        /// </summary>
        IDiscount Discount { get; }
    }
}

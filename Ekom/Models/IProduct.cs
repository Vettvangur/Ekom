using System;
using System.Collections.Generic;

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
        /// Gets the Vat.
        /// </summary>
        /// <value>
        /// The stock.
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
        /// All categories product belongs to, includes parent category.
        /// Does not include categories product is an indirect child of.
        /// </summary>
        IEnumerable<ICategory> Categories { get; }

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
        IEnumerable<ICategory> CategoryAncestors { get; }
        /// <summary>
        /// A discount specific to this product populated after product discount cache is filled.
        /// </summary>
        IDiscount ProductDiscount(string price = null);
    }
}

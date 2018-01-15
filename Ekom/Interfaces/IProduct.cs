using Ekom.Models;
using System;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    /// <summary>
    /// 
    /// </summary>
    public interface IProduct : INodeEntityWithUrl
    {
        /// <summary>
        /// Gets the price.
        /// </summary>
        /// <value>
        /// The price.
        /// </value>
        IDiscountedPrice Price { get; }


        /// <summary>
        /// Gets the stock.
        /// </summary>
        /// <value>
        /// The stock.
        /// </value>
        int Stock { get; }

        /// <summary>
        /// Gets the categories.
        /// </summary>
        /// <value>
        /// The categories.
        /// </value>
        IEnumerable<ICategory> Categories();

        IEnumerable<Guid> CategoriesIds { get; }
        string Description { get; }
        IEnumerable<Image> Images { get; }
        IVariantGroup PrimaryVariantGroup { get; }
        string SKU { get; }
        IStore Store { get; }
        string Summary { get; }
        string Url { get; }
        IEnumerable<string> Urls { get; set; }
        IEnumerable<IVariantGroup> VariantGroups { get; }

        List<ICategory> CategoryAncestors();
    }
}

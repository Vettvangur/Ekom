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
        /// Gets the slug.
        /// </summary>
        /// <value>
        /// The slug.
        /// </value>
        string Slug { get; }

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
        List<ICategory> Categories();
    }
}

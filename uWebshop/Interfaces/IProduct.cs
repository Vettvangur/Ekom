using System.Collections.Generic;

namespace uWebshop.Interfaces
{
    public interface IProduct : INodeEntitiyWithUrl
    {
        /// <summary>
        /// Gets the price.
        /// </summary>
        /// <value>
        /// The price.
        /// </value>
        IDiscountedPrice Price { get; }

        /// <summary>
        /// Gets the title.
        /// </summary>
        /// <value>
        /// The title.
        /// </value>
        string Title { get; }

        /// <summary>
        /// Gets the slug.
        /// </summary>
        /// <value>
        /// The slug.
        /// </value>
        string Slug { get; }

        /// <summary>
        /// Gets the original price.
        /// </summary>
        /// <value>
        /// The original price.
        /// </value>
        decimal OriginalPrice { get; }

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

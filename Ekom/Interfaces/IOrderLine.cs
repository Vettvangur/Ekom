using System;
using Ekom.Models;

namespace Ekom.Interfaces
{
    public interface IOrderLine
    {
        /// <summary>
        /// Gets the line Id.
        /// </summary>
        /// <value>
        /// The line Id.
        /// </value>
        Guid Id { get; }

        /// <summary>
        /// Gets the product.
        /// </summary>
        /// <value>
        /// The product.
        /// </value>
        OrderedProduct Product { get; }

        /// <summary>
        /// Gets the quantity.
        /// </summary>
        /// <value>
        /// The quantity.
        /// </value>
        int Quantity { get; }
    }
}

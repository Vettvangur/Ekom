using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Models;

namespace uWebshop.Interfaces
{
    public interface IOrderLine
    {
        /// <summary>
        /// Gets the line Id.
        /// </summary>
        /// <value>
        /// The line Id.
        /// </value>
        int Id { get; }

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

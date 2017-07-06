using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace uWebshop.Interfaces
{
    public interface IOrderedProduct : IProduct
    {
        /// <summary>
        /// Gets or sets the item count.
        /// </summary>
        /// <value>
        /// The item count.
        /// </value>
        int Quantity { get; }

        /// <summary>
        /// Gets the variants.
        /// </summary>
        /// <value>
        /// The variants.
        /// </value>
        IEnumerable<IOrderedProductVariant> Variants { get; }
    }
}

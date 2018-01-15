using Ekom.Models;
using Ekom.Models.Discounts;
using System;

namespace Ekom.Interfaces
{
    /// <summary>
    /// Order/Cart
    /// </summary>
    public interface IOrderLine
    {
        /// <summary>
        /// Price
        /// </summary>
        IDiscountedPrice Amount { get; }
        /// <summary>
        /// Currently set coupon
        /// </summary>
        string Coupon { get; }
        /// <summary>
        /// Current discount
        /// </summary>
        IDiscount Discount { get; }

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

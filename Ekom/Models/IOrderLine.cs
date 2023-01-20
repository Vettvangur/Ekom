using Ekom.Models;
using System;

namespace Ekom.Models
{
    /// <summary>
    /// Order/Cart
    /// </summary>
    public interface IOrderLine
    {
        /// <summary>
        /// Price
        /// </summary>
        IPrice Amount { get; }
        /// <summary>
        /// Currently set coupon
        /// </summary>
        string Coupon { get; }
        /// <summary>
        /// Current discount
        /// </summary>
        OrderedDiscount Discount { get; }

        OrderLineInfo OrderLineInfo { get; }

        /// <summary>
        /// Gets the line Id.
        /// </summary>
        /// <value>
        /// The line Id.
        /// </value>
        Guid Key { get; }

        /// <summary>
        /// Gets the product.
        /// </summary>
        /// <value>
        /// The product.
        /// </value>
        OrderedProduct Product { get; }
        /// <summary>
        /// 
        /// </summary>
        Guid ProductKey { get; }
        /// <summary>
        /// 
        /// </summary>
        IOrderInfo OrderInfo { get; }

        /// <summary>
        /// Line quantity, amount of items represented by this order line.
        /// </summary>
        /// <value>
        /// Line quantity, amount of items represented by this order line.
        /// </value>
        int Quantity { get; }
        decimal Vat { get; }
        OrderLineSettings Settings { get; }
    }
}

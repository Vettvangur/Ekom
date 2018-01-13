using Ekom.Models;
using Ekom.Models.Discounts;
using System;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    /// <summary>
    /// Order/Cart
    /// </summary>
    public interface IOrderInfo
    {
        /// <summary>
        /// Force changes to come through order api, 
        /// api can then make checks to ensure that a discount is only ever applied to either cart or items, never both.
        /// </summary>
        IDiscount Discount { get; }
        /// <summary>
        /// 
        /// </summary>
        string Coupon { get; }

        /// <summary>
        /// Gets the uniqueId.
        /// </summary>
        /// <value>
        /// The uniqueId.
        /// </value>
        Guid UniqueId { get; }

        /// <summary>
        /// Gets the quantity.
        /// </summary>
        /// <value>
        /// The quantity.
        /// </value>
        int Quantity { get; }

        /// <summary>
        /// Gets the store info.
        /// </summary>
        /// <value>
        /// The store info.
        /// </value>
        StoreInfo StoreInfo { get; }

        /// <summary>
        /// 
        /// </summary>
        IReadOnlyCollection<IOrderLine> OrderLines { get; }

        /// <summary>
        /// <see cref="Price"/> object for total value of all orderlines.
        /// </summary>
        Price ChargedAmount { get; }
    }
}

using Ekom.Models.Discounts;
using Ekom.Models.OrderedObjects;
using System;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    /// <summary>
    /// Umbraco discount node with coupons and <see cref="DiscountAmount"/>
    /// </summary>
    public interface IDiscount : IComparable<IDiscount>, IComparable<OrderedDiscount>
    {
        /// <summary>
        /// Discount amount in the specified <see cref="DiscountType"/>
        /// </summary>
        DiscountAmount Amount { get; }
        /// <summary>
        /// Ranges
        /// </summary>
        IConstraints Constraints { get; }
        /// <summary>
        /// 
        /// </summary>
        IReadOnlyCollection<string> Coupons { get; }
        /// <summary>
        /// Coupon code activations left
        /// </summary>
        bool HasMasterStock { get; }

        /// <summary>
        /// Called on coupon application
        /// </summary>
        event CouponEventHandler CouponApplied;

        /// <summary>
        /// Gets the unique key identifier.
        /// </summary>
        /// <value>
        /// The unique key identifier.
        /// </value>
        Guid Key { get; }
    }
}

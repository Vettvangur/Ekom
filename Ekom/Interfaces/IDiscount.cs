using Ekom.Models.Behaviors;
using System;
using System.Collections.Generic;

namespace Ekom.Models.Discounts
{
    /// <summary>
    /// Umbraco discount node with coupons and <see cref="DiscountAmount"/>
    /// </summary>
    public interface IDiscount : IComparable<IDiscount>, IPerStoreNodeEntity
    {
        /// <summary>
        /// Discount amount in the specified <see cref="DiscountType"/>
        /// </summary>
        DiscountAmount Amount { get; }
        /// <summary>
        /// Ranges
        /// </summary>
        Constraints Constraints { get; }
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
    }
}

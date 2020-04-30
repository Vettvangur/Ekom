using Ekom.Models;
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
        /// If the discount can be applied ontop of product discounts
        /// </summary>
        bool Stackable { get; }
        /// <summary>
        /// Called on coupon application
        /// </summary>
        event CouponEventHandler CouponApplied;
        /// <summary>
        /// The products that are in this discount;
        /// </summary>
        List<Guid> DiscountItems { get; }
        /// <summary>
        /// Gets the unique key identifier.
        /// </summary>
        /// <value>
        /// The unique key identifier.
        /// </value>
        Guid Key { get; }

        List<CurrencyValue> Discounts { get; }

        /// <summary>
        /// Umbraco node properties
        /// </summary>
        IReadOnlyDictionary<string, string> Properties { get; }
    }
}

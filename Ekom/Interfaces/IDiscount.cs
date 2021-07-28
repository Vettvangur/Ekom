using Ekom.Models;
using Ekom.Models.Discounts;
using Ekom.Models.OrderedObjects;
using System;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    /// <summary>
    /// The base discount interface, common to ordered discounts, order discounts and product discounts
    /// </summary>
    public interface IDiscount : IComparable<IDiscount>
    {
        /// <summary>
        /// Fixed or percentage?
        /// </summary>
        DiscountType Type { get; }
        /// <summary>
        /// Discount amount in the specified <see cref="DiscountType"/>
        ///
        /// Percentage example:
        /// Umbraco input: 28.5 <para></para>
        /// Stored value: 0.285<para></para>
        /// Effective value: 28.5%<para></para>
        /// </summary>
        decimal Amount { get; }
        /// <summary>
        /// Ranges
        /// </summary>
        IConstraints Constraints { get; }
        /// <summary>
        /// Coupon code activations left
        /// </summary>
        bool HasMasterStock { get; }
        /// <summary>
        /// If the discount can be applied ontop of product discounts.
        /// Discount stacking = Applying discounts to specific OrderLine's while applying a seperate discount to the order and general order items
        /// </summary>
        bool Stackable { get; }
        /// <summary>
        /// The products that are in this discount;
        /// </summary>
        IReadOnlyCollection<string> DiscountItems { get; }
        /// <summary>
        /// Gets the unique key identifier.
        /// </summary>
        /// <value>
        /// The unique key identifier.
        /// </value>
        Guid Key { get; }
        bool GlobalDiscount { get; }
    }
}

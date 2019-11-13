using Ekom.Interfaces;
using Ekom.Models.Behaviors;
using Ekom.Models.Discounts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Ekom.Models.OrderedObjects
{
    /// <summary>
    /// Frozen <see cref="Discount"/> with coupons and <see cref="DiscountAmount"/>
    /// </summary>
    public class OrderedDiscount : IComparable<IDiscount>, IDiscount
    {
        /// <summary>
        /// 
        /// </summary>
        [JsonConstructor]
        public OrderedDiscount(
            Guid key,
            bool stackable,
            DiscountAmount amount,
            List<Guid> discountItems,
            IConstraints constraints,
            IReadOnlyCollection<string> coupons,
            bool hasMasterStock)
        {
            Key = key;
            Stackable = stackable;
            DiscountItems = discountItems;
            Amount = amount;
            Constraints = constraints;
            Coupons = coupons;
            HasMasterStock = hasMasterStock;
        }

        /// <summary>
        /// ctor
        /// </summary>
        public OrderedDiscount(IDiscount discount)
        {
            discount = discount ?? throw new ArgumentNullException(nameof(discount));
            Stackable = discount.Stackable;
            Key = discount.Key;
            DiscountItems = discount.DiscountItems.ToList().AsReadOnly();
            Amount = discount.Amount;
            Constraints = new Constraints(discount.Constraints);
            Coupons = new List<string>(discount.Coupons);
            HasMasterStock = discount.HasMasterStock;
        }

        /// <summary>
        /// 
        /// </summary>
        public Guid Key { get; internal set; }

        /// <summary>
        /// Discount amount in the specified <see cref="DiscountType"/>
        /// </summary>
        public DiscountAmount Amount { get; internal set; }

        public IReadOnlyCollection<Guid> DiscountItems { get; }
        /// <summary>
        /// If discount is stackable with productDiscounts
        /// </summary>
        public bool Stackable { get; }
        /// <summary>
        /// Ranges
        /// </summary>
        public IConstraints Constraints { get; internal set; }
        /// <summary>
        /// 
        /// </summary>
        public IReadOnlyCollection<string> Coupons { get; internal set; }

        /// <summary>
        /// Coupon code activations left
        /// </summary>
        public bool HasMasterStock { get; internal set; }

        #region Comparisons
        /// <summary>
        /// <see cref="IComparable{T}"/> implementation
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(IDiscount other)
        {
            if (other == null)
                return 1;

            else if (Amount.Type != other.Amount.Type)
                throw new FormatException("Discounts are not equal, please compare type before comparing value.");
            else if (Amount.Amount == other.Amount.Amount)
                return 0;
            else if (Amount.Amount > other.Amount.Amount)
                return 1;
            else
                return -1;
        }
        /// <summary>
        /// <see cref="IComparable{T}"/> implementation
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(OrderedDiscount other)
        {
            if (other == null)
                return 1;

            else if (Amount.Type != other.Amount.Type)
                throw new FormatException("Discounts are not equal, please compare type before comparing value.");
            else if (Amount.Amount == other.Amount.Amount)
                return 0;
            else if (Amount.Amount > other.Amount.Amount)
                return 1;
            else
                return -1;
        }

        /// <summary>
        /// Operator overloading
        /// </summary>
        /// <param name="d1"></param>
        /// <param name="d2"></param>
        /// <returns></returns>
        [Obsolete("Unused")]
        public static bool operator <(OrderedDiscount d1, IDiscount d2)
        {
            return d1.CompareTo(d2) < 0;
        }

        /// <summary>
        /// Operator overloading
        /// </summary>
        /// <param name="d1"></param>
        /// <param name="d2"></param>
        /// <returns></returns>
        [Obsolete("Unused")]
        public static bool operator >(OrderedDiscount d1, IDiscount d2)
        {
            return d1.CompareTo(d2) > 0;
        }
        #endregion

    }
}

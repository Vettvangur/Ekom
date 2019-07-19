using Ekom.Interfaces;
using Ekom.Models.Behaviors;
using Ekom.Models.Discounts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace Ekom.Models.OrderedObjects
{
    /// <summary>
    /// Frozen <see cref="Discount"/> with coupons and <see cref="DiscountAmount"/>
    /// </summary>
    public class OrderedDiscount : IComparable<IDiscount>
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
            Constraints constraints,
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
            DiscountItems = discount.DiscountItems;
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

        public List<Guid> DiscountItems { get; }
        /// <summary>
        /// Ranges
        /// </summary>
        public Constraints Constraints { get; internal set; }
        /// <summary>
        /// If discount is stackable with productDiscounts
        /// </summary>
        public bool Stackable { get; }
        /// <summary>
        /// 
        /// </summary>
        public IReadOnlyCollection<string> Coupons { get; internal set; }

        /// <summary>
        /// Coupon code activations left
        /// </summary>
        public bool HasMasterStock { get; internal set; }

        /// <summary>
        /// <see cref="IComparable{T}"/> implementation
        /// </summary>
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

        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}

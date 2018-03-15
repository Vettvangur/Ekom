using Ekom.Interfaces;
using Ekom.Models.Behaviors;
using Ekom.Models.Discounts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

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
        public OrderedDiscount(Guid key, DiscountAmount amount, IConstraints constraints, IReadOnlyCollection<string> coupons, bool hasMasterStock)
        {
            Key = key;
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
            Key = discount.Key;
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
    }
}

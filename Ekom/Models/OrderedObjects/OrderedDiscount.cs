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
            decimal amount,
            DiscountType type,
            List<Guid> discountItems,
            Constraints constraints,
            List<string> coupons,
            bool hasMasterStock)
        {
            Key = key;
            Stackable = stackable;
            DiscountItems = discountItems;
            Amount = amount;
            Type = type;
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
            Type = discount.Type;
            Constraints = new Constraints(discount.Constraints);
            Coupons = new List<string>(discount.Coupons);
            HasMasterStock = discount.HasMasterStock;
        }

        /// <summary>
        /// 
        /// </summary>
        public Guid Key { get; internal set; }

        public DiscountType Type { get; internal set; }

        public decimal Amount { get; internal set; }

        public IReadOnlyCollection<Guid> DiscountItems { get; }
        /// <summary>
        /// Ranges
        /// </summary>
        public IConstraints Constraints { get; internal set; }
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

            else if (Type != other.Type)
                throw new FormatException("Discounts are not equal, please compare type before comparing value.");
            else if (Amount == other.Amount)
                return 0;
            else if (Amount > other.Amount)
                return 1;
            else
                return -1;
        }
    }
}

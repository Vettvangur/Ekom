using Ekom.Interfaces;
using Ekom.Models.Behaviors;
using Ekom.Utilities;
using Examine;
using System;
using System.Collections.Generic;
using Umbraco.Core.Models;

namespace Ekom.Models.Discounts
{
    /// <summary>
    /// Umbraco discount node with coupons and <see cref="DiscountAmount"/>
    /// </summary>
    class Discount : PerStoreNodeEntity, IConstrained, IDiscount
    {
        public Constraints Constraints { get; private set; }
        public DiscountAmount Amount { get; private set; }

        internal string[] CouponsInternal;
        public IReadOnlyCollection<string> Coupons => Array.AsReadOnly(CouponsInternal);


        /// <summary>
        /// Coupon code activations left
        /// </summary>
        public bool HasMasterStock => Properties.GetPropertyValue("masterStock").ConvertToBool();

        /// <summary>
        /// Used by Ekom extensions
        /// </summary>
        public Discount(Store store) : base(store)
        {
            Construct();
        }

        /// <summary>
        /// Construct ShippingProvider from Examine item
        /// </summary>
        public Discount(SearchResult item, Store store) : base(item, store)
        {
            Construct();
        }

        /// <summary>
        /// Construct ShippingProvider from umbraco publish event
        /// </summary>
        public Discount(IContent node, Store store) : base(node, store)
        {
            Construct();
        }

        private void Construct()
        {
            Constraints = new Constraints(this);
            var couponsInternal = Properties.GetPropertyValue("coupons");

            CouponsInternal = couponsInternal?.Split(',');

            DiscountType type = DiscountType.Fixed;

            switch (Properties.GetPropertyValue("discountType"))
            {
                case "Fixed":
                    break;

                case "Percentage":
                    type = DiscountType.Percentage;
                    break;
            }

            Amount = new DiscountAmount
            {
                Amount = Convert.ToDecimal(Properties.GetPropertyValue("discountAmount")) / 100,
                Type = type,
            };
        }

        internal void OnCouponApply() => CouponApplied?.Invoke(this);

        /// <summary>
        /// Called on coupon application
        /// </summary>
        public event CouponEventHandler CouponApplied;

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
        /// Operator overloading
        /// </summary>
        /// <param name="d1"></param>
        /// <param name="d2"></param>
        /// <returns></returns>
        public static bool operator <(Discount d1, IDiscount d2)
        {
            return d1.CompareTo(d2) < 0;
        }

        /// <summary>
        /// Operator overloading
        /// </summary>
        /// <param name="d1"></param>
        /// <param name="d2"></param>
        /// <returns></returns>
        public static bool operator >(Discount d1, IDiscount d2)
        {
            return d1.CompareTo(d2) > 0;
        }
        #endregion

    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="d"></param>
    public delegate void CouponEventHandler(IDiscount d);
}

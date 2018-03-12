using Ekom.Interfaces;
using Ekom.Models.Behaviors;
using Ekom.Models.OrderedObjects;
using Ekom.Utilities;
using Examine;
using log4net;
using System;
using System.Collections.Generic;
using System.Reflection;
using Umbraco.Core;
using Umbraco.Core.Models;

namespace Ekom.Models.Discounts
{
    /// <summary>
    /// Umbraco discount node with coupons and <see cref="DiscountAmount"/>
    /// </summary>
    class Discount : PerStoreNodeEntity, IConstrained, IDiscount, IPerStoreNodeEntity
    {
        public virtual IConstraints Constraints { get; private set; }
        public virtual DiscountAmount Amount { get; private set; }

        internal string[] couponsInternal;
        public virtual IReadOnlyCollection<string> Coupons
            => Array.AsReadOnly(couponsInternal ?? new string[0]);
        internal List<IProduct> discountItems = new List<IProduct>();
        public virtual IReadOnlyCollection<IProduct> DiscountItems => discountItems.AsReadOnly();

        /// <summary>
        /// Coupon code activations left
        /// </summary>
        public virtual bool HasMasterStock => Properties.GetPropertyValue("masterStock").ConvertToBool();

        /// <summary>
        /// Used by Ekom extensions
        /// </summary>
        public Discount(IStore store) : base(store)
        {
            Construct();
        }

        /// <summary>
        /// Construct ShippingProvider from Examine item
        /// </summary>
        public Discount(SearchResult item, IStore store) : base(item, store)
        {
            Construct();
        }

        /// <summary>
        /// Construct ShippingProvider from umbraco publish event
        /// </summary>
        public Discount(IContent node, IStore store) : base(node, store)
        {
            Construct();
        }

        private void Construct()
        {
            Constraints = new Constraints(this);

            // Not applicable while coupons are represented as umbraco nodes
            //var couponsInternal = Properties.GetPropertyValue("coupons");

            //CouponsInternal = couponsInternal?.Split(',');

            var discountAmount = Convert.ToDecimal(Properties.GetPropertyValue("discount"));

            DiscountType type = DiscountType.Fixed;

            switch (Properties.GetPropertyValue("discountType"))
            {
                case "Fixed":
                    break;

                case "Percentage":
                    type = DiscountType.Percentage;
                    discountAmount /= 100;
                    break;
            }


            Amount = new DiscountAmount
            {
                Amount = discountAmount,
                Type = type,
            };

            var discountItemsProp = Properties.GetPropertyValue("discountItems", Store.Alias);

            if (!string.IsNullOrEmpty(discountItemsProp))
            {
                foreach (var discountItem in discountItemsProp.Split(','))
                {
                    if (GuidUdi.TryParse(discountItem, out var udi))
                    {
                        var product = API.Catalog.Instance.GetProduct(Store.Alias, udi.Guid);

                        if (product != null)
                        {
                            discountItems.Add(product);

                            // Link discount to product
                            // If a previous discount exists on product, it's setter will determine if discount is better than previous one 
                            if (product is Product productItem)
                            {
                                productItem.Discount = this;
                            }
                        }
                    }
                }
            }
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
        [Obsolete("Unused")]
        public static bool operator >(Discount d1, IDiscount d2)
        {
            return d1.CompareTo(d2) > 0;
        }
        #endregion

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="d"></param>
    public delegate void CouponEventHandler(IDiscount d);
}

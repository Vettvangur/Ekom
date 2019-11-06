using Ekom.Interfaces;
using Ekom.Models.Behaviors;
using Ekom.Models.OrderedObjects;
using Ekom.Utilities;
using Examine;
using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.Composing;

namespace Ekom.Models.Discounts
{
    /// <summary>
    /// Umbraco discount node with coupons and <see cref="DiscountAmount"/>
    /// </summary>
    public class Discount : PerStoreNodeEntity, IConstrained, IDiscount, IPerStoreNodeEntity
    {
        protected virtual UmbracoHelper UmbHelper => Current.Factory.GetInstance<UmbracoHelper>();
        protected virtual IDataTypeService DataTypeService => Current.Factory.GetInstance<IDataTypeService>();
        public virtual IConstraints Constraints { get; protected set; }
        public virtual DiscountAmount Amount { get; protected set; }

        internal string[] couponsInternal;
        public virtual IReadOnlyCollection<string> Coupons
            => Array.AsReadOnly(couponsInternal ?? new string[0]);
        internal List<Guid> discountItems = new List<Guid>();
        public virtual List<Guid> DiscountItems => discountItems;

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
        public Discount(ISearchResult item, IStore store) : base(item, store)
        {
            Construct();
        }

        /// <summary>
        /// Construct Discount from umbraco publish event
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

            var typeValue = Properties.GetPropertyValue("type");

            if (int.TryParse(typeValue, out int typeValueInt))
            {
                var dt = DataTypeService.GetDataType(typeValueInt);

                // FIX: verify
                typeValue = dt.ConfigurationAs<string>();
            }

            DiscountType type = DiscountType.Fixed;

            switch (typeValue)
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

            var discountItemsVal = Properties.GetPropertyValue("discountItems", Store.Alias);

            if (!string.IsNullOrEmpty(discountItemsVal))
            {
                var nodes = discountItemsVal.Split(',').Select(x => UmbHelper.Content(GuidUdiHelper.GetGuid(x))).ToList();

                foreach (var node in nodes)
                {
                    if (node.ContentType.Alias == "ekmProduct")
                    {
                        discountItems.Add(node.Key);
                    }
                    if (node.ContentType.Alias == "ekmCategory")
                    {
                        discountItems.AddRange(
                            node.Descendants()
                                .Where(x => x.ContentType.Alias == "ekmProduct")
                                .Select(x => x.Key));
                    }
                }
            }

            //if (!string.IsNullOrEmpty(discountItemsProp))
            //{
            //    foreach (var discountItem in discountItemsProp.Split(','))
            //    {
            //        if (GuidUdi.TryParse(discountItem, out var udi))
            //        {
            //            var product = API.Catalog.Instance.GetProduct(Store.Alias, udi.Guid);

            //            if (product != null)
            //            {
            //                discountItems.Add(product);

            //                // Link discount to product
            //                // If a previous discount exists on product, it's setter will determine if discount is better than previous one 
            //                if (product is Product productItem)
            //                {
            //                    Log.Debug($"Linking product {productItem.Title} with key {productItem.Key} to discount {Title} with key {Key}");
            //                    productItem.Discount = this;
            //                }
            //            }
            //        }
            //    }
            //}
        }

        internal void OnCouponApply() => CouponApplied?.Invoke(this);

        /// <summary>
        /// Called on coupon application
        /// </summary>
        public event CouponEventHandler CouponApplied;
        /// <summary>
        /// If the discount can be used with productdiscounts
        /// </summary>
        public virtual bool Stackable => Properties.GetPropertyValue("stackable", Store.Alias).ConvertToBool();
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
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="d"></param>
    public delegate void CouponEventHandler(IDiscount d);
}

using Ekom.Interfaces;
using Ekom.Models.Behaviors;
using Ekom.Models.OrderedObjects;
using Ekom.Utilities;
using Examine;
using Newtonsoft.Json;
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
        public virtual DiscountType Type
        {
            get
            {
                var typeValue = Properties.GetPropertyValue("type");

                if (int.TryParse(typeValue, out int typeValueInt))
                {
                    var dt = DataTypeService.GetDataType(typeValueInt);

                    // FIX: verify
                    typeValue = dt.ConfigurationAs<string>();
                }

                switch (typeValue)
                {
                    case "Fixed":
                        return DiscountType.Fixed;

                    case "Percentage":
                        return DiscountType.Percentage;
                    default:
                        return DiscountType.Fixed;
                }
            }

        }
        public virtual DiscountAmount Amount
        {
            get
            {

                decimal discountAmount = 0;

                var discount = Discounts.FirstOrDefault();

                var currency = CookieHelper.GetCurrencyCookieValue(Store.Currencies, Store.Alias);

                if (Discounts.Any(x => x.Currency == currency.CurrencyValue))
                {
                    discount = Discounts.FirstOrDefault(x => x.Currency == currency.CurrencyValue);
                }

                discountAmount = Convert.ToDecimal(discount.Value);

                if (Type == DiscountType.Percentage)
                {
                    discountAmount /= 100;
                }

                return new DiscountAmount
                {
                    Amount = discountAmount,
                    Type = Type,
                };
            }
            set
            {

            }
        }

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
        /// Used in unit tests
        /// </summary>
        /// <param name="store">The store.</param>
        /// <param name="json">The json.</param>
        internal Discount(IStore store, string json) :base(store) 
        {
            _properties = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
        }

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

            var nodes = Properties.GetPropertyValue("discountItems")
                .Split(',')
                .Select(x => UmbHelper.Content(GuidUdiHelper.GetGuid(x))).ToList();


            foreach (var node in nodes)
            {
                var descendants = node.Descendants().ToList();

                if (node.ContentType.Alias == "ekmProduct")
                {
                    discountItems.Add(node.Key);

                    if (descendants.Any())
                    {
                        discountItems.AddRange(
                            descendants
                                .Where(x => x.ContentType.Alias == "ekmProductVariant")
                                .Select(x => x.Key));
                    }
                }
                if (node.ContentType.Alias == "ekmCategory")
                {
                    discountItems.AddRange(descendants.Where(x => x.ContentType.Alias == "ekmProduct").Select(x => x.Key));
                }
            }
        }

        public virtual List<CurrencyValue> Discounts
        {
            get
            {
                return Properties.GetPropertyValue("discount", Store.Alias).GetCurrencyValues();
            }
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

using Ekom.Services;
using Ekom.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ekom.Models
{
    /// <summary>
    /// Umbraco order discount node
    /// </summary>
    public class Discount : PerStoreNodeEntity, IConstrained, IDiscount, IPerStoreNodeEntity
    {
        public virtual IConstraints Constraints { get; protected set; }
        public virtual DiscountType Type
        {
            get
            {
                var typeValue = Properties.GetPropertyValue("type");

                var umbSvc = Configuration.Resolver.GetService<IUmbracoService>();
                var dt = umbSvc.GetDataType(typeValue);

                switch (dt)
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
        public virtual decimal Amount
        {
            get
            {
                decimal discountAmount = 0;

                var currency = CookieHelper.GetCurrencyCookieValue(Store.Currencies, Store.Alias);

                var discount = Discounts.FirstOrDefault(x => x.Currency == currency.CurrencyValue)
                    ?? Discounts.First();

                discountAmount = Convert.ToDecimal(discount.Value);

                if (Type == DiscountType.Percentage)
                {
                    discountAmount /= 100;
                }

                return discountAmount;
            }
        }

        public virtual IReadOnlyCollection<string> DiscountItems
        {
            get
            {
                // Im returning INT instead of GUID if we would like to query by Path that is stored as comma seperate int
                var returnList = new List<string>();

                var umbSvc = Configuration.Resolver.GetService<IUmbracoService>();

                var nodes = Properties.GetPropertyValue("discountItems");

                returnList.AddRange(umbSvc.GetContent(nodes));

                //foreach (var node in nodes.Where(x => x != null))
                //{
                //    returnList.Add(node.Key);
                //    if (node.Children.Any())
                //    {
                //        returnList.AddRange(
                //            node.Descendants()
                //                .Where(x => x.ContentType.Alias == "ekmProduct"
                //                    || x.ContentType.Alias == "ekmProductVariant")
                //                .Select(x => x.Key));
                //    }
                //}

                return returnList.AsReadOnly();
            }
        }

        /// <summary>
        /// Means this couponless discount will be automatically applied to orders that match it's constraints
        /// We can not currently filter by discounts without a coupon since the linking is from Coupon -> Order.
        /// </summary>
        public bool GlobalDiscount => Properties.GetPropertyValue("globalDiscount").ConvertToBool();

        /// <summary>
        /// Coupon code activations left
        /// </summary>
        public virtual bool HasMasterStock => Properties.GetPropertyValue("masterStock").ConvertToBool();

        /// <summary>
        /// Used in unit tests
        /// </summary>
        /// <param name="store">The store.</param>
        /// <param name="json">The json.</param>
        internal Discount(IStore store, string json) : base(store)
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
        /// Construct ShippingProvider from IPublishedContent item
        /// </summary>
        public Discount(UmbracoContent item, IStore store) : base(item, store)
        {
            Construct();
        }

        private void Construct()
        {
            Constraints = new Constraints(this);
        }

        public IReadOnlyCollection<CurrencyValue> Discounts
        {
            get
            {
                return Properties
                    .GetPropertyValue("discount", Store.Alias)
                    .GetCurrencyValues()
                    .AsReadOnly();
            }
        }

        internal void OnCouponApply() => CouponApplied?.Invoke(this);

        /// <summary>
        /// Called on coupon application
        /// </summary>
        public event CouponEventHandler CouponApplied;
        /// <summary>
        /// If the discount can be used with product discounts
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

            else if (Type != other.Type)
                throw new FormatException("Discounts are not equal, please compare type before comparing value.");
            else if (Amount == other.Amount)
                return 0;
            else if (Amount > other.Amount)
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

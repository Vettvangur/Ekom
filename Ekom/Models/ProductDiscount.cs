using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models.Discounts;
using Ekom.Utilities;
using Examine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Web;

namespace Ekom.Models
{
    public class ProductDiscount : PerStoreNodeEntity, IProductDiscount
    {

        protected virtual UmbracoHelper UmbHelper => Current.Factory.GetInstance<UmbracoHelper>();
        protected virtual IDataTypeService DataTypeService => Current.Factory.GetInstance<IDataTypeService>();

        /// <summary>
        /// Used by Ekom extensions, keep logic empty to allow full customisation of object construction.
        /// </summary>
        /// <param name="store"></param>
        public ProductDiscount(IStore store) : base(store) { }

        /// <summary>
        /// Construct Product from Examine item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        public ProductDiscount(ISearchResult item, IStore store) : base(item, store)
        {

        }

        public ProductDiscount(IContent node, IStore store) : base(node, store)
        {

        }

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
        public virtual decimal Discount
        {

            get
            {
                if (!Discounts.Any())
                {
                    return 0;
                }

                var discount = Discounts.FirstOrDefault();

                var currency = CookieHelper.GetCurrencyCookieValue(Store.Currencies, Store.Alias);

                if (Discounts.Any(x => x.Currency == currency.CurrencyValue))
                {
                    discount = Discounts.FirstOrDefault(x => x.Currency == currency.CurrencyValue);
                }

                if (discount.Value <= 0)
                {
                    return 0;
                }
                else
                {
                    decimal value = discount.Value;
                    value = value * 0.01M;
                    if (value > 1)
                    {
                        return value * 0.01M;
                    }
                    return value;
                }

            }

        }

        public virtual List<CurrencyValue> Discounts
        {
            get
            {
                var items = Properties.GetPropertyValue("discount", Store.Alias).GetCurrencyValues();

                return items;
            }
        }


        public List<Guid> DiscountItems
        {
            get
            {
                List<Guid> returnList = new List<Guid>();

                var nodes = Properties.GetPropertyValue("discountItems")
                    .Split(',')
                    .Select(x => UmbHelper.Content(GuidUdiHelper.GetGuid(x))).ToList();

                foreach (var node in nodes.Where(x => x != null))
                {
                    returnList.Add(node.Key);
                    if (node.Children.Any())
                    {
                        returnList.AddRange(node.Descendants().Where(x => x.ContentType.Alias == "ekmProduct" || x.ContentType.Alias == "ekmCategory" || x.ContentType.Alias == "ekmProductVariant").Select(x => x.Key));
                    }
                }
                return returnList;
            }
        }
        /// <summary>
        /// Simple <see cref="ICloneable"/> implementation using object.MemberwiseClone
        /// </summary>
        /// <returns></returns>
        public object Clone() => MemberwiseClone();

        public virtual decimal StartOfRange
        {
            get
            {

                var ranges = Properties.GetPropertyValue("startOfRange", Store.Alias).GetCurrencyValues();

                if (ranges != null && ranges.Any())
                {
                    var rangeItem = ranges.FirstOrDefault();

                    var currency = CookieHelper.GetCurrencyCookieValue(Store.Currencies, Store.Alias);

                    if (ranges.Any(x => x.Currency == currency.CurrencyValue))
                    {
                        rangeItem = ranges.FirstOrDefault(x => x.Currency == currency.CurrencyValue);
                    }

                    return rangeItem.Value;
                }

                return 0;

            }
        }

        public virtual decimal EndOfRange
        {
            get
            {

                var ranges = Properties.GetPropertyValue("endOfRange", Store.Alias).GetCurrencyValues();

                if (ranges != null && ranges.Any())
                {
                    var rangeItem = ranges.FirstOrDefault();

                    var currency = CookieHelper.GetCurrencyCookieValue(Store.Currencies, Store.Alias);

                    if (ranges.Any(x => x.Currency == currency.CurrencyValue))
                    {
                        rangeItem = ranges.FirstOrDefault(x => x.Currency == currency.CurrencyValue);
                    }

                    return rangeItem.Value;
                }

                return 0;

            }
        }
        public virtual bool Disabled
        {
            get
            {
                return Properties.GetPropertyValue("disable", Store.Alias) == "1";
            }

        }

    }
}

using Ekom.Utilities;
using System;
using System.Linq;

namespace Ekom.Models
{
    public class ProductDiscount : Discount, IProductDiscount
    {
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
        public ProductDiscount(UmbracoContent content, IStore store) : base(content, store)
        {

        }

        public override decimal Amount
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

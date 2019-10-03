using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Discounts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ekom.Tests.Objects
{
    public class CustomProductDiscount : ProductDiscount
    {
        public override decimal Discount
        {
            get
            {
                var discount = GetPropertyValue("discount", Store.Alias);
                if (string.IsNullOrEmpty(discount))
                {
                    return 0;
                }
                else
                {
                    if (Store.Alias == "IS")
                    {

                    }
                    decimal value;
                    Decimal.TryParse(discount.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
                    if (Type == DiscountType.Fixed)
                    {
                        return value;
                    }
                    value = value * 0.01M;
                    if (value > 1)
                    {
                        return value * 0.01M;
                    }
                    return value;
                }

            }
        }
        public override DiscountType Type
        {
            get
            {
                var f = GetPropertyValue("type");
                switch (GetPropertyValue("type"))
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
        public override decimal StartOfRange
        {
            get
            {
                var discount = GetPropertyValue("startOfRange", Store.Alias);
                if (string.IsNullOrEmpty(discount))
                {
                    return 0;
                }
                else
                {

                    decimal value;
                    Decimal.TryParse(discount.Replace(',', '.'), out value);
                    return value;
                }
            }
        }
        public override decimal EndOfRange
        {
            get
            {
                var discount = GetPropertyValue("endOfRange", Store.Alias);
                if (string.IsNullOrEmpty(discount))
                {
                    return 0;
                }
                else
                {
                    decimal value;
                    Decimal.TryParse(discount.Replace(',', '.'), out value);
                    return value;
                }
            }
        }
        public override bool Disabled => GetPropertyValue("disable", Store.Alias) == "1";

        public CustomProductDiscount(IStore store, string json) : base(store)
        {
            //var f = JsonConvert.DeserializeObject<CustomProductDiscount>(json);
            _properties = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
        }
    }
}

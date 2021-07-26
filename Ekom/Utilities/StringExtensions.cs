using Ekom.Interfaces;
using Ekom.JsonDotNet;
using Ekom.Models;
using Ekom.Models.Discounts;
using Ekom.Models.OrderedObjects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Our.Umbraco.Vorto.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Umbraco.Core.Composing;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core;

namespace Ekom.Utilities
{
    public static class StringExtension
    {
        private static string RemoveAccent(this string txt)
        {
            byte[] bytes = Encoding.GetEncoding("Cyrillic").GetBytes(txt);
            return Encoding.ASCII.GetString(bytes);
        }

        /// <summary>
        /// Ensure string ends in one and only one '/'
        /// </summary>
        /// <param name="value">String to examine</param>
        /// <returns>String ending in one and only one '/'</returns>
        public static string AddTrailing(this string value)
        {
            if (value.Length == 0)
            {
                return "/";
            }
            else if (value[value.Length - 1] != '/')
            {
                value += "/";
            }

            return value;
        }

        /// <summary>
        /// Coerces a string to a boolean value, case insensitive and also registers true for 1 and y
        /// </summary>
        /// <param name="value">String value to convert to bool</param>
        public static bool ConvertToBool(this string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                var bVal = bool.TryParse(value, out bool result);

                if (bVal)
                {
                    return result;
                }
                else
                {
                    return (value == "1" || value.ToLower() == "y");
                }
            }

            return false;
        }

        public static bool IsVortoValue(this string value)
        {

            try
            {
                if (value.IsJson())
                {
                    var o = JsonConvert.DeserializeObject<VortoValue>(value);

                    return true;
                }

            }
            catch { }

            return false;

        }

        // Maybe this should return T and not force String
        public static string GetVortoValue(this string value, string storeAlias)
        {

            if (!string.IsNullOrEmpty(value))
            {

                if (value.IsJson())
                {
                    VortoValue o = null;
                    try
                    {
                        o = JsonConvert.DeserializeObject<VortoValue>(value);
                    }
                    catch { }

                    if (o != null)
                    {
                        object itemValue = null;

                        var foundValue = o.Values?.TryGetValue(storeAlias, out itemValue);

                        if (foundValue == true)
                        {
                            if (itemValue != null)
                            {
                                return itemValue.ToString();
                            }
                        }
                    }
                }
                else
                {
                    return value;
                }
            }

            JObject parsed = null;
            try
            {
                parsed = JObject.Parse(value);
            }
            catch { }

            JToken itemVal = null;
            if (parsed?.TryGetValue(storeAlias, out itemVal) == true)
            {
                return itemVal.ToString();
            }

            return string.Empty;
        }

        public static List<IPrice> GetPriceValuesConstructed(this string priceJson, decimal vat, bool vatIncludedInPrice, CurrencyModel fallbackCurrency = null)
        {
            var prices = new List<IPrice>();

            try
            {
                var _prices = JArray.Parse(priceJson);

                foreach (var price in _prices)
                {
                    var currency = price["Currency"].ToObject<CurrencyModel>(EkomJsonDotNet.serializer);

                    prices.Add(new Price(price, currency, vat, vatIncludedInPrice));
                }
            }
            catch
            {
                if (fallbackCurrency == null)
                {
                    var store = API.Store.Instance.GetStore();

                    fallbackCurrency = store.Currency;
                }

                prices = new List<IPrice>
                {
                    new Price(priceJson, fallbackCurrency, vat, vatIncludedInPrice)
                };
            }

            return prices;
        }

        public static List<IPrice> GetPriceValues(
            this string priceJson,
            List<CurrencyModel> storeCurrencies,
            decimal vat,
            bool vatIncludedInPrice,
            CurrencyModel fallbackCurrency = null,
            string storeAlias = null,
            string path = null,
            string[] categories = null
            )
        {
            var prices = new List<IPrice>();

            try
            {
                var _prices = JArray.Parse(priceJson);

                foreach (var price in _prices)
                {
                    var currencyValue = price["Currency"].Value<string>();
                    var currency = storeCurrencies.FirstOrDefault(x => x.CurrencyValue == currencyValue) ?? storeCurrencies.FirstOrDefault();

                    IDiscount productDiscount = !string.IsNullOrEmpty(path) 
                        ? Current.Factory.GetInstance<IProductDiscountService>()
                            .GetProductDiscount(
                                path,
                                storeAlias,
                                price["Price"].Value<string>(),
                                categories
                            ) 
                        : null;

                    prices.Add(new Price(
                        price["Price"].Value<string>(),
                        currency,
                        vat,
                        vatIncludedInPrice,
                        productDiscount != null 
                            ? new OrderedDiscount(productDiscount) 
                            : null)
                    );
                }
            }
            catch (JsonException)
            {
                if (fallbackCurrency == null)
                {
                    var store = API.Store.Instance.GetStore();

                    fallbackCurrency = store.Currency;
                }

                IDiscount productDiscount = !string.IsNullOrEmpty(path) 
                    ? Current.Factory.GetInstance<IProductDiscountService>()
                        .GetProductDiscount(
                            path,
                            storeAlias,
                            priceJson,
                            categories
                        ) 
                    : null;

                prices = new List<IPrice>
                {
                    new Price(
                        priceJson,
                        fallbackCurrency,
                        vat,
                        vatIncludedInPrice,
                        productDiscount != null 
                            ? new OrderedDiscount(productDiscount) 
                            : null)
                };
            }

            return prices;
        }

        public static List<CurrencyValue> GetCurrencyValues(this string priceJson)
        {
            var values = new List<CurrencyValue>();

            try
            {
                var _values = JArray.Parse(priceJson);

                foreach (var value in _values)
                {
                    var currencyValue = value["Currency"].Value<string>();
                    var val = value["Price"] != null ? value["Price"].Value<decimal>() : (value["Value"] != null ? value["Value"].Value<decimal>() : 0);

                    values.Add(new CurrencyValue(val, currencyValue));
                }
            }
            catch
            {
                if (decimal.TryParse(priceJson, out decimal value))
                {
                    var store = API.Store.Instance.GetStore();

                    values = new List<CurrencyValue>
                    {
                        new CurrencyValue(value, store.Currency.CurrencyValue)
                    };
                }
            }

            return values;
        }
        public static List<CurrencyPrice> GetCurrencyPrices(this string priceJson)
        {
            var values = new List<CurrencyPrice>();

            try
            {
                {
                    values = JsonConvert.DeserializeObject<List<CurrencyPrice>>(priceJson);
                }
            }
            catch
            {
                if (decimal.TryParse(priceJson, out decimal value))
                {
                    var store = API.Store.Instance.GetStore();

                    values = new List<CurrencyPrice>
                    {
                        new CurrencyPrice(value, store.Currency.CurrencyValue)
                    };
                }
            }

            return values;
        }

        public static bool IsJson(this string input)
        {
            input = input.Trim();
            return input.StartsWith("{") && input.EndsWith("}")
                   || input.StartsWith("[") && input.EndsWith("]");
        }

        public static IEnumerable<IPublishedContent> GetMediaNodes(this string nodeIds)
        {
            var list = new List<IPublishedContent>();

            if (!string.IsNullOrEmpty(nodeIds))
            {
                var imageIds = nodeIds.Split(',');

                foreach (var imgId in imageIds)
                {
                    var node = NodeHelper.GetMediaNode(imgId);

                    if (node != null)
                    {
                        list.Add(node);
                    }
                }

                return list;
            }

            return Enumerable.Empty<IPublishedContent>();
        }
        public static IEnumerable<Image> GetImages(this string nodeIds)
        {
            var list = new List<Image>();

            if (!string.IsNullOrEmpty(nodeIds))
            {
                var imageIds = nodeIds.Split(',');

                foreach (var imgId in imageIds)
                {
                    var node = NodeHelper.GetMediaNode(imgId);

                    if (node != null)
                    {
                        list.Add(new Image(node));
                    }
                }

                return list;
            }

            return Enumerable.Empty<Image>();
        }

        internal static bool IsBooleanTrue(this string value)
        {
            if (value == "1" || value.Equals("true", StringComparison.InvariantCultureIgnoreCase) || value.Equals("enable", StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}

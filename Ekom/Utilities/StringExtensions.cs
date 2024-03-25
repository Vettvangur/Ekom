using Ekom.Models;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

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
        public static bool IsJson(this string input)
        {
            input = input.Trim();
            return input.StartsWith("{") && input.EndsWith("}")
                   || input.StartsWith("[") && input.EndsWith("]");
        }

        internal static bool IsBoolean(this string? value)
        {
            if (!string.IsNullOrEmpty(value) && (value == "1" || value == "y" || value.Equals("true", StringComparison.InvariantCultureIgnoreCase) || value.Equals("enable", StringComparison.InvariantCultureIgnoreCase)))
            {
                return true;
            }
   
            return false;
            
        }
        // Maybe this should return T and not force String
        internal static string GetEkomPropertyEditorValue(this string value, string alias)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (!value.IsJson())
            {
                return value;
            }

            try
            {
                var obj = JObject.Parse(value);

                // Try getting the value directly by alias.
                if (obj.TryGetValue(alias, StringComparison.OrdinalIgnoreCase, out JToken directValue) && directValue != null)
                {
                    return directValue.ToString();
                }

                // Attempt to deserialize to PropertyValue and extract based on culture or alias.
                var propertyValue = obj.ToObject<PropertyValue>();
                if (propertyValue != null)
                {
                    // Prioritize direct alias match in PropertyValue.Values.
                    if (propertyValue.Values?.TryGetValue(alias, out object valAlias) == true && valAlias != null)
                    {
                        return valAlias.ToString();
                    }

                    // Fallback to current culture match in PropertyValue.Values.
                    var currentCultureName = CultureInfo.CurrentCulture.Name;
                    if (propertyValue.Values?.TryGetValue(currentCultureName, out object valCulture) == true && valCulture != null)
                    {
                        return valCulture.ToString();
                    }
                }
            }
            catch (JsonException ex)
            {
                // Consider logging the exception or handling it as needed.
                // Log.Error(ex, "Failed to parse JSON in GetEkomPropertyEditorValue.");
            }

            return string.Empty;
        }
        internal static List<IPrice> GetPriceValuesConstructed(this string priceJson, decimal vat, bool vatIncludedInPrice, CurrencyModel fallbackCurrency = null)
        {
            var prices = new List<IPrice>();

            try
            {
                var _prices = JArray.Parse(priceJson);

                foreach (var price in _prices)
                {
                    var currency = price[KeyExists(price, "Currency") ? "Currency" : "currency"].ToObject<CurrencyModel>(EkomJsonDotNet.serializer);

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
                    var currencyValue = price[KeyExists(price, "Currency") ? "Currency" : "currency"].Value<string>();
                    var currency = storeCurrencies.FirstOrDefault(x => x.CurrencyValue == currencyValue) ?? storeCurrencies.FirstOrDefault();

                    IDiscount productDiscount = !string.IsNullOrEmpty(path)
                        ? Configuration.Resolver.GetService<ProductDiscountService>()
                            .GetProductDiscount(
                                path,
                                storeAlias,
                                price[KeyExists(price, "Price") ? "Price" : "price"].Value<string>(),
                                categories
                            )
                        : null;

                    prices.Add(new Price(
                        price[KeyExists(price, "Price") ? "Price" : "price"].Value<string>(),
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
                    ? Configuration.Resolver.GetService<ProductDiscountService>()
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
        internal static List<CurrencyValue> GetCurrencyValues(this string priceJson)
        {
            var values = new List<CurrencyValue>();

            try
            {
                var _values = JArray.Parse(priceJson);

                foreach (var value in _values)
                {
                    if (KeyExists(value, "Currency"))
                    {
                        var currencyValue = value["Currency"].Value<string>();
                        var val = value["Price"] != null ? value["Price"].Value<decimal>() : (value["Value"] != null ? value["Value"].Value<decimal>() : 0);

                        values.Add(new CurrencyValue(val, currencyValue));
                    } else
                    {
                        var currencyValue = value["currency"].Value<string>();
                        var val = value["price"] != null ? value["price"].Value<decimal>() : (value["value"] != null ? value["value"].Value<decimal>() : 0);

                        values.Add(new CurrencyValue(val, currencyValue));
                    }
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
        
        internal static bool KeyExists(JToken token, string key)
        {
            JObject obj = token as JObject;
            return obj?.ContainsKey(key) ?? false;
        }

        internal static List<CurrencyPrice> GetCurrencyPrices(this string priceJson)
        {
            var values = new List<CurrencyPrice>();

            try
            {
                values = JsonConvert.DeserializeObject<List<CurrencyPrice>>(priceJson);
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

        public static IEnumerable<Image> GetImages(this string nodeIds, string storeAlias = null)
        {
            var list = new List<Image>();

            if (!string.IsNullOrEmpty(nodeIds))
            {
                if (nodeIds.StartsWith("[") && nodeIds.IndexOf("mediakey", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var imageList = JsonConvert.DeserializeObject<List<MediaCropImage>>(nodeIds);

                    foreach (var image in imageList)
                    {
                        var node = Configuration.Resolver.GetService<INodeService>()?.MediaById(image.MediaKey);

                        if (node != null)
                        {
                            list.Add(new Image(node, storeAlias));
                        }
                    }
                }
                else
                {
                    var imageIds = nodeIds.Split(',');

                    foreach (var imgId in imageIds)
                    {

                        var node = Configuration.Resolver.GetService<INodeService>()?.MediaById(imgId);

                        if (node != null)
                        {
                            list.Add(new Image(node, storeAlias));
                        }
                    }
                }
                return list;
            }

            return Enumerable.Empty<Image>();
        }

        public static string ToCamelCase(this string str)
        {
            var words = str.Split(new[] { "_", " " }, StringSplitOptions.RemoveEmptyEntries);
            var leadWord = Regex.Replace(words[0], @"([A-Z])([A-Z]+|[a-z0-9]+)($|[A-Z]\w*)",
                m =>
                {
                    return m.Groups[1].Value.ToLower() + m.Groups[2].Value.ToLower() + m.Groups[3].Value;
                });
            var tailWords = words.Skip(1)
                .Select(word => char.ToUpper(word[0]) + word.Substring(1))
                .ToArray();
            return $"{leadWord}{string.Join(string.Empty, tailWords)}";
        }
    }
}

using Ekom.Models;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Murmur;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Ekom.Utilities;

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
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

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
    public static string EnsureStartsAndEndsWithChar(this string input, char requiredChar)
    {
        if (!input.StartsWith(requiredChar))
        {
            return requiredChar + input;
        }
        if (!input.EndsWith(requiredChar))
        {
            return input + requiredChar;
        }
        return input;
    }
    public static string EnsureStartsWithChar(this string input, char requiredChar)
    {
        if (!input.StartsWith(requiredChar))
        {
            return requiredChar + input;
        }
        return input;
    }

    public static string EnsureEndsWithChar(this string input, char requiredChar)
    {
        if (!input.EndsWith(requiredChar))
        {
            return input + requiredChar;
        }
        return input;
    }

    /// <summary>
    /// Coerces a string to a boolean value, case insensitive and also registers true for 1 and y
    /// </summary>
    /// <param name="value">String value to convert to bool</param>
    public static bool ConvertToBool(this string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            bool bVal = bool.TryParse(value, out bool result);

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
    internal static string GetEkomPropertyEditorValue(this string value, string alias, bool fallback = false)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (!value.IsJson())
            return value;

        try
        {
            var token = JToken.Parse(value);

            if (token.Type == JTokenType.Object)
            {
                var obj = (JObject)token;

                // 1. Try getting the value directly by alias
                if (obj.TryGetValue(alias, StringComparison.OrdinalIgnoreCase, out var directToken) && directToken != null)
                {
                    if (directToken.Type == JTokenType.Object && directToken["markup"] != null)
                        return directToken["markup"]!.ToString();

                    return directToken.ToString();
                }

                // 2. Try getting from "values" dictionary inside the JSON
                if (obj.TryGetValue("values", StringComparison.OrdinalIgnoreCase, out var valuesToken) && valuesToken is JObject valuesObj)
                {
                    return GetValueFromValuesObject(valuesObj, alias, fallback);
                }
            }
            else if (token.Type == JTokenType.Array)
            {
                return value;
            }
        }
        catch (JsonException ex)
        {
            // Log or handle if needed
        }

        return value;
    }

    private static string GetValueFromValuesObject(JObject valuesObj, string alias, bool fallback)
    {
        if (!valuesObj.HasValues)
            return string.Empty;

        // Try alias first
        if (!string.IsNullOrEmpty(alias))
        {
            if (valuesObj.TryGetValue(alias, StringComparison.OrdinalIgnoreCase, out var aliasToken))
            {
                var result = ExtractValueFromToken(aliasToken);
                if (result != null) return result;
            }
        }

        // Try current culture
        var culture = CultureInfo.CurrentCulture.Name;
        if (valuesObj.TryGetValue(culture, StringComparison.OrdinalIgnoreCase, out var cultureToken))
        {
            var result = ExtractValueFromToken(cultureToken);
            if (result != null) return result;
        }

        // Fallback: return first value
        if (fallback)
        {
            var firstValueToken = valuesObj.Properties().Select(p => p.Value).FirstOrDefault();
            if (firstValueToken != null)
            {
                var result = ExtractValueFromToken(firstValueToken);
                if (result != null) return result;
            }
        }

        return string.Empty;
    }

    static string? ExtractValueFromToken(JToken token)
    {
        // Special handling if it's an object with a "markup" field
        if (token.Type == JTokenType.Object && token["markup"] != null)
            return token["markup"]!.ToString();

        return token.ToString();
    }

    internal static List<IPrice> GetPriceValuesConstructed(this string priceJson, decimal vat, bool vatIncludedInPrice, CurrencyModel fallbackCurrency = null)
    {
        List<IPrice> prices = new List<IPrice>();


        if (priceJson.IsJson())
        {
            JArray _prices = JArray.Parse(priceJson);

            foreach (JToken price in _prices)
            {
                CurrencyModel? currency = price[KeyExists(price, "Currency") ? "Currency" : "currency"].ToObject<CurrencyModel>(EkomJsonDotNet.serializer);

                prices.Add(new Price(price, currency, vat, vatIncludedInPrice));
            }
        }
        else
        {
            if (fallbackCurrency == null)
            {
                IStore? store = API.Store.Instance.GetStore();

                fallbackCurrency = store.Currency;
            }

            prices = new List<IPrice>
            {
                new Price(priceJson, fallbackCurrency, vat, vatIncludedInPrice)
            };
        }

        return prices;
    }

    internal static string Hash(this string input)
    {
        Murmur128 murmur = MurmurHash.Create128();
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = murmur.ComputeHash(inputBytes);

        // Convert to hexadecimal string
        StringBuilder builder = new StringBuilder();
        foreach (byte b in hashBytes)
        {
            builder.Append(b.ToString("x2"));
        }
        return builder.ToString();
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
        List<IPrice> prices = new List<IPrice>();

        if (priceJson.IsJson())
        {
            JArray _prices = JArray.Parse(priceJson);

            foreach (JToken price in _prices)
            {
                string? currencyValue = price[KeyExists(price, "Currency") ? "Currency" : "currency"].Value<string>();
                CurrencyModel? currency = storeCurrencies.FirstOrDefault(x => x.CurrencyValue == currencyValue) ?? storeCurrencies.FirstOrDefault();

                IDiscount? productDiscount = !string.IsNullOrEmpty(path)
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
        else
        {
            if (fallbackCurrency == null)
            {
                IStore? store = API.Store.Instance.GetStore();

                fallbackCurrency = store.Currency;
            }

            IDiscount? productDiscount = !string.IsNullOrEmpty(path)
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
        List<CurrencyValue> values = new List<CurrencyValue>();

        if (priceJson.IsJson())
        {
            JArray _values = JArray.Parse(priceJson);

            foreach (JToken value in _values)
            {
                if (KeyExists(value, "Currency"))
                {
                    string? currencyValue = value["Currency"].Value<string>();
                    decimal val = value["Price"] != null ? value["Price"].Value<decimal>() : (value["Value"] != null ? value["Value"].Value<decimal>() : 0);

                    values.Add(new CurrencyValue(val, currencyValue));
                }
                else
                {
                    string? currencyValue = value["currency"].Value<string>();
                    decimal val = value["price"] != null ? value["price"].Value<decimal>() : (value["value"] != null ? value["value"].Value<decimal>() : 0);

                    values.Add(new CurrencyValue(val, currencyValue));
                }
            }
        }
        else
        {
            if (decimal.TryParse(priceJson, out decimal value))
            {
                IStore? store = API.Store.Instance.GetStore();

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
        List<CurrencyPrice>? values = new List<CurrencyPrice>();

        try
        {
            values = JsonConvert.DeserializeObject<List<CurrencyPrice>>(priceJson);
        }
        catch
        {
            if (decimal.TryParse(priceJson, out decimal value))
            {
                IStore? store = API.Store.Instance.GetStore();

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
        List<Image> list = new List<Image>();

        if (!string.IsNullOrEmpty(nodeIds))
        {
            if (nodeIds.StartsWith("[") && nodeIds.IndexOf("mediakey", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                List<MediaCropImage>? imageList = JsonConvert.DeserializeObject<List<MediaCropImage>>(nodeIds);

                foreach (MediaCropImage image in imageList)
                {
                    UmbracoContent? node = Configuration.Resolver.GetService<INodeService>()?.MediaById(image.MediaKey);

                    if (node != null)
                    {
                        list.Add(new Image(node, storeAlias));
                    }
                }
            }
            else
            {
                string[] imageIds = nodeIds.Split(',');

                foreach (string imgId in imageIds)
                {

                    UmbracoContent? node = Configuration.Resolver.GetService<INodeService>()?.MediaById(imgId);

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
        string[] words = str.Split(new[] { "_", " " }, StringSplitOptions.RemoveEmptyEntries);
        string leadWord = Regex.Replace(words[0], @"([A-Z])([A-Z]+|[a-z0-9]+)($|[A-Z]\w*)",
            m =>
            {
                return m.Groups[1].Value.ToLower() + m.Groups[2].Value.ToLower() + m.Groups[3].Value;
            });
        string[] tailWords = words.Skip(1)
            .Select(word => char.ToUpper(word[0]) + word.Substring(1))
            .ToArray();
        return $"{leadWord}{string.Join(string.Empty, tailWords)}";
    }

    public static bool IsValidEmail(this string email)
    {
        string emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(email.Trim(), emailPattern);
    }
}

using Ekom.Models;
using Ekom.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
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
                return value == "1" || value.Equals("y", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }
    public static bool IsJson(this string input)
    {
        if (string.IsNullOrEmpty(input)) {
            return false;
        }

        var span = input.AsSpan().Trim();
        return span.Length > 1
            && ((span[0] == '{' && span[^1] == '}')
                || (span[0] == '[' && span[^1] == ']'));
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
                if (obj.TryGetValue("values", StringComparison.OrdinalIgnoreCase, out var valuesToken))
                {
                    if (valuesToken.Type == JTokenType.Null)
                    {
                        return string.Empty;
                    }

                    if (valuesToken is JObject valuesObj)
                    {
                        return GetValueFromValuesObject(valuesObj, alias, fallback);
                    }
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

        HttpContext? httpCtx = Configuration.Resolver?.GetService<IHttpContextAccessor>()?.HttpContext;

        var culture = httpCtx?.Request?.HttpContext?.Features.Get<IRequestCultureFeature>()?.RequestCulture.Culture ?? CultureInfo.CurrentCulture;


        if (valuesObj.TryGetValue(culture.Name, StringComparison.OrdinalIgnoreCase, out var cultureToken))
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
        if (priceJson.IsJson())
        {
            JArray _prices = JArray.Parse(priceJson);
            var prices = new List<IPrice>(_prices.Count);

            foreach (JToken price in _prices)
            {
                CurrencyModel? currency = GetPropertyIgnoreCase(price, "Currency")?.ToObject<CurrencyModel>(EkomJsonDotNet.Serializer);

                prices.Add(new Price(price, currency, vat, vatIncludedInPrice));
            }

            return prices;
        }

        if (fallbackCurrency == null)
        {
            IStore? store = API.Store.Instance.GetStore();

            fallbackCurrency = store.Currency;
        }

        return new List<IPrice>(1)
        {
            new Price(priceJson, fallbackCurrency, vat, vatIncludedInPrice)
        };
    }

    public static List<IPrice> GetPriceValues(
        this string priceJson,
        List<CurrencyModel> storeCurrencies,
        decimal vat,
        bool vatIncludedInPrice,
        CurrencyModel? fallbackCurrency = null,
        string? storeAlias = null,
        string? path = null,
        string[]? categories = null
        )
    {
        var discountService = Configuration.Resolver.GetService<ProductDiscountService>();

        if (priceJson.IsJson())
        {
            var _prices = JArray.Parse(priceJson);
            var prices = new List<IPrice>(_prices.Count);

            foreach (JToken price in _prices)
            {
                string? currencyValue = GetPropertyIgnoreCase(price, "Currency")?.Value<string>();
                CurrencyModel? currency = storeCurrencies.FirstOrDefault(x => x.CurrencyValue == currencyValue) ?? storeCurrencies.FirstOrDefault();
                string? priceValue = GetPropertyIgnoreCase(price, "Price")?.Value<string>();

                IDiscount? productDiscount = !string.IsNullOrEmpty(path) && discountService != null
                    ? discountService
                        .GetProductDiscount(
                            path,
                            storeAlias,
                            priceValue,
                            categories
                        )
                    : null;

                prices.Add(new Price(
                    priceValue,
                    currency,
                    vat,
                    vatIncludedInPrice,
                    productDiscount != null
                        ? new OrderedDiscount(productDiscount)
                        : null)
                );
            }

            return prices;
        }

        if (fallbackCurrency == null)
        {
            IStore? store = API.Store.Instance.GetStore();

            fallbackCurrency = store.Currency;
        }

        IDiscount? singleProductDiscount = !string.IsNullOrEmpty(path) && discountService != null
            ? discountService
                .GetProductDiscount(
                    path,
                    storeAlias,
                    priceJson,
                    categories
                )
            : null;

        return new List<IPrice>(1)
        {
            new Price(
                priceJson,
                fallbackCurrency,
                vat,
                vatIncludedInPrice,
                singleProductDiscount != null
                    ? new OrderedDiscount(singleProductDiscount)
                    : null)
        };
    }
    public static List<CurrencyValue> GetCurrencyValues(this string priceJson, string? storeAlias = null)
    {
        if (!LooksLikeJson(priceJson))
        {
            if (decimal.TryParse(priceJson, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
            {
                var store = string.IsNullOrEmpty(storeAlias)
                    ? API.Store.Instance.GetStore()
                    : API.Store.Instance.GetStore(storeAlias);

                return new List<CurrencyValue>(1)
                {
                    new CurrencyValue(v, store?.Currency.CurrencyValue
                                         ?? API.Store.Instance.GetAllStores().FirstOrDefault()?.Currency.CurrencyValue
                                         ?? string.Empty)
                };
            }
            return new List<CurrencyValue>(0);
        }

        var token = JToken.Parse(priceJson);

        if (token is JArray arr)
        {
            var list = new List<CurrencyValue>(arr.Count);
            foreach (var t in arr)
            {
                if (TryReadCurrencyValue(t, out var cv))
                    list.Add(cv);
            }
            return list;
        }
        else // single object
        {
            return TryReadCurrencyValue(token, out var single)
                ? new List<CurrencyValue>(1) { single }
                : new List<CurrencyValue>(0);
        }
    }

    private static bool LooksLikeJson(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return false;

        ReadOnlySpan<char> span = s.AsSpan().TrimStart();
        if (span.IsEmpty)
            return false;

        char c = span[0];
        return c == '{' || c == '[' || c == '"';
    }

    internal static bool TryGetOriginalPriceValue(this string? priceJson, string? storeAlias, out decimal value)
    {
        value = 0m;

        if (string.IsNullOrWhiteSpace(priceJson))
            return false;

        if (decimal.TryParse(priceJson, NumberStyles.Number, CultureInfo.InvariantCulture, out var org))
        {
            value = org;
            return true;
        }

        var s = priceJson.Trim();
        if (s.Length == 0)
            return false;

        var first = s[0];
        if (first != '[' && first != '{')
            return false;

        try
        {
            if (first == '[')
            {
                var list = JsonConvert.DeserializeObject<List<CurrencyPrice>>(s);
                var val = list?.Count > 0 ? list[0].Price : (decimal?)null;
                if (val.HasValue)
                {
                    value = val.Value;
                    return true;
                }

                return false;
            }

            var root = JToken.Parse(s);
            if (root is not JObject obj)
                return false;

            JToken? storeToken = null;
            if (!string.IsNullOrWhiteSpace(storeAlias))
            {
                obj.TryGetValue(storeAlias, StringComparison.OrdinalIgnoreCase, out storeToken);
            }

            if (storeToken == null)
                return TryReadCurrencyPrice(obj, out value);

            return TryReadCurrencyPriceStoreToken(storeToken, out value);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadCurrencyPriceStoreToken(JToken token, out decimal price)
    {
        price = 0m;

        switch (token.Type)
        {
            case JTokenType.Array:
                var firstItem = token.First;
                return firstItem != null && TryReadCurrencyPrice(firstItem, out price);
            case JTokenType.Object:
                return TryReadCurrencyPrice(token, out price);
            case JTokenType.Integer:
            case JTokenType.Float:
            case JTokenType.String:
                return TryGetDecimalInvariant(token, out price);
            default:
                return false;
        }
    }

    private static bool TryReadCurrencyPrice(JToken token, out decimal price)
    {
        price = 0m;

        var priceToken = token["Price"] ?? token["price"] ?? token["Value"] ?? token["value"];
        if (priceToken == null)
            return false;

        return TryGetDecimalInvariant(priceToken, out price);
    }

    private static bool TryReadCurrencyValue(JToken t, out CurrencyValue cv)
    {
        cv = default;

        // Currency: support both "Currency" and "currency"
        var currencyTok = t["Currency"] ?? t["currency"];
        var currency = currencyTok?.Value<string>();
        if (string.IsNullOrWhiteSpace(currency))
            return false; // required

        // Price/Value: support both casings; accept number or string numbers
        decimal price = 0m;
        var pTok = t["Price"] ?? t["price"] ?? t["Value"] ?? t["value"];
        if (pTok != null && TryGetDecimalInvariant(pTok, out var p))
            price = p; // else keep 0

        cv = new CurrencyValue(price, currency);
        return true;
    }

    private static bool TryGetDecimalInvariant(JToken tok, out decimal value)
    {
        value = 0m;
        switch (tok.Type)
        {
            case JTokenType.Integer:
            case JTokenType.Float:
                // direct conversion is fine
                value = tok.Value<decimal>();
                return true;
            case JTokenType.String:
                var s = tok.Value<string>();
                return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
            default:
                return false;
        }
    }

    internal static bool KeyExists(JToken token, string key)
    {
        JObject obj = token as JObject;
        return obj?.ContainsKey(key) ?? false;
    }

    private static JToken? GetPropertyIgnoreCase(JToken token, string key)
    {
        return token is JObject obj && obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var value)
            ? value
            : null;
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
            using var scope = Configuration.Resolver.GetRequiredService<IServiceScopeFactory>().CreateScope();
            var nodeService = scope.ServiceProvider.GetService<INodeService>();
            if (nodeService == null)
                return Enumerable.Empty<Image>();

            if (nodeIds.StartsWith("[") && nodeIds.IndexOf("mediakey", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                List<MediaCropImage>? imageList = JsonConvert.DeserializeObject<List<MediaCropImage>>(nodeIds);
                if (imageList == null)
                    return Enumerable.Empty<Image>();

                foreach (MediaCropImage image in imageList)
                {
                    UmbracoContent? node = nodeService.MediaById(image.MediaKey);

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

                    UmbracoContent? node = nodeService.MediaById(imgId);

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

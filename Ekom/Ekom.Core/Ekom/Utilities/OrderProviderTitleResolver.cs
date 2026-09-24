using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ekom.Utilities;

internal static class OrderProviderTitleResolver
{
    internal static string Resolve(IReadOnlyDictionary<string, string> properties, string storeAlias, string? culture)
    {
        var nodeName = properties.TryGetValue("nodeName", out var name) ? name : string.Empty;

        if (!properties.TryGetValue("title", out var rawTitle) || string.IsNullOrWhiteSpace(rawTitle))
        {
            return nodeName;
        }

        if (!rawTitle.IsJson())
        {
            return System.Web.HttpUtility.HtmlDecode(rawTitle);
        }

        try
        {
            if (JToken.Parse(rawTitle) is JObject editor
                && editor.TryGetValue("values", StringComparison.OrdinalIgnoreCase, out var values)
                && values is JObject titles)
            {
                return GetTitle(titles, storeAlias)
                    ?? GetTitle(titles, culture)
                    ?? nodeName;
            }
        }
        catch (JsonException)
        {
            // Invalid editor values cannot provide a localized title.
        }

        return nodeName;
    }

    private static string? GetTitle(JObject titles, string? key)
    {
        if (string.IsNullOrWhiteSpace(key)
            || !titles.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var token)
            || token == null
            || token.Type == JTokenType.Null)
        {
            return null;
        }

        var value = token.Type == JTokenType.Object && token["markup"] is JToken markup
            ? markup.ToString()
            : token.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : System.Web.HttpUtility.HtmlDecode(value);
    }
}

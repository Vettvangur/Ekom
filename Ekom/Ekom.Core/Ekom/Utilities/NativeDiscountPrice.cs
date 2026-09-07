using Ekom.Models;
using System.Globalization;
using System.Text.Json;

namespace Ekom.Utilities;

internal static class NativeDiscountPrice
{
    internal static string Raw(INodeEntity? node)
        => node?.Properties.TryGetValue("ekmDiscountPrice", out var value) == true ? value : string.Empty;

    internal static decimal? Read(INodeEntity node, IStore store, CurrencyModel currency)
        => Read(Raw(node), store.Alias, currency.CurrencyValue,
            (store.Currencies.FirstOrDefault() ?? store.Currency).CurrencyValue);

    // Scalars belong to the default currency. Currency arrays never fall back to their first entry.
    internal static decimal? Read(string? raw, string? storeAlias, string currency, string defaultCurrency)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var text = raw.Trim();
        if (text[0] != '[' && text[0] != '{' && text[0] != '"')
        {
            return string.Equals(currency, defaultCurrency, StringComparison.OrdinalIgnoreCase) &&
                decimal.TryParse(text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var scalar)
                ? scalar : null;
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            var value = doc.RootElement;
            if (value.ValueKind == JsonValueKind.Object)
            {
                if (TryProperty(value, "values", out var values)) value = values;
                if (!TryProperty(value, storeAlias, out var storeValue)) return null;
                value = storeValue;
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray())
                {
                    if (TryProperty(item, "Currency", out var code) &&
                        code.ValueKind == JsonValueKind.String &&
                        string.Equals(code.GetString(), currency, StringComparison.OrdinalIgnoreCase) &&
                        TryProperty(item, "Price", out var amount))
                    {
                        return amount.ValueKind is JsonValueKind.String or JsonValueKind.Number &&
                            decimal.TryParse(amount.ToString().Replace(',', '.'), NumberStyles.Any,
                                CultureInfo.InvariantCulture, out var price)
                            ? price : null;
                    }
                }
                return null;
            }

            return value.ValueKind is JsonValueKind.String or JsonValueKind.Number
                ? Read(value.ToString(), storeAlias, currency, defaultCurrency)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryProperty(JsonElement element, string? name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }
}

using Ekom.Models;
using Newtonsoft.Json;

namespace Ekom.Utilities;

public static class PriceHelper
{
    /// <summary>Read stored node prices, without resolving table overrides or inherited variant prices.</summary>
    public static IReadOnlyList<PriceMutation> ReadNodePrices(Guid nodeKey, string? nodeValue,
        IReadOnlyList<IStore> stores, string propertyAlias = "price")
    {
        ArgumentNullException.ThrowIfNull(stores);
        if (nodeKey == Guid.Empty) throw new ArgumentException("A node key is required.", nameof(nodeKey));
        var result = new List<PriceMutation>();
        if (string.IsNullOrWhiteSpace(nodeValue)) return result;
        var text = nodeValue.Trim();
        if (text[0] is '[' or '{' or '"')
        {
            // Do not silently mark malformed JSON as successfully migrated.
            using var document = System.Text.Json.JsonDocument.Parse(text);
        }
        foreach (var store in stores)
        foreach (var currency in store.Currencies)
        {
            var price = NativeDiscountPrice.Read(nodeValue, store.Alias, currency.CurrencyValue,
                store.Currencies.FirstOrDefault()?.CurrencyValue ?? string.Empty);
            if (price.HasValue)
                result.Add(new PriceMutation(nodeKey, store.Alias, currency.CurrencyValue, price.Value,
                    propertyAlias, CheckExpected: true));
        }
        return result;
    }

    /// <summary>Merge table overrides into node JSON, preserving legacy fallback values.</summary>
    public static string Export(string? nodeValue, IReadOnlyList<PriceData> rows, IReadOnlyList<IStore> stores)
    {
        var value = nodeValue ?? string.Empty;
        if (rows.Count == 0) return value;
        if (!value.TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            var converted = new CurrencyPriceRoot();
            foreach (var store in stores)
            {
                var prices = new List<CurrencyPrice>();
                foreach (var currency in store.Currencies)
                {
                    var price = NativeDiscountPrice.Read(value, store.Alias, currency.CurrencyValue,
                        store.Currencies.FirstOrDefault()?.CurrencyValue ?? string.Empty);
                    if (price.HasValue) prices.Add(new CurrencyPrice(price.Value, currency.CurrencyValue));
                }
                converted[store.Alias] = prices;
            }
            value = JsonConvert.SerializeObject(converted);
        }
        foreach (var row in rows)
        {
            var store = stores.FirstOrDefault(x => x.Alias.Equals(row.StoreAlias, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Unknown store {row.StoreAlias}; fix the mapping before exporting prices.");
            var currency = store.Currencies.FirstOrDefault(x => x.CurrencyValue.Equals(row.Currency, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Unknown currency {row.Currency}; fix the mapping before exporting prices.");
            value = SetPrice(value, row.Price, currency.CurrencyValue, store.Alias);
        }
        return value;
    }

    public static string SetPrice(string? currentValue, decimal price, string currency, string storeAlias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentException.ThrowIfNullOrWhiteSpace(storeAlias);

        var prices = string.IsNullOrWhiteSpace(currentValue)
            ? new CurrencyPriceRoot()
            : JsonConvert.DeserializeObject<CurrencyPriceRoot>(currentValue) ?? new CurrencyPriceRoot();

        var storePrices = prices
            .Where(x => x.Key.Equals(storeAlias, StringComparison.OrdinalIgnoreCase))
            .SelectMany(x => x.Value)
            .GroupBy(x => x.Currency, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Last())
            .ToList();

        foreach (var key in prices.Keys.Where(x => x.Equals(storeAlias, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            prices.Remove(key);
        }

        var currencyPrice = storePrices.FirstOrDefault(x => x.Currency.Equals(currency, StringComparison.OrdinalIgnoreCase));
        if (currencyPrice is null)
        {
            storePrices.Add(new CurrencyPrice(price, currency));
        }
        else
        {
            currencyPrice.Price = price;
        }

        prices.Add(storeAlias, storePrices);

        return JsonConvert.SerializeObject(prices);
    }
}

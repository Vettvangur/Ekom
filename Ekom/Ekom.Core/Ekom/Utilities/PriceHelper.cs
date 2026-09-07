using Ekom.Models;
using Newtonsoft.Json;

namespace Ekom.Utilities;

public static class PriceHelper
{
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

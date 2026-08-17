using Ekom.Models;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Ekom.Utilities;

public static class PriceBuilder
{
    public static List<IPrice> BuildPricesSync(
        string priceJson,
        List<CurrencyModel> storeCurrencies,
        decimal vat,
        bool vatIncludedInPrice,
        CurrencyModel fallbackCurrency,
        string? storeAlias,
        string? path,
        string[]? categories)
    {

        var prices = new List<IPrice>();

        if (string.IsNullOrWhiteSpace(priceJson))
        {
            prices.Add(new Price(
                "0",
                fallbackCurrency,
                vat,
                vatIncludedInPrice,
                null
            ));
            return prices;
        }


        var discountSvc = Configuration.Resolver.GetService<ProductDiscountService>();

        var isArray = priceJson.AsSpan().TrimStart().StartsWith("[");
        if (!isArray)
        {
            IDiscount? disc = (!string.IsNullOrEmpty(path) && discountSvc != null)
                ? discountSvc.GetProductDiscount(path!, storeAlias, priceJson, categories)
                : null;

            prices.Add(new Price(
                priceJson,
                fallbackCurrency,
                vat,
                vatIncludedInPrice,
                disc is null ? null : new OrderedDiscount(disc)));

            return prices;
        }

        using var doc = JsonDocument.Parse(priceJson);
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            // ---- PRICE ----
            JsonElement priceElement =
                el.TryGetProperty("Price", out var p1) ? p1 :
                el.TryGetProperty("price", out var p2) ? p2 :
                default;

            if (priceElement.ValueKind == JsonValueKind.Undefined)
                continue;

            string? priceStr = GetStringOrNumber(priceElement);
            if (string.IsNullOrEmpty(priceStr))
                continue;

            // ---- CURRENCY ----
            JsonElement currencyElement =
                el.TryGetProperty("Currency", out var c1) ? c1 :
                el.TryGetProperty("currency", out var c2) ? c2 :
                default;

            string? currStr = GetStringValue(currencyElement);

            var currency = (!string.IsNullOrEmpty(currStr)
                    ? storeCurrencies.FirstOrDefault(x => x.CurrencyValue == currStr)
                    : null)
                ?? storeCurrencies.FirstOrDefault()
                ?? fallbackCurrency;

            // ---- DISCOUNT ----
            IDiscount? disc = (!string.IsNullOrEmpty(path) && discountSvc != null)
                ? discountSvc.GetProductDiscount(path!, storeAlias, priceStr, categories)
                : null;

            prices.Add(new Price(
                priceStr,
                currency,
                vat,
                vatIncludedInPrice,
                disc is null ? null : new OrderedDiscount(disc)));
        }

        foreach (var storeCurrency in storeCurrencies)
        {
            if (prices.Any(x => string.Equals(
                x.Currency.CurrencyValue,
                storeCurrency.CurrencyValue,
                StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            prices.Add(new Price(
                "0",
                storeCurrency,
                vat,
                vatIncludedInPrice,
                null));
        }

        return prices;
    }

    private static string? GetStringOrNumber(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(), // "14990" from 14990
            _ => null
        };
    }

    private static string? GetStringValue(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
    }
}

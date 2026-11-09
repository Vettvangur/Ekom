using Ekom.Exceptions;
using Ekom.Models;

namespace Ekom.Utilities;

/// <summary>Build a delta from the editor baseline, with database compare-and-set expectations.</summary>
public static class PriceEdits
{
    public static IReadOnlyList<PriceMutation> Create(Guid key, CurrencyPriceRoot values,
        CurrencyPriceRoot? original, IReadOnlyList<PriceData>? overrides, bool isNew)
    {
        if (!isNew && (original == null || overrides == null)) throw new PriceConflictException();
        var result = new List<PriceMutation>();
        foreach (var (store, prices) in values)
        foreach (var price in prices)
        {
            var before = original?.FirstOrDefault(x => x.Key.Equals(store, StringComparison.OrdinalIgnoreCase)).Value?
                .FirstOrDefault(x => x.Currency.Equals(price.Currency, StringComparison.OrdinalIgnoreCase))?.Price ?? 0;
            if (!isNew && before == (price.Price ?? 0)) continue;
            var row = overrides?.FirstOrDefault(x => x.StoreAlias.Equals(store, StringComparison.OrdinalIgnoreCase)
                && x.Currency.Equals(price.Currency, StringComparison.OrdinalIgnoreCase) && x.PropertyAlias.Equals("price", StringComparison.OrdinalIgnoreCase));
            result.Add(new PriceMutation(key, store, price.Currency, price.Price ?? 0, CheckExpected: true, ExpectedPrice: row?.Price));
        }
        return result;
    }
}

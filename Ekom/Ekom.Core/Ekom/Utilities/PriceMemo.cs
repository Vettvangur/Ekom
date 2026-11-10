using Ekom.Models;

namespace Ekom.Utilities;

/// <summary>
/// Everything a product or variant price list is calculated from. Two equal values
/// always produce the same versioned cache key.
/// </summary>
internal readonly record struct PriceInputs(
    string GlobalGeneration,
    string ItemGeneration,
    string Currency,
    string Path,
    string Price,
    string Sale,
    bool DisableDiscounts,
    string ParentGeneration = "",
    string ParentPrice = "",
    string ParentSale = "",
    bool HasParent = false);

/// <summary>
/// The last price list an entity calculated together with its inputs, so repeated reads
/// (sorting, filtering, serialization) skip hashing the inputs and the shared cache lookup.
/// Immutable; entities swap the whole instance.
/// </summary>
internal sealed class PriceMemo
{
    // Same lifetime as the shared versioned cache entry it shortcuts.
    internal static readonly TimeSpan Lifetime = TimeSpan.FromHours(48);

    private readonly PriceInputs _inputs;
    private readonly int[] _categoryIds;
    private readonly long _created = Environment.TickCount64;

    internal PriceMemo(PriceInputs inputs, IReadOnlyList<ICategory>? categories, List<IPrice> prices)
    {
        _inputs = inputs;
        Prices = prices;
        _categoryIds = categories == null || categories.Count == 0 ? Array.Empty<int>() : new int[categories.Count];
        for (var i = 0; i < _categoryIds.Length; i++)
            _categoryIds[i] = categories![i].Id;
    }

    internal List<IPrice> Prices { get; }

    internal bool Matches(in PriceInputs inputs, IReadOnlyList<ICategory>? categories)
    {
        if (Environment.TickCount64 - _created > (long)Lifetime.TotalMilliseconds || _inputs != inputs)
            return false;

        var count = categories?.Count ?? 0;
        if (count != _categoryIds.Length)
            return false;

        for (var i = 0; i < count; i++)
        {
            if (categories![i].Id != _categoryIds[i])
                return false;
        }

        return true;
    }
}

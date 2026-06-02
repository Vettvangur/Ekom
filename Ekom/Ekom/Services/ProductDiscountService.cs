using Ekom.Cache;
using Ekom.Events;
using Ekom.Models;
using System.Globalization;
namespace Ekom.Services;

class ProductDiscountService
{
    readonly IPerStoreCache<IProductDiscount> _productDiscountCache;
    private readonly DiscountEvents _discountEvents;
    internal ProductDiscountService(IPerStoreCache<IProductDiscount> productDiscountCache, DiscountEvents discountEvents)
    {
        _productDiscountCache = productDiscountCache;
        _discountEvents = discountEvents;
    }

    public virtual IProductDiscount? GetProductDiscount(
        string path,
        string? storeAlias,
        string inputPrice,
        string[]? categories = null)
        => GetProductDiscountCoreAsync(
            path,
            storeAlias,
            inputPrice,
            categories,
            CancellationToken.None).GetAwaiter().GetResult();

    public virtual Task<IProductDiscount?> GetProductDiscountAsync(
        string path,
        string? storeAlias,
        string inputPrice,
        string[]? categories = null,
        CancellationToken ct = default)
        => GetProductDiscountCoreAsync(
            path,
            storeAlias,
            inputPrice,
            categories,
            ct);

    private async Task<IProductDiscount?> GetProductDiscountCoreAsync(
        string path,
        string? storeAlias,
        string inputPrice,
        string[]? categories = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(storeAlias))
            return null;

        inputPrice = string.IsNullOrEmpty(inputPrice)
            ? "0"
            : inputPrice.Replace(',', '.');

        if (!decimal.TryParse(inputPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
            return null;

        var evalArgs = new DiscountEvents.ProductDiscountEvaluationEventArgs
        {
            Path = path,
            StoreAlias = storeAlias,
            Price = price,
            Categories = categories
        };

        await _discountEvents.RaiseBeforeEvaluateDiscountsAsync(this, evalArgs, ct).ConfigureAwait(false);

        // use possibly modified values from event args
        path = evalArgs.Path;
        storeAlias = evalArgs.StoreAlias;
        price = evalArgs.Price;
        categories = evalArgs.Categories;

        if (!_productDiscountCache.Cache.TryGetValue(storeAlias, out var storeDiscounts)
            || storeDiscounts.Count == 0)
        {
            return null;
        }

        var pathItems = string.IsNullOrWhiteSpace(path)
            ? Array.Empty<string>()
            : path.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var pathSet = pathItems.Length > 0
            ? new HashSet<string>(pathItems)
            : null;

        var categorySet = (categories != null && categories.Length > 0)
            ? new HashSet<string>(categories)
            : null;

        static bool HasAny(HashSet<string>? targets, IEnumerable<string>? source)
        {
            if (targets == null || source == null)
                return false;

            foreach (var item in source)
            {
                if (targets.Contains(item))
                    return true;
            }

            return false;
        }

        var applicableDiscounts = new List<IProductDiscount>();

        foreach (var kvp in storeDiscounts)
        {
            if (kvp.Value.Disabled)
                continue;

            if (kvp.Value is not Discount disc)
                continue;

            var inDiscount =
                HasAny(pathSet, disc.DiscountItems) ||
                HasAny(categorySet, disc.DiscountItems);

            var inExclusion =
                disc.ExcludeDiscountItems != null &&
                (HasAny(pathSet, disc.ExcludeDiscountItems) ||
                 HasAny(categorySet, disc.ExcludeDiscountItems));

            if (inDiscount && !inExclusion)
                applicableDiscounts.Add(kvp.Value);
        }

        var applicableArgs = new DiscountEvents.ProductDiscountApplicableEventArgs(
            path,
            storeAlias,
            price,
            categories,
            applicableDiscounts);

        await _discountEvents.RaiseAfterApplicableDiscountsAsync(this, applicableArgs, ct).ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();

        if (applicableArgs.ApplicableDiscounts.Count == 0)
            return null;

        Guid bestFixedKey = Guid.Empty;
        Guid bestPercentageKey = Guid.Empty;
        decimal bestPercentageValue = 0;
        decimal bestFixedValue = 0;

        foreach (var usableDiscount in applicableArgs.ApplicableDiscounts)
        {
            bool inRange =
                usableDiscount.StartOfRange <= price &&
                (usableDiscount.EndOfRange == 0 || price <= usableDiscount.EndOfRange);

            if (!inRange)
                continue;

            if (usableDiscount.Type == DiscountType.Fixed)
            {
                if (usableDiscount.Amount > bestFixedValue)
                {
                    bestFixedValue = usableDiscount.Amount;
                    bestFixedKey = usableDiscount.Key;
                }
            }
            else if (usableDiscount.Type == DiscountType.Percentage)
            {
                if (usableDiscount.Amount > bestPercentageValue)
                {
                    bestPercentageValue = usableDiscount.Amount;
                    bestPercentageKey = usableDiscount.Key;
                }
            }
        }

        if (bestFixedKey == Guid.Empty && bestPercentageKey == Guid.Empty)
            return null;

        if (bestFixedKey == Guid.Empty)
            return applicableArgs.ApplicableDiscounts.SingleOrDefault(x => x.Key == bestPercentageKey);

        if (bestPercentageKey == Guid.Empty)
            return applicableArgs.ApplicableDiscounts.SingleOrDefault(x => x.Key == bestFixedKey);

        var fixedAsPercent = Math.Abs(bestFixedValue / price * 100m);

        return fixedAsPercent > bestPercentageValue
            ? applicableArgs.ApplicableDiscounts.SingleOrDefault(x => x.Key == bestFixedKey)
            : applicableArgs.ApplicableDiscounts.SingleOrDefault(x => x.Key == bestPercentageKey);
    }

}

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

        return SelectBestDiscount(applicableArgs.ApplicableDiscounts, price);
    }

    internal static IProductDiscount? SelectBestDiscount(
        IReadOnlyCollection<IProductDiscount> applicableDiscounts,
        decimal price)
    {
        IProductDiscount? bestFixed = null;
        IProductDiscount? bestPercentage = null;

        foreach (var usableDiscount in applicableDiscounts)
        {
            bool inRange =
                usableDiscount.StartOfRange <= price &&
                (usableDiscount.EndOfRange == 0 || price <= usableDiscount.EndOfRange);

            if (!inRange)
                continue;

            if (DiscountValueCalculator.CalculateDiscountAmount(price, usableDiscount) <= 0)
                continue;

            if (usableDiscount.Type == DiscountType.Fixed)
            {
                if (bestFixed == null || usableDiscount.Amount > bestFixed.Amount)
                {
                    bestFixed = usableDiscount;
                }
            }
            else if (usableDiscount.Type == DiscountType.Percentage)
            {
                if (bestPercentage == null || usableDiscount.Amount > bestPercentage.Amount)
                {
                    bestPercentage = usableDiscount;
                }
            }
        }

        if (bestFixed == null)
            return bestPercentage;

        return bestPercentage == null || DiscountValueCalculator.IsBetterDiscount(price, bestFixed, bestPercentage)
            ? bestFixed
            : bestPercentage;
    }

}

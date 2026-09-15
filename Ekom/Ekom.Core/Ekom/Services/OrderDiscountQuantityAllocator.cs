using Ekom.Models;
using Ekom.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Ekom.Services;

internal static class OrderDiscountQuantityAllocator
{
    internal static IReadOnlyDictionary<Guid, decimal> Allocate(
        IOrderInfo orderInfo,
        IDiscount discount,
        INodeService? nodeService = null,
        IReadOnlyDictionary<Guid, decimal>? effectiveUnitPrices = null)
    {
        ArgumentNullException.ThrowIfNull(orderInfo);
        ArgumentNullException.ThrowIfNull(discount);

        var mode = discount.QuantityDiscountMode;
        var discountItems = discount.DiscountItems ?? [];
        var excludeDiscountItems = discount.ExcludeDiscountItems ?? [];
        var globalDiscount = discount.GlobalDiscount;
        var qualifyingItems = discount.QualifyingItems ?? [];
        var requiredQuantity = discount.RequiredQuantity;
        var rewardQuantity = discount.RewardQuantity;

        if (!IsValid(mode, requiredQuantity, rewardQuantity, qualifyingItems, discountItems))
        {
            return new Dictionary<Guid, decimal>();
        }

        var orderLines = orderInfo.OrderLines;
        using var nodeServiceScope = orderLines.Any(line =>
            !string.IsNullOrWhiteSpace(line.Product.Properties.GetValue("categories")))
                ? ResolveNodeService(ref nodeService)
                : null;
        var categoryPaths = nodeService == null
            ? null
            : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var lines = orderLines
            .Select(line => new LineTargets(
                line,
                DiscountApplicability.GetOrderLineDiscountTargetItems(line, nodeService, categoryPaths)))
            .ToList();

        if (mode == OrderDiscountQuantityMode.None)
        {
            return lines
                .Where(item => DiscountApplicability.MatchesLineTargets(
                    item.Targets,
                    discountItems,
                    excludeDiscountItems,
                    globalDiscount))
                .ToDictionary(item => item.Line.Key, item => item.Line.Quantity);
        }

        var qualifyingQuantity = lines
            .Where(item => DiscountApplicability.MatchesLineTargets(
                item.Targets,
                qualifyingItems,
                Array.Empty<string>()))
            .Sum(item => Math.Floor(Math.Max(0, item.Line.Quantity)));

        if (qualifyingQuantity < requiredQuantity)
        {
            return new Dictionary<Guid, decimal>();
        }

        var rewardLimit = mode == OrderDiscountQuantityMode.Threshold
            ? decimal.MaxValue
            : Math.Floor(qualifyingQuantity / requiredQuantity) * rewardQuantity;
        var remaining = rewardLimit;
        var result = new Dictionary<Guid, decimal>();

        foreach (var item in lines
            .Where(line => DiscountApplicability.MatchesLineTargets(
                line.Targets,
                discountItems,
                excludeDiscountItems,
                globalDiscount))
            .Select(line => new RewardCandidate(
                line.Line,
                Math.Floor(Math.Max(0, line.Line.Quantity)),
                effectiveUnitPrices?.GetValueOrDefault(line.Line.Key)
                    ?? GetEffectiveUnitPrice(line.Line, discount)))
            .Where(item => item.WholeQuantity > 0)
            .OrderBy(item => item.UnitPrice)
            .ThenBy(item => item.Line.Key))
        {
            var allocated = Math.Min(item.WholeQuantity, remaining);
            if (allocated <= 0)
            {
                break;
            }

            if (!IsBetterThanExistingProductDiscount(
                item.Line,
                discount,
                orderInfo.StoreInfo.VatIncludedInPrice,
                allocated))
            {
                continue;
            }

            result[item.Line.Key] = allocated;
            remaining -= allocated;
        }

        return result;
    }

    internal static bool IsValid(IDiscount discount)
    {
        ArgumentNullException.ThrowIfNull(discount);

        return IsValid(
            discount.QuantityDiscountMode,
            discount.RequiredQuantity,
            discount.RewardQuantity,
            discount.QualifyingItems,
            discount.DiscountItems);
    }

    private static bool IsValid(
        OrderDiscountQuantityMode mode,
        int requiredQuantity,
        int rewardQuantity,
        IReadOnlyCollection<string> qualifyingItems,
        IReadOnlyCollection<string> discountItems)
    {
        if (mode == OrderDiscountQuantityMode.None)
        {
            return true;
        }

        return requiredQuantity > 0
            && qualifyingItems.Count > 0
            && discountItems.Count > 0
            && (mode != OrderDiscountQuantityMode.Repeating || rewardQuantity > 0);
    }

    private static decimal GetEffectiveUnitPrice(IOrderLine line, IDiscount discount)
    {
        var price = line.Variant?.Price ?? line.Product.Price;
        return discount.Stackable ? price.Value : price.OriginalValue;
    }

    private static bool IsBetterThanExistingProductDiscount(
        IOrderLine line,
        IDiscount discount,
        bool vatIncludedInPrice,
        decimal discountedQuantity)
    {
        var price = line.Variant?.Price ?? line.Product.Price;
        return discount.Stackable
            || !price.HasDiscount
            || DiscountValueCalculator.IsBetterLineDiscount(
                price,
                discount,
                price.Discount,
                line.Vat,
                vatIncludedInPrice,
                line.Quantity,
                discountedQuantity);
    }

    private static IServiceScope? ResolveNodeService(ref INodeService? nodeService)
    {
        if (nodeService != null)
        {
            return null;
        }

        var scopeFactory = Configuration.Resolver.GetService<IServiceScopeFactory>();
        if (scopeFactory == null)
        {
            return null;
        }

        var scope = scopeFactory.CreateScope();
        nodeService = scope.ServiceProvider.GetRequiredService<INodeService>();
        return scope;
    }

    private sealed record LineTargets(IOrderLine Line, HashSet<string> Targets);

    private sealed record RewardCandidate(IOrderLine Line, decimal WholeQuantity, decimal UnitPrice);
}

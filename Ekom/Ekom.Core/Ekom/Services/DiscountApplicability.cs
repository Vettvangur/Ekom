using Ekom.Models;
using Ekom.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Ekom.Services;

internal static class DiscountApplicability
{
    public static bool AreOrderConstraintsMet(IOrderInfo orderInfo, IDiscount discount)
    {
        if (discount is IProductDiscount)
        {
            return false;
        }

        return AreConstraintsMet(orderInfo, discount);
    }

    /// <summary>
    /// Same as <see cref="AreOrderConstraintsMet(IOrderInfo, IDiscount)"/> but with a caller supplied
    /// order line total, for cases where lines were priced under differing ambient pricing contexts
    /// and <c>orderInfo.OrderLineTotal</c> would re-price them without that context.
    /// </summary>
    public static bool AreOrderConstraintsMet(IOrderInfo orderInfo, IDiscount discount, decimal orderLineTotal)
    {
        if (discount is IProductDiscount)
        {
            return false;
        }

        return discount.Constraints == null
            || discount.Constraints.IsValid(orderInfo.StoreInfo.Culture, orderLineTotal);
    }

    public static bool IsDiscountApplicable(
        IOrderInfo orderInfo,
        IOrderLine orderLine,
        IDiscount discount,
        INodeService? nodeService = null)
        => IsDiscountApplicable(
            orderInfo,
            orderLine,
            discount,
            orderInfo.OrderLineTotal.Value,
            nodeService);

    internal static bool IsDiscountApplicable(
        IOrderInfo orderInfo,
        IOrderLine orderLine,
        IDiscount discount,
        decimal orderLineTotal,
        INodeService? nodeService = null)
    {
        if (discount.Constraints != null
            && !discount.Constraints.IsValid(orderInfo.StoreInfo.Culture, orderLineTotal))
        {
            return false;
        }

        if (!discount.Stackable && orderLine.Product.ProductDiscount != null)
        {
            return false;
        }

        return MatchesLineTargets(orderLine, discount, nodeService);
    }

    private static bool AreConstraintsMet(IOrderInfo orderInfo, IDiscount discount)
    {
        return discount.Constraints == null
            || discount.Constraints.IsValid(orderInfo.StoreInfo.Culture, orderInfo.OrderLineTotal.Value);
    }

    public static bool MatchesLineTargets(
        IOrderLine orderLine,
        IDiscount discount,
        INodeService? nodeService = null)
    {
        var includeItems = discount.DiscountItems ?? [];
        var excludeItems = discount.ExcludeDiscountItems ?? [];
        return MatchesLineTargets(
            orderLine,
            includeItems,
            excludeItems,
            nodeService,
            discount.GlobalDiscount);
    }

    internal static bool MatchesLineTargets(
        IOrderLine orderLine,
        IReadOnlyCollection<string> includeItems,
        IReadOnlyCollection<string> excludeItems,
        INodeService? nodeService = null,
        bool globalDiscount = false)
    {
        var targetItems = GetOrderLineDiscountTargetItems(orderLine, nodeService);
        return MatchesLineTargets(targetItems, includeItems, excludeItems, globalDiscount);
    }

    internal static bool MatchesLineTargets(
        HashSet<string> targetItems,
        IReadOnlyCollection<string> includeItems,
        IReadOnlyCollection<string> excludeItems,
        bool globalDiscount = false)
    {
        var matchesInclude = globalDiscount
            || (includeItems.Count > 0 && targetItems.Overlaps(includeItems));

        if (!matchesInclude)
        {
            return false;
        }

        if (excludeItems.Count > 0 && targetItems.Overlaps(excludeItems))
        {
            return false;
        }

        return true;
    }

    internal static HashSet<string> GetOrderLineDiscountTargetItems(
        IOrderLine orderLine,
        INodeService? nodeService,
        IDictionary<string, string?>? categoryPaths = null)
    {
        var targetItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddSplitItems(targetItems, orderLine.Product.Path);

        var categories = orderLine.Product.Properties.GetValue("categories");
        if (string.IsNullOrWhiteSpace(categories))
        {
            return targetItems;
        }

        if (nodeService != null)
        {
            AddCategoryTargetItems(targetItems, categories, nodeService, categoryPaths);
            return targetItems;
        }

        var scopeFactory = Configuration.Resolver.GetService<IServiceScopeFactory>();
        if (scopeFactory == null)
        {
            return targetItems;
        }

        using var scope = scopeFactory.CreateScope();
        var scopedNodeService = scope.ServiceProvider.GetRequiredService<INodeService>();
        AddCategoryTargetItems(targetItems, categories, scopedNodeService);

        return targetItems;
    }

    private static void AddCategoryTargetItems(
        HashSet<string> targetItems,
        string categories,
        INodeService nodeService,
        IDictionary<string, string?>? categoryPaths = null)
    {
        foreach (var category in categories.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (categoryPaths == null || !categoryPaths.TryGetValue(category, out var categoryPath))
            {
                categoryPath = nodeService.NodeById(category, false)?.Path;
                categoryPaths?.Add(category, categoryPath);
            }

            AddSplitItems(targetItems, categoryPath ?? string.Empty);
        }
    }

    private static void AddSplitItems(HashSet<string> targetItems, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            targetItems.Add(item);
        }
    }
}

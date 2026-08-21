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
    {
        if (!AreConstraintsMet(orderInfo, discount))
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
        var targetItems = GetOrderLineDiscountTargetItems(orderLine, nodeService);

        var matchesInclude = discount.GlobalDiscount
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

    private static HashSet<string> GetOrderLineDiscountTargetItems(
        IOrderLine orderLine,
        INodeService? nodeService)
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
            AddCategoryTargetItems(targetItems, categories, nodeService);
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
        INodeService nodeService)
    {
        foreach (var category in categories.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var categoryNode = nodeService.NodeById(category, false);
            if (categoryNode != null)
            {
                AddSplitItems(targetItems, categoryNode.Path);
            }
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

namespace Ekom.Utilities;

internal static class OrderDiscountPropertyDescriptions
{
    internal static string? GetDescription(string alias) => alias switch
    {
        "type" => "Choose a percentage reduction or a fixed amount off each discounted unit.",
        "discount" => "Enter the reduction: for example, 20 means 20% when Type is Percentage, or 20 in the selected currency when Type is Fixed. Fixed reductions apply per discounted unit, not once per order.",
        "discountItems" => "Select the products or categories that receive a non-global discount. Global Discount bypasses this selection. Threshold and Repeating still require a selection, even for global offers.",
        "excludeDiscountItems" => "Select products or categories that must not receive this discount, including global offers. These exclusions do not remove items from the qualifying quantity count.",
        "stackable" => "Allow this discount on top of an existing product reduction. For example, an additional 10% off a sale price of 80 gives 72, before rounding. This does not enable multiple coupons together.",
        "globalDiscount" => "Automatically evaluate this offer without a coupon. Rewards can apply to all eligible, non-excluded items, regardless of Discount Items. Adding coupon codes prevents automatic application.",
        "quantityDiscountMode" => "None: no quantity requirement. Threshold: reaching the minimum discounts all eligible whole units. Repeating: each complete qualifying group unlocks the configured number of reward units.",
        "qualifyingItems" => "Select products or categories whose whole-unit quantities count towards the requirement. These may differ from the items receiving the discount. Used by Threshold and Repeating.",
        "requiredQuantity" => "Minimum whole qualifying units for Threshold, or group size for Repeating. For example, 3 requires three qualifying units, not three different products. Must be positive.",
        "rewardQuantity" => "Whole eligible units discounted per completed group in Repeating mode. For example, Required Quantity 3 and Reward Quantity 1 unlock one discounted unit per group of three. Cheapest eligible units receive limited rewards first.",
        "coupons" => "Add or generate codes that activate this offer. Usage limits count remaining uses of each code across all customers, not uses per customer or logged-in account.",
        _ => null,
    };

    internal static bool ShouldUpdate(string alias, string? currentDescription)
    {
        var description = GetDescription(alias);
        if (description == null || string.Equals(currentDescription, description, StringComparison.Ordinal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(currentDescription))
        {
            return true;
        }

        var previousDescription = alias switch
        {
            "discountItems" => "Controls what items in the order receive the discount. (In contrast to product discount, discount items, where it is used as a constraint)",
            "excludeDiscountItems" => "Exclude items from discount items. For example if you select a category in discount items you can exclude a single product here.",
            "globalDiscount" => "This couponless discount will be automatically applied to orders that match it's constraints",
            "quantityDiscountMode" => "None uses normal discounts. Threshold discounts all eligible items after the requirement; Repeating unlocks rewards per group.",
            "qualifyingItems" => "Products and categories whose whole-unit quantities count towards this discount.",
            "requiredQuantity" => "Number of whole qualifying items required before rewards are discounted.",
            "rewardQuantity" => "Whole units discounted for each completed group in Repeating mode.",
            _ => null,
        };

        return previousDescription != null
            && string.Equals(currentDescription, previousDescription, StringComparison.Ordinal);
    }
}

using Ekom.Utilities;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class OrderDiscountPropertyDescriptionsTests
{
    public static TheoryData<string> PropertyAliases => new()
    {
        "type",
        "discount",
        "discountItems",
        "excludeDiscountItems",
        "stackable",
        "globalDiscount",
        "quantityDiscountMode",
        "qualifyingItems",
        "requiredQuantity",
        "rewardQuantity",
        "coupons",
    };

    [Theory]
    [MemberData(nameof(PropertyAliases))]
    public void BlankDescriptionsAreUpdated(string alias)
    {
        Assert.False(string.IsNullOrWhiteSpace(OrderDiscountPropertyDescriptions.GetDescription(alias)));
        Assert.True(OrderDiscountPropertyDescriptions.ShouldUpdate(alias, null));
        Assert.True(OrderDiscountPropertyDescriptions.ShouldUpdate(alias, ""));
        Assert.True(OrderDiscountPropertyDescriptions.ShouldUpdate(alias, "   "));
    }

    [Theory]
    [MemberData(nameof(PropertyAliases))]
    public void CurrentDescriptionsDoNotNeedAnotherUpdate(string alias)
    {
        var description = OrderDiscountPropertyDescriptions.GetDescription(alias);

        Assert.False(OrderDiscountPropertyDescriptions.ShouldUpdate(alias, description));
    }

    [Theory]
    [MemberData(nameof(PropertyAliases))]
    public void CustomDescriptionsArePreserved(string alias)
    {
        Assert.False(OrderDiscountPropertyDescriptions.ShouldUpdate(alias, "Our store's custom instructions."));
    }

    [Theory]
    [InlineData("discountItems", "Controls what items in the order receive the discount. (In contrast to product discount, discount items, where it is used as a constraint)")]
    [InlineData("excludeDiscountItems", "Exclude items from discount items. For example if you select a category in discount items you can exclude a single product here.")]
    [InlineData("globalDiscount", "This couponless discount will be automatically applied to orders that match it's constraints")]
    [InlineData("quantityDiscountMode", "None uses normal discounts. Threshold discounts all eligible items after the requirement; Repeating unlocks rewards per group.")]
    [InlineData("qualifyingItems", "Products and categories whose whole-unit quantities count towards this discount.")]
    [InlineData("requiredQuantity", "Number of whole qualifying items required before rewards are discounted.")]
    [InlineData("rewardQuantity", "Whole units discounted for each completed group in Repeating mode.")]
    public void KnownPreviousDescriptionsAreUpdatedOnce(string alias, string previousDescription)
    {
        Assert.True(OrderDiscountPropertyDescriptions.ShouldUpdate(alias, previousDescription));

        var updatedDescription = OrderDiscountPropertyDescriptions.GetDescription(alias);

        Assert.False(OrderDiscountPropertyDescriptions.ShouldUpdate(alias, updatedDescription));
        Assert.False(OrderDiscountPropertyDescriptions.ShouldUpdate(alias, previousDescription + " Custom restriction."));
    }

    [Theory]
    [InlineData("title")]
    [InlineData("description")]
    [InlineData("disabled")]
    [InlineData("startRange")]
    [InlineData("endRange")]
    [InlineData("customProperty")]
    public void UnrelatedPropertiesAreNotUpdated(string alias)
    {
        Assert.Null(OrderDiscountPropertyDescriptions.GetDescription(alias));
        Assert.False(OrderDiscountPropertyDescriptions.ShouldUpdate(alias, null));
        Assert.False(OrderDiscountPropertyDescriptions.ShouldUpdate(alias, "Custom help text."));
    }

    [Fact]
    public void PreviousDescriptionFromAnotherPropertyIsPreserved()
    {
        Assert.False(OrderDiscountPropertyDescriptions.ShouldUpdate("type",
            "Products and categories whose whole-unit quantities count towards this discount."));
    }
}

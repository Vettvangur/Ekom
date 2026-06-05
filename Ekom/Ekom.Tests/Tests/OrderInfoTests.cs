using Ekom.Models;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ekom.Tests.Tests;

public class OrderInfoTests
{
    [Fact]
    public void CreateOrderedDiscountFromJson_PreservesDecimalAmount()
    {
        JObject discountJson = CreateDiscountJson();

        OrderedDiscount? discount = CreateOrderedDiscountFromJson(discountJson);

        Assert.NotNull(discount);
        Assert.Equal(10m, discount.Amount);
    }

    [Fact]
    public void CreateOrderedDiscountFromJson_PreservesLegacyObjectAmountValue()
    {
        JObject discountJson = CreateDiscountJson();
        discountJson[nameof(OrderedDiscount.Amount)] = new JObject
        {
            ["Value"] = 12.34m,
        };

        OrderedDiscount? discount = CreateOrderedDiscountFromJson(discountJson);

        Assert.NotNull(discount);
        Assert.Equal(12.34m, discount.Amount);
    }

    [Fact]
    public void CreateOrderedDiscountFromJson_ReturnsNullForUnreadableObjectAmount()
    {
        JObject discountJson = CreateDiscountJson();
        discountJson[nameof(OrderedDiscount.Amount)] = new JObject
        {
            ["Currency"] = "ISK",
        };

        OrderedDiscount? discount = CreateOrderedDiscountFromJson(discountJson);

        Assert.Null(discount);
    }

    private static OrderedDiscount? CreateOrderedDiscountFromJson(JToken discountJson)
    {
        MethodInfo method = typeof(OrderInfo).GetMethod(
            "CreateOrderedDiscountFromJson",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        return (OrderedDiscount?)method.Invoke(null, new object[] { discountJson });
    }

    private static JObject CreateDiscountJson()
    {
        return new JObject
        {
            [nameof(OrderedDiscount.Key)] = Guid.NewGuid(),
            [nameof(OrderedDiscount.Title)] = "Test discount",
            [nameof(OrderedDiscount.Stackable)] = true,
            [nameof(OrderedDiscount.Amount)] = 10m,
            [nameof(OrderedDiscount.Type)] = DiscountType.Fixed.ToString(),
            [nameof(OrderedDiscount.DiscountItems)] = new JArray(),
            [nameof(OrderedDiscount.ExcludeDiscountItems)] = new JArray(),
            [nameof(OrderedDiscount.Constraints)] = null,
            [nameof(OrderedDiscount.HasMasterStock)] = false,
            [nameof(OrderedDiscount.GlobalDiscount)] = false,
        };
    }
}

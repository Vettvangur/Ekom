using Ekom.Models;
using Ekom.Tests.Objects;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ekom.Tests.Tests;

public class OrderInfoTests
{
    [Fact]
    public void Culture_UsesRequestCultureAndRetainsItWithoutARequest()
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext(),
        };
        httpContextAccessor.HttpContext.Features.Set<IRequestCultureFeature>(
            new RequestCultureFeature(new RequestCulture("da-DK"), null));

        using var configurationScope = new ConfigurationScope(addServices: services =>
            services.AddSingleton<IHttpContextAccessor>(httpContextAccessor));
        var store = new Mock<IStore>();
        store.SetupGet(x => x.Culture).Returns(new CultureInfoDto { Name = "en-US" });
        var orderInfo = new OrderInfo(new OrderData(), store.Object);

        Assert.Equal("da-DK", orderInfo.Culture);

        httpContextAccessor.HttpContext = null;

        Assert.Equal("da-DK", orderInfo.Culture);
    }

    [Fact]
    public void Constructor_PreservesLegacyTopLevelDiscountObjectAmountValue()
    {
        JObject orderInfoJson = new JObject
        {
            [nameof(OrderInfo.Culture)] = "en-US",
            [nameof(OrderInfo.OrderLines)] = new JArray(),
            [nameof(OrderInfo.Discount)] = CreateDiscountJson(),
        };
        ((JObject)orderInfoJson[nameof(OrderInfo.Discount)]!)[nameof(OrderedDiscount.Amount)] = new JObject
        {
            ["Value"] = 12.34m,
        };
        var orderData = new OrderData
        {
            OrderInfo = orderInfoJson.ToString(),
        };

        var orderInfo = new OrderInfo(orderData);

        Assert.NotNull(orderInfo.Discount);
        Assert.Equal(12.34m, orderInfo.Discount.Amount);
    }

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

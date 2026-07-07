using Ekom.API;
using Ekom.Models;
using Ekom.Tests.Objects;
using Ekom.Utilities;
using System.Runtime.CompilerServices;
using System.Reflection;
using Xunit;

namespace Ekom.Tests.Tests;

public class OrderCouponTests
{
    [Fact]
    public void TryRemoveCouponFromOrder_ClearsCouponAndDiscount_WhenCouponExists()
    {
        var orderInfo = CreateOrderInfo();
        var discount = CreateDiscount();

        SetProperty(orderInfo, nameof(OrderInfo.Coupon), "spring-sale");
        SetProperty(orderInfo, nameof(OrderInfo.Discount), discount);

        var result = InvokeTryRemoveCouponFromOrder(orderInfo);

        Assert.True(result);
        Assert.Null(orderInfo.Coupon);
        Assert.Null(orderInfo.Discount);
    }

    [Fact]
    public void TryRemoveCouponFromOrder_DoesNotClearDiscount_WhenCouponMissing()
    {
        var orderInfo = CreateOrderInfo();
        var discount = CreateDiscount();

        SetProperty(orderInfo, nameof(OrderInfo.Coupon), null);
        SetProperty(orderInfo, nameof(OrderInfo.Discount), discount);

        var result = InvokeTryRemoveCouponFromOrder(orderInfo);

        Assert.False(result);
        Assert.Null(orderInfo.Coupon);
        Assert.Same(discount, orderInfo.Discount);
    }

    private static OrderInfo CreateOrderInfo()
    {
        var orderData = new OrderData
        {
            UniqueId = Guid.NewGuid(),
            OrderNumber = "1",
            OrderStatus = OrderStatus.Pending,
            OrderInfo = string.Empty,
            CustomerEmail = string.Empty,
            CustomerUsername = string.Empty,
            ShippingCountry = string.Empty,
            Currency = "is-IS",
            StoreAlias = Stores.Store_IS_24Vat_VatIncluded.Alias,
        };

        return new OrderInfo(orderData, Stores.Store_IS_24Vat_VatIncluded);
    }

    private static OrderedDiscount CreateDiscount()
    {
        return (OrderedDiscount)RuntimeHelpers.GetUninitializedObject(typeof(OrderedDiscount));
    }

    private static bool InvokeTryRemoveCouponFromOrder(OrderInfo orderInfo)
    {
        var orderServiceType = typeof(Order).Assembly.GetType("Ekom.Services.OrderService");
        Assert.NotNull(orderServiceType);

        var method = orderServiceType!.GetMethod("TryRemoveCouponFromOrder", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method!.Invoke(null, new object[] { orderInfo });
        Assert.IsType<bool>(result);

        return (bool)result!;
    }

    private static void SetProperty(object instance, string propertyName, object? value)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);

        property!.SetValue(instance, value);
    }
}

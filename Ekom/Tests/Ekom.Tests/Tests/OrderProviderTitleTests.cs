using Ekom.Controllers;
using Ekom.Models;
using Ekom.Utilities;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ekom.Tests.Tests;

public class OrderProviderTitleTests
{
    [Theory]
    [InlineData("{\"values\":{\"Store\":\"Store title\",\"is-IS\":\"Icelandic title\",\"en-US\":\"English title\"}}", "Store title")]
    [InlineData("{\"values\":{\"is-IS\":\"Icelandic title\",\"en-US\":\"English title\"}}", "Icelandic title")]
    [InlineData("{\"values\":{\"en-US\":\"English title\"}}", "Node name")]
    [InlineData("Custom pickup", "Custom pickup")]
    public void Resolve_Uses_Store_Alias_Then_Order_Culture(string rawTitle, string expected)
    {
        var properties = new Dictionary<string, string>
        {
            ["nodeName"] = "Node name",
            ["title"] = rawTitle
        };

        Assert.Equal(expected, OrderProviderTitleResolver.Resolve(properties, "Store", "is-IS"));
    }

    [Fact]
    public void Manager_Response_Fills_Blank_Titles_Without_Changing_Saved_Order()
    {
        var currency = new CurrencyModel { CurrencyFormat = "C", CurrencyValue = "is-IS" };
        var storeInfo = new StoreInfo(Guid.NewGuid(), currency, [currency], "is-IS", "Store", true, 0, true);
        var payment = new JObject
        {
            ["Id"] = 1,
            ["Key"] = Guid.NewGuid(),
            ["Title"] = "",
            ["Properties"] = new JObject
            {
                ["nodeName"] = "Payment name",
                ["title"] = "{\"values\":{\"is-IS\":\"Greiðsla\"}}"
            }
        };
        var shipping = new JObject
        {
            ["Id"] = 2,
            ["Key"] = Guid.NewGuid(),
            ["Title"] = "",
            ["Properties"] = new JObject
            {
                ["nodeName"] = "Shipping name",
                ["title"] = "{\"values\":{\"Store\":\"Store pickup\",\"is-IS\":\"Pickup\"}}"
            }
        };
        var orderData = new OrderData
        {
            OrderStatusCol = nameof(OrderStatus.Incomplete),
            OrderInfo = new JObject
            {
                ["Culture"] = "is-IS",
                ["StoreInfo"] = JToken.FromObject(storeInfo),
                ["OrderLines"] = new JArray(),
                ["PaymentProvider"] = payment,
                ["ShippingProvider"] = shipping
            }.ToString()
        };
        var order = new OrderInfo(orderData);

        var response = new JObject
        {
            ["paymentProvider"] = new JObject { ["title"] = "" },
            ["shippingProvider"] = new JObject { ["title"] = "" }
        };
        EkomManagerController.ApplyProviderTitleFallbacks(response, order);

        Assert.Equal("Greiðsla", response["paymentProvider"]?["title"]?.Value<string>());
        Assert.Equal("Store pickup", response["shippingProvider"]?["title"]?.Value<string>());
        Assert.Equal(string.Empty, order.PaymentProvider?.Title);
        Assert.Equal(string.Empty, order.ShippingProvider?.Title);
    }

    [Fact]
    public void Manager_Response_Leaves_Existing_And_Dynamic_Titles_Unchanged()
    {
        var order = new Moq.Mock<IOrderInfo>();
        var storeInfo = new StoreInfo(Guid.NewGuid(), new CurrencyModel { CurrencyValue = "is-IS" }, [], "is-IS", "Store", true, 0, true);
        var payment = new Moq.Mock<OrderedPaymentProvider>(new JObject
        {
            ["Id"] = 1,
            ["Key"] = Guid.NewGuid(),
            ["Title"] = "Historic payment"
        }, storeInfo);
        payment.SetupGet(x => x.Title).Returns("Historic payment");
        var shipping = new OrderedShippingProvider(new JObject
        {
            ["Id"] = 2,
            ["Key"] = Guid.NewGuid(),
            ["Title"] = "Dynamic pickup"
        }, storeInfo);
        order.SetupGet(x => x.PaymentProvider).Returns(payment.Object);
        order.SetupGet(x => x.ShippingProvider).Returns(shipping);

        Assert.Same(order.Object, EkomManagerController.GetOrderInfoResponse(order.Object));
    }
}

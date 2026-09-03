using Ekom.Algolia.Models.Events;
using Ekom.Algolia.Services;
using Ekom.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Ekom.Tests.Tests;

public class AlgoliaInsightsClientTests
{
    [Theory]
    [InlineData("Added To Cart")]
    [InlineData("Started Checkout")]
    [InlineData("Purchased")]
    public async Task Sends_Ecommerce_Conversion_Object_Data_As_Array(string eventName)
    {
        using var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://insights.algolia.io/1/")
        };
        var client = new AlgoliaInsightsClient(httpClient, NullLogger<AlgoliaInsightsClient>.Instance);
        var evt = new AlgoliaInsightsEvent
        {
            EventType = "conversion",
            EventName = eventName,
            Index = "products",
            UserToken = "user-token",
            ObjectIds = ["product-1"],
            Currency = "USD",
            ObjectData =
            [
                new Dictionary<string, object?>
                {
                    ["queryID"] = "query-id",
                    ["price"] = 10m,
                    ["discount"] = 2m,
                    ["quantity"] = 3m
                }
            ]
        };

        await client.SendEventsAsync([evt]);

        using var document = JsonDocument.Parse(handler.RequestBody);
        var objectData = document.RootElement
            .GetProperty("events")[0]
            .GetProperty("objectData");

        Assert.Equal(JsonValueKind.Array, objectData.ValueKind);
        Assert.Equal("query-id", objectData[0].GetProperty("queryID").GetString());
        Assert.Equal(10m, objectData[0].GetProperty("price").GetDecimal());
        Assert.Equal(2m, objectData[0].GetProperty("discount").GetDecimal());
        Assert.Equal(3m, objectData[0].GetProperty("quantity").GetDecimal());
        var sentEvent = document.RootElement.GetProperty("events")[0];
        Assert.Equal("USD", sentEvent.GetProperty("currency").GetString());
        Assert.False(sentEvent.TryGetProperty("queryID", out _));
    }

    [Fact]
    public async Task Sends_Query_Id_When_Present()
    {
        using var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://insights.algolia.io/1/")
        };
        var client = new AlgoliaInsightsClient(httpClient, NullLogger<AlgoliaInsightsClient>.Instance);
        var evt = new AlgoliaInsightsEvent
        {
            EventType = "conversion",
            EventName = "Purchased",
            Index = "products",
            UserToken = "user-token",
            QueryId = "query-id",
            ObjectIds = ["product-1"]
        };

        await client.SendEventsAsync([evt]);

        using var document = JsonDocument.Parse(handler.RequestBody);
        var sentEvent = document.RootElement.GetProperty("events")[0];
        Assert.Equal("query-id", sentEvent.GetProperty("queryID").GetString());
    }

    [Fact]
    public void Creates_Ecommerce_Object_Data_With_Per_Unit_Price_And_Discount()
    {
        var orderLineKey = Guid.NewGuid();
        var tracking = new OrderTracking();
        tracking.Algolia.AddLine(orderLineKey, "query-id");

        var orderInfo = new Mock<IOrderInfo>();
        orderInfo.SetupGet(order => order.Tracking).Returns(tracking);

        var discountAmount = new Mock<ICalculatedPrice>();
        discountAmount.SetupGet(amount => amount.Value).Returns(3.99m);

        var amount = new Mock<IPrice>();
        amount.SetupGet(price => price.Value).Returns(59.97m);
        amount.SetupGet(price => price.DiscountAmount).Returns(discountAmount.Object);

        var orderLine = new Mock<IOrderLine>();
        orderLine.SetupGet(line => line.Key).Returns(orderLineKey);
        orderLine.SetupGet(line => line.Quantity).Returns(3m);
        orderLine.SetupGet(line => line.Amount).Returns(amount.Object);

        var objectData = AlgoliaEventService.CreateObjectData(orderInfo.Object, orderLine.Object);

        Assert.Equal("query-id", objectData["queryID"]);
        Assert.Equal(19.99m, objectData["price"]);
        Assert.Equal(1.33m, objectData["discount"]);
        Assert.Equal(3m, objectData["quantity"]);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}

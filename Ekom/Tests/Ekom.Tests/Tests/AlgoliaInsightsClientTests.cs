using Ekom.Algolia.Models.Events;
using Ekom.Algolia.Services;
using Microsoft.Extensions.Logging.Abstractions;
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
    public async Task Sends_Conversion_Object_Data_As_Array(string eventName)
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
            ObjectData =
            [
                new Dictionary<string, object?>
                {
                    ["value"] = 10m,
                    ["currency"] = "USD"
                }
            ]
        };

        await client.SendEventsAsync([evt]);

        using var document = JsonDocument.Parse(handler.RequestBody);
        var objectData = document.RootElement
            .GetProperty("events")[0]
            .GetProperty("objectData");

        Assert.Equal(JsonValueKind.Array, objectData.ValueKind);
        Assert.Equal(10m, objectData[0].GetProperty("value").GetDecimal());
        Assert.Equal("USD", objectData[0].GetProperty("currency").GetString());
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

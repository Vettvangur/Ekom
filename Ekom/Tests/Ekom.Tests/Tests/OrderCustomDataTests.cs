using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ekom.Tests.Tests;

public class OrderCustomDataTests
{
    [Theory]
    [InlineData("{\"CustomData\":{\"Gift\":null}}")]
    [InlineData("{\"CustomData\":{\"Gift\":2}}")]
    [InlineData("{\"CustomData\":{\"Gift\":{}}}")]
    [InlineData("{\"CustomData\":{\"Gift\":[]}}")]
    [InlineData("{\"CustomData\":null}")]
    [InlineData("{\"CustomData\":{\"Gift\":\"a\",\"gift\":\"b\"}}")]
    [InlineData("{\"CustomData\":{\"Gift\":\"a\",\"Gift\":\"b\"}}")]
    [InlineData("{\"CustomData\":{\" \":\"a\"}}")]
    public void RejectsInvalidJson(string json)
        => Assert.Throws<OrderCustomDataException>(() => OrderCustomData.FromJson(OrderCustomData.ParseJson(json)));

    [Fact]
    public void RejectsRepeatedFormValues()
        => Assert.Throws<OrderCustomDataException>(() => OrderCustomData.FromForm(new FormCollection(
            new Dictionary<string, StringValues> { ["CustomData[Gift]"] = new[] { "a", "b" } })));

    [Theory]
    [InlineData("CustomData[]")]
    [InlineData("CustomData[ ]")]
    [InlineData("CustomData[Gift")]
    [InlineData("CustomData[Gift][Nested]")]
    public void RejectsMalformedFields(string key)
        => Assert.Throws<OrderCustomDataException>(() => OrderCustomData.ReadFields(new Dictionary<string, string> { [key] = "value" }));

    [Fact]
    public void PreservesCustomTextAndExistingEncoding()
    {
        var fields = OrderCustomData.FromJson(JObject.Parse("{\"CUSTOMDATA\":{\"Gift\":\"A & <B>\"},\"customerName\":\"A & B\"}"));
        Assert.Equal("A & <B>", OrderCustomData.ReadFields(fields)["gift"]);
        Assert.Equal("A &amp; B", fields["customerName"]);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"CustomData\":null}")]
    public void LegacyOrdersHaveEmptyCustomData(string json)
        => Assert.Empty(new OrderInfo(new OrderData { OrderInfo = WithOrderLines(json) }).CustomData);

    [Fact]
    public void HydrationAndSnapshotsPreserveComparerWithoutSharingDictionary()
    {
        var data = new OrderData { UniqueId = Guid.NewGuid(), OrderInfo = WithOrderLines("{\"CustomData\":{\"Gift\":\"A & B\"}}") };
        var first = new OrderInfo(data) { Culture = "en-US" };
        var second = new OrderInfo(data);
        second.ApplyPersistedSnapshot(first, data);
        first.CustomData["gift"] = "changed";
        Assert.Equal("A & B", second.CustomData["GIFT"]);
    }

    private static string WithOrderLines(string json)
    {
        var value = JObject.Parse(json);
        value["OrderLines"] = new JArray();
        return value.ToString();
    }

    [Fact]
    public void CustomDataDoesNotChangeSubmittedPurchaseComparison()
    {
        Assert.True(CheckoutPaymentAttemptService.SamePurchase("{\"Amount\":4}", "{\"Amount\":4,\"CustomData\":{\"Gift\":\"hello\"}}"));
        Assert.False(CheckoutPaymentAttemptService.SamePurchase("{\"Amount\":4}", "{\"Amount\":5,\"CustomData\":{\"Gift\":\"hello\"}}"));
    }
}

using Ekom.Models;
using Ekom.Models.Orders;
using System.Text.Json;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class LegacyOrderStatusMapperTests
{
    [Theory]
    [InlineData("Incomplete", OrderStatus.Draft, PaymentStatus.Pending, FulfillmentStatus.Unfulfilled)]
    [InlineData("Wishlist", OrderStatus.Draft, PaymentStatus.Pending, FulfillmentStatus.Unfulfilled)]
    [InlineData("WaitingForPayment", OrderStatus.Draft, PaymentStatus.Pending, FulfillmentStatus.Unfulfilled)]
    [InlineData("PaymentFailed", OrderStatus.Draft, PaymentStatus.Failed, FulfillmentStatus.Unfulfilled)]
    [InlineData("Pending", OrderStatus.Open, PaymentStatus.Pending, FulfillmentStatus.Unfulfilled)]
    [InlineData("OfflinePayment", OrderStatus.Open, PaymentStatus.Paid, FulfillmentStatus.Unfulfilled)]
    [InlineData("ReadyForDispatch", OrderStatus.Open, PaymentStatus.Paid, FulfillmentStatus.Unfulfilled)]
    [InlineData("ReadyForDispatchWhenStockArrives", OrderStatus.Open, PaymentStatus.Paid, FulfillmentStatus.Unfulfilled)]
    [InlineData("ReadyForPickup", OrderStatus.Open, PaymentStatus.Paid, FulfillmentStatus.Packed)]
    [InlineData("Dispatched", OrderStatus.Open, PaymentStatus.Paid, FulfillmentStatus.Fulfilled)]
    [InlineData("Closed", OrderStatus.Closed, PaymentStatus.Paid, FulfillmentStatus.Fulfilled)]
    public void ProjectsKnownLegacyStates(string legacy, OrderStatus order, PaymentStatus payment, FulfillmentStatus fulfillment)
    {
        var result = LegacyOrderStatusMapper.Map(legacy);
        Assert.Equal(order, result.OrderStatus);
        Assert.Equal(payment, result.PaymentStatus);
        Assert.Equal(fulfillment, result.FulfillmentStatus);
        Assert.False(result.RequiresReview);
    }

    [Fact]
    public void CancelledDefaultsToPendingAndUnfulfilledButPreservesPaidDate()
    {
        var pending = LegacyOrderStatusMapper.Map("Cancelled");
        Assert.Equal(OrderStatus.Cancelled, pending.OrderStatus);
        Assert.Equal(PaymentStatus.Pending, pending.PaymentStatus);
        Assert.Equal(FulfillmentStatus.Unfulfilled, pending.FulfillmentStatus);
        Assert.False(pending.RequiresReview);
        Assert.Equal(PaymentStatus.Paid, LegacyOrderStatusMapper.Map("Cancelled", DateTime.UtcNow).PaymentStatus);
    }

    [Fact]
    public void ReturnedDoesNotInventRefund()
    {
        var result = LegacyOrderStatusMapper.Map("Returned", DateTime.UtcNow);
        Assert.Equal(PaymentStatus.Paid, result.PaymentStatus);
        Assert.True(result.RequiresReview);
    }

    [Fact]
    public void PaymentEvidenceIsPreservedAndContradictionIsFlagged()
    {
        var result = LegacyOrderStatusMapper.Map("PaymentFailed", DateTime.UtcNow);
        Assert.Equal(PaymentStatus.Paid, result.PaymentStatus);
        Assert.True(result.RequiresReview);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("999")]
    [InlineData("CustomStatus")]
    public void UnknownLegacyValueIsReportedWithoutThrowing(string? legacy)
    {
        var result = LegacyOrderStatusMapper.Map(legacy);
        Assert.Null(result.OrderStatus);
        Assert.True(result.RequiresReview);
    }

    [Fact]
    public void NumericLegacyValuesRetainTheirOriginalMeaning()
    {
        foreach (var legacy in Enum.GetValues<Utilities.OrderStatus>())
            Assert.Equal(LegacyOrderStatusMapper.Map(legacy.ToString()),
                LegacyOrderStatusMapper.Map(((int)legacy).ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void ReadingAndSerializingDoesNotChangeLegacyStateOrPaidDate()
    {
        var order = new OrderData { OrderStatusCol = "Dispatched" };
        var json = JsonSerializer.Serialize(order.Status, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);
        Assert.Equal("Open", document.RootElement.GetProperty("orderStatus").GetString());
        Assert.Equal("Fulfilled", document.RootElement.GetProperty("fulfillmentStatus").GetString());
        Assert.Equal("Dispatched", order.OrderStatusCol);
        Assert.Null(order.PaidDate);
    }
}

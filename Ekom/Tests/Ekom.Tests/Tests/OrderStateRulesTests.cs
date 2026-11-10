using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Models.Orders;
using Ekom.Services;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class OrderStateRulesTests
{
    [Theory]
    [InlineData("Pending")]
    [InlineData("Authorized")]
    [InlineData("Paid")]
    [InlineData("PartiallyPaid")]
    [InlineData("Failed")]
    [InlineData("Voided")]
    public void ManualPaymentStatusChangesClassificationOnly(string target)
    {
        var order = PaidOrder();
        order.TotalAmount = 123m;
        order.OrderInfo = "{\"preserve\":true}";
        var paid = order.PaidDate;
        OrderStateService.Apply(order, new(0, "Payment" + target), null);
        Assert.Equal(target, order.Status.PaymentStatus.ToString());
        Assert.Equal(123m, order.TotalAmount);
        Assert.Equal("{\"preserve\":true}", order.OrderInfo);
        Assert.Equal(paid, order.PaidDate);
        OrderStateService.Apply(order, new(0, "PaymentPending"), null);
        Assert.Equal(PaymentStatus.Pending, order.Status.PaymentStatus);
    }

    [Fact]
    public void ManualPaidAutoClosesFulfilledOrderAndRefundCommandsAreRejected()
    {
        var order = PaidOrder();
        OrderStateService.Apply(order, new(0, "PaymentPending"), null);
        OrderStateService.Apply(order, new(0, "Fulfill"), new Dictionary<Guid, decimal> { [Guid.NewGuid()] = 1 });
        OrderStateRules.AutoClose(order, true);
        Assert.Equal(OrderStatus.Open, order.Status.OrderStatus);
        Assert.Throws<EkomHttpException>(() => OrderStateService.Apply(order, new(0, "PaymentRefunded"), null));
        Assert.Throws<EkomHttpException>(() => OrderStateService.Apply(order, new(0, "PaymentPartiallyRefunded"), null));
        OrderStateService.Apply(order, new(0, "PaymentPaid"), null);
        OrderStateRules.AutoClose(order, true);
        Assert.Equal(OrderStatus.Closed, order.Status.OrderStatus);
        Assert.Throws<EkomHttpException>(() => OrderStateService.Apply(order, new(0, "PaymentPending"), null));
        OrderStateService.Apply(order, new(0, "Reopen"), null);
        OrderStateService.Apply(order, new(0, "PaymentPending"), null);
        Assert.Equal(PaymentStatus.Pending, order.Status.PaymentStatus);
    }

    [Theory]
    [InlineData("Unfulfilled")]
    [InlineData("Picking")]
    [InlineData("Picked")]
    [InlineData("Packed")]
    [InlineData("PartiallyFulfilled")]
    public void FulfillmentCanBeCorrectedAfterReopening(string target)
    {
        var order = PaidOrder();
        var lines = new Dictionary<Guid, decimal> { [Guid.NewGuid()] = 2 };
        OrderStateService.Apply(order, new(0, "Fulfill"), lines);
        OrderStateRules.AutoClose(order, true);
        Assert.Throws<EkomHttpException>(() => OrderStateService.Apply(order, new(0, target), null));
        OrderStateService.Apply(order, new(0, "Reopen"), null);
        OrderStateService.Apply(order, new(0, target), null);
        Assert.Equal(target, order.Status.FulfillmentStatus.ToString());
        Assert.Empty(OrderStateRules.Quantities(order));
        OrderStateRules.AutoClose(order, true);
        Assert.Equal(OrderStatus.Open, order.Status.OrderStatus);
        OrderStateService.Apply(order, new(0, "Fulfill"), lines);
        Assert.Equal(FulfillmentStatus.Fulfilled, order.Status.FulfillmentStatus);
    }

    private static OrderData PaidOrder()
    {
        var order = new OrderData { OrderStatusCol = "ReadyForDispatch" };
        Assert.True(OrderStateRules.Initialize(order, true));
        return order;
    }

    [Fact]
    public void PaidCheckoutRemainsOpenUntilEveryQuantityIsFulfilled()
    {
        var order = PaidOrder();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var lines = new Dictionary<Guid, decimal> { [first] = 2, [second] = 1 };
        OrderStateService.Apply(order, new(0, "Fulfill", new Dictionary<Guid, decimal> { [first] = 1 }), lines);
        OrderStateRules.AutoClose(order, true);
        Assert.Equal(OrderStatus.Open, order.Status.OrderStatus);
        Assert.Equal(FulfillmentStatus.PartiallyFulfilled, order.Status.FulfillmentStatus);
        OrderStateService.Apply(order, new(0, "Fulfill"), lines);
        OrderStateRules.AutoClose(order, true);
        Assert.Equal(OrderStatus.Closed, order.Status.OrderStatus);
        Assert.Equal(FulfillmentStatus.Fulfilled, order.Status.FulfillmentStatus);
    }

    [Fact]
    public void ReopeningSuppressesAutomaticClosingAndAllowsExplicitClose()
    {
        var order = PaidOrder();
        order.FulfillmentStateCol = "Fulfilled";
        OrderStateRules.AutoClose(order, true);
        OrderStateService.Apply(order, new(0, "Reopen"), null);
        OrderStateRules.AutoClose(order, true);
        Assert.Equal(OrderStatus.Open, order.Status.OrderStatus);
        Assert.True(order.Status.AutoCloseSuppressed);
        OrderStateService.Apply(order, new(0, "Close"), null);
        Assert.Equal(OrderStatus.Closed, order.Status.OrderStatus);
    }

    [Fact]
    public void AutomaticClosingHonorsConfigurationAndPayment()
    {
        var order = PaidOrder();
        order.FulfillmentStateCol = "Fulfilled";
        OrderStateRules.AutoClose(order, false);
        Assert.Equal(OrderStatus.Open, order.Status.OrderStatus);
        order.PaymentStateCol = "Pending";
        OrderStateRules.AutoClose(order, true);
        Assert.Equal(OrderStatus.Open, order.Status.OrderStatus);
        Assert.Throws<EkomHttpException>(() => OrderStateService.Apply(order, new(0, "Close"), null));
    }

    [Fact]
    public void FulfillmentRejectsUnknownLinesExcessAndDecreasingQuantities()
    {
        var order = PaidOrder();
        var id = Guid.NewGuid();
        var lines = new Dictionary<Guid, decimal> { [id] = 2 };
        foreach (var quantity in new[] { -1m, 3m })
            Assert.Throws<EkomHttpException>(() => OrderStateService.Apply(order, new(0, "Fulfill", new Dictionary<Guid, decimal> { [id] = quantity }), lines));
        Assert.Throws<EkomHttpException>(() => OrderStateService.Apply(order, new(0, "Fulfill", new Dictionary<Guid, decimal> { [Guid.NewGuid()] = 1 }), lines));
        OrderStateService.Apply(order, new(0, "Fulfill", new Dictionary<Guid, decimal> { [id] = 1 }), lines);
        Assert.Throws<EkomHttpException>(() => OrderStateService.Apply(order, new(0, "Fulfill", new Dictionary<Guid, decimal> { [id] = 0 }), lines));
        OrderStateService.Apply(order, new(0, "Packed"), null);
        Assert.Equal(FulfillmentStatus.Packed, order.Status.FulfillmentStatus);
        Assert.Empty(OrderStateRules.Quantities(order));
    }

    [Fact]
    public void LegacySavePreservesCanonicalPreparationAndOriginalEvidence()
    {
        var previous = PaidOrder();
        previous.FulfillmentStateCol = "Picked";
        previous.StatusRevision = 4;
        var stale = new OrderData { OrderStatusCol = "ReadyForDispatch" };
        OrderStateRules.SynchronizeLegacy(stale, previous, true);
        Assert.Equal(FulfillmentStatus.Picked, stale.Status.FulfillmentStatus);
        Assert.Equal(5, stale.Status.Revision);
        Assert.Equal("ReadyForDispatch", stale.OriginalOrderStatusCol);
    }

    [Fact]
    public void AmbiguousHistoryIsNotSilentlyMigrated()
    {
        var order = new OrderData { OrderStatusCol = "Returned" };
        Assert.False(OrderStateRules.Initialize(order, true));
        Assert.Equal(0, order.StatusMappingVersion);
        Assert.True(order.Status.RequiresReview);
    }

    [Fact]
    public void ManagerNewtonsoftSerializationUsesStatusNames()
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(PaidOrder().Status);
        Assert.Contains("\"PaymentStatus\":\"Paid\"", json);
        Assert.Contains("\"FulfillmentStatus\":\"Unfulfilled\"", json);
    }
}

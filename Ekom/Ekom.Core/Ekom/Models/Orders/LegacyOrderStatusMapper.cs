using LegacyStatus = Ekom.Utilities.OrderStatus;

namespace Ekom.Models.Orders;

/// <summary>
/// Read-only projection of legacy orders. This is not a transition service or a backfill:
/// it never closes an order, creates financial evidence, or executes checkout side effects.
/// </summary>
public static class LegacyOrderStatusMapper
{
    public static OrderStatusSnapshot Map(string? legacyStatus, DateTime? paidDate = null)
    {
        if (!Enum.TryParse<LegacyStatus>(legacyStatus, out var status) || !Enum.IsDefined(status))
            return new(null, null, null, true, legacyStatus ?? string.Empty);

        PaymentStatus? payment = paidDate.HasValue ? PaymentStatus.Paid : null;
        var result = status switch
        {
            LegacyStatus.Incomplete or LegacyStatus.Wishlist =>
                new OrderStatusSnapshot(OrderStatus.Draft, payment ?? PaymentStatus.Pending, FulfillmentStatus.Unfulfilled, false, status.ToString()),
            LegacyStatus.WaitingForPayment =>
                new(OrderStatus.Draft, payment ?? PaymentStatus.Pending, FulfillmentStatus.Unfulfilled, false, status.ToString()),
            LegacyStatus.PaymentFailed =>
                new(OrderStatus.Draft, payment ?? PaymentStatus.Failed, FulfillmentStatus.Unfulfilled, paidDate.HasValue, status.ToString()),
            LegacyStatus.Pending =>
                new(OrderStatus.Open, payment ?? PaymentStatus.Pending, FulfillmentStatus.Unfulfilled, false, status.ToString()),
            LegacyStatus.OfflinePayment or LegacyStatus.ReadyForDispatch or LegacyStatus.ReadyForDispatchWhenStockArrives =>
                new(OrderStatus.Open, PaymentStatus.Paid, FulfillmentStatus.Unfulfilled, false, status.ToString()),
            LegacyStatus.ReadyForPickup =>
                new(OrderStatus.Open, PaymentStatus.Paid, FulfillmentStatus.Packed, false, status.ToString()),
            LegacyStatus.Dispatched =>
                new(OrderStatus.Open, PaymentStatus.Paid, FulfillmentStatus.Fulfilled, false, status.ToString()),
            LegacyStatus.Closed => new(OrderStatus.Closed, PaymentStatus.Paid, FulfillmentStatus.Fulfilled, false, status.ToString()),
            LegacyStatus.Cancelled => new(OrderStatus.Cancelled, payment ?? PaymentStatus.Pending, FulfillmentStatus.Unfulfilled, false, status.ToString()),
            // A return is not evidence of a refund. Keep it unresolved for migration review.
            LegacyStatus.Returned => new(OrderStatus.Closed, payment, FulfillmentStatus.Fulfilled, true, status.ToString()),
            _ => new(null, null, null, true, status.ToString()),
        };

        return result;
    }
}

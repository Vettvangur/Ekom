using System.Text.Json;
using LegacyStatus = Ekom.Utilities.OrderStatus;

namespace Ekom.Models.Orders;

internal static class OrderStateRules
{
    public static OrderStatusSnapshot Read(OrderData data)
    {
        if (data.StatusMappingVersion == 0)
            return MapLegacy(data) with { Revision = data.StatusRevision };
        var order = Parse<OrderStatus>(data.OrderStateCol);
        var payment = Parse<PaymentStatus>(data.PaymentStateCol);
        var fulfillment = Parse<FulfillmentStatus>(data.FulfillmentStateCol);
        return new(order, payment, fulfillment, order == null || payment == null || fulfillment == null,
            data.OrderStatusCol, data.StatusRevision, data.AutoCloseSuppressed, Quantities(data));
    }

    private static OrderStatusSnapshot MapLegacy(OrderData data)
    {
        var mapped = LegacyOrderStatusMapper.Map(data.OrderStatusCol, data.PaidDate);
        if (mapped.OrderStatus != OrderStatus.Cancelled) return mapped;
        var payment = Parse<PaymentStatus>(data.PaymentStateCol);
        var fulfillment = Parse<FulfillmentStatus>(data.FulfillmentStateCol);
        var order = Parse<OrderStatus>(data.OrderStateCol);
        var review = (!string.IsNullOrEmpty(data.PaymentStateCol) && payment == null)
            || (!string.IsNullOrEmpty(data.FulfillmentStateCol) && fulfillment == null)
            || (!string.IsNullOrEmpty(data.OrderStateCol) && order != OrderStatus.Cancelled)
            || (data.PaidDate.HasValue && payment.HasValue && payment != PaymentStatus.Paid);
        IReadOnlyDictionary<Guid, decimal>? quantities = null;
        try
        {
            quantities = Quantities(data);
            // Quantities without a compatible summary need review, not an invented completion state.
            review |= quantities.Any(x => x.Key == Guid.Empty || x.Value < 0)
                || (quantities.Any(x => x.Value > 0)
                    && fulfillment is not (FulfillmentStatus.Fulfilled or FulfillmentStatus.PartiallyFulfilled));
        }
        catch (JsonException) { review = true; }
        return mapped with
        {
            PaymentStatus = payment ?? mapped.PaymentStatus,
            FulfillmentStatus = fulfillment ?? mapped.FulfillmentStatus,
            RequiresReview = review,
            FulfilledQuantities = quantities,
        };
    }

    private static T? Parse<T>(string? value) where T : struct, Enum
        => Enum.TryParse<T>(value, out var result) && Enum.IsDefined(result) ? result : null;

    public static Dictionary<Guid, decimal> Quantities(OrderData data)
        => string.IsNullOrWhiteSpace(data.FulfilledQuantitiesJson) ? new()
            : JsonSerializer.Deserialize<Dictionary<Guid, decimal>>(data.FulfilledQuantitiesJson) ?? new();

    public static bool Initialize(OrderData data, bool autoClose)
    {
        if (data.StatusMappingVersion != 0) return true;
        var mapped = MapLegacy(data);
        if (mapped.RequiresReview) return false;
        data.OriginalOrderStatusCol ??= data.OrderStatusCol;
        data.StatusMappingVersion = 1;
        Set(data, mapped);
        AutoClose(data, autoClose);
        return true;
    }

    public static void Set(OrderData data, OrderStatusSnapshot state)
    {
        data.OrderStateCol = state.OrderStatus?.ToString();
        data.PaymentStateCol = state.PaymentStatus?.ToString();
        data.FulfillmentStateCol = state.FulfillmentStatus?.ToString();
    }

    public static void Copy(OrderData target, OrderData source)
    {
        target.OrderStateCol = source.OrderStateCol;
        target.PaymentStateCol = source.PaymentStateCol;
        target.FulfillmentStateCol = source.FulfillmentStateCol;
        target.OriginalOrderStatusCol = source.OriginalOrderStatusCol;
        target.StatusMappingVersion = source.StatusMappingVersion;
        target.StatusRevision = source.StatusRevision;
        target.AutoCloseSuppressed = source.AutoCloseSuppressed;
        target.FulfilledQuantitiesJson = source.FulfilledQuantitiesJson;
    }

    // Legacy callers may save stale OrderData. Preserve canonical fields from the database,
    // then interpret only an actual legacy-status change instead of replaying migration defaults.
    public static void SynchronizeLegacy(OrderData data, OrderData? previous, bool autoClose)
    {
        if (previous == null)
        {
            Initialize(data, autoClose);
            data.StatusRevision = 1;
            return;
        }
        Copy(data, previous);
        Initialize(data, autoClose);
        if (data.StatusMappingVersion == 1 && data.OrderStatusCol != previous.OrderStatusCol)
        {
            var current = Read(data);
            var mapped = LegacyOrderStatusMapper.Map(data.OrderStatusCol, data.PaidDate);
            if (Enum.TryParse<LegacyStatus>(data.OrderStatusCol, out var legacy))
            {
                var order = current.OrderStatus;
                var payment = current.PaymentStatus;
                var fulfillment = current.FulfillmentStatus;
                switch (legacy)
                {
                    case LegacyStatus.Cancelled: order = OrderStatus.Cancelled; break;
                    case LegacyStatus.Closed: order = OrderStatus.Closed; break;
                    case LegacyStatus.Returned: order = OrderStatus.Closed; break;
                    case LegacyStatus.Incomplete:
                    case LegacyStatus.Wishlist:
                    case LegacyStatus.WaitingForPayment:
                    case LegacyStatus.PaymentFailed:
                        if (order == OrderStatus.Draft && payment != PaymentStatus.Paid) payment = mapped.PaymentStatus;
                        break;
                    default:
                        if (order == OrderStatus.Draft) order = OrderStatus.Open;
                        if (payment is PaymentStatus.Pending or PaymentStatus.Failed or PaymentStatus.Authorized)
                            payment = mapped.PaymentStatus ?? payment;
                        if (legacy == LegacyStatus.Dispatched) fulfillment = FulfillmentStatus.Fulfilled;
                        else if (legacy == LegacyStatus.ReadyForPickup && fulfillment is not (FulfillmentStatus.Fulfilled or FulfillmentStatus.PartiallyFulfilled))
                            fulfillment = FulfillmentStatus.Packed;
                        break;
                }
                Set(data, current with { OrderStatus = order, PaymentStatus = payment, FulfillmentStatus = fulfillment });
            }
        }
        AutoClose(data, autoClose);
        data.StatusRevision = checked(previous.StatusRevision + 1);
    }

    public static void AutoClose(OrderData data, bool enabled)
    {
        if (enabled && !data.AutoCloseSuppressed && data.OrderStateCol == nameof(OrderStatus.Open)
            && data.PaymentStateCol == nameof(PaymentStatus.Paid) && data.FulfillmentStateCol == nameof(FulfillmentStatus.Fulfilled))
            data.OrderStateCol = nameof(OrderStatus.Closed);
    }

    public static string Project(OrderData data)
    {
        var state = Read(data);
        if (state.OrderStatus == OrderStatus.Cancelled) return nameof(LegacyStatus.Cancelled);
        if (state.OrderStatus == OrderStatus.Closed) return nameof(LegacyStatus.Closed);
        if (state.OrderStatus == OrderStatus.Draft) return data.OrderStatusCol;
        if (state.FulfillmentStatus == FulfillmentStatus.Fulfilled) return nameof(LegacyStatus.Dispatched);
        if (state.PaymentStatus == PaymentStatus.Paid) return nameof(LegacyStatus.ReadyForDispatch);
        return data.OrderStatusCol == nameof(LegacyStatus.OfflinePayment) ? data.OrderStatusCol : nameof(LegacyStatus.Pending);
    }
}

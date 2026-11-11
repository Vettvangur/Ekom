using System.Text.Json.Serialization;

namespace Ekom.Models.Orders;

[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
[JsonConverter(typeof(JsonStringEnumConverter<OrderStatus>))]
public enum OrderStatus
{
    Draft = 0,
    Open = 1,
    Closed = 2,
    Cancelled = 3,
}

[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
[JsonConverter(typeof(JsonStringEnumConverter<PaymentStatus>))]
public enum PaymentStatus
{
    Pending = 0,
    Authorized = 1,
    Paid = 2,
    PartiallyPaid = 3,
    Failed = 4,
    PartiallyRefunded = 5,
    Refunded = 6,
    Voided = 7,
}

[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
[JsonConverter(typeof(JsonStringEnumConverter<FulfillmentStatus>))]
public enum FulfillmentStatus
{
    Unfulfilled = 0,
    Picking = 1,
    Picked = 2,
    Packed = 3,
    PartiallyFulfilled = 4,
    Fulfilled = 5,
}

/// <summary>
/// Independent status dimensions. Null values mean historical evidence is insufficient;
/// they are not additional business statuses. Legacy projections do not modify stored orders.
/// </summary>
public sealed record OrderStatusSnapshot(
    OrderStatus? OrderStatus,
    PaymentStatus? PaymentStatus,
    FulfillmentStatus? FulfillmentStatus,
    bool RequiresReview,
    string LegacyStatus,
    int Revision = 0,
    bool AutoCloseSuppressed = false,
    IReadOnlyDictionary<Guid, decimal>? FulfilledQuantities = null);

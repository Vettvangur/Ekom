using Ekom.Models.Shipments;

namespace Ekom.Shipping;

public sealed record ShippingFulfillmentRecord(
    Guid OrderId,
    Guid ShippingProviderKey,
    string CarrierAlias,
    string AccountReference,
    string ServiceId,
    ShipmentState State,
    string? BookingReference,
    string? ShipmentId,
    string? TrackingNumber,
    int AttemptCount,
    string? LastError,
    DateTime OrderCreatedUtc,
    DateTime UpdatedUtc,
    IReadOnlyList<StoredShippingDocument>? Documents = null)
{
    /// <summary>The shipment in Ekom's shipment records (<see cref="Models.Shipments.OrderShipment.ShipmentId"/>).</summary>
    public Guid RecordId { get; init; }

    public string? TrackingUrl { get; init; }
}

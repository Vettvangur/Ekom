namespace Ekom.Shipping;

public interface IShippingFulfillmentCarrier
{
    string Alias { get; }

    ShippingCarrierCapabilities Capabilities { get; }

    ShippingFulfillmentMode GetFulfillmentMode(string accountReference);

    Task<string?> ReserveBookingReferenceAsync(
        string accountReference,
        CancellationToken cancellationToken = default);

    Task<ShipmentBookingResult> CreateShipmentAsync(
        string accountReference,
        ShipmentBookingRequest request,
        CancellationToken cancellationToken = default);

    Task<ShippingLabel> GetLabelAsync(
        string accountReference,
        string shipmentId,
        CancellationToken cancellationToken = default);

    Task<ShippingTrackingResult> GetTrackingAsync(
        string accountReference,
        string trackingNumber,
        string language,
        CancellationToken cancellationToken = default);
}

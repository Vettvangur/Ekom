namespace Ekom.Shipping;

public interface IShippingCarrier
{
    string Alias { get; }

    ShippingCarrierCapabilities Capabilities { get; }

    Task<IReadOnlyList<ShippingService>> GetServicesAsync(
        string accountReference,
        ShippingLookupRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PickupLocation>> GetPickupLocationsAsync(
        string accountReference,
        string serviceId,
        ShippingLookupRequest request,
        CancellationToken cancellationToken = default);
}

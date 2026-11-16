namespace Ekom.Shipping;

[Flags]
public enum ShippingCarrierCapabilities
{
    None = 0,
    Services = 1,
    PickupLocations = 2,
    ShipmentBooking = 4,
    Labels = 8,
    Tracking = 16,
    ShipmentLookup = 32,
    ShipmentDeletion = 64,
}

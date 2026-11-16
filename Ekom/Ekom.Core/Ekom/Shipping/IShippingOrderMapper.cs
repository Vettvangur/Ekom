using Ekom.Models;

namespace Ekom.Shipping;

public interface IShippingOrderMapper
{
    ShipmentBookingRequest Map(
        IOrderInfo order,
        EkomShippingMethodConfiguration configuration,
        string? bookingReference);
}

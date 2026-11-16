using Ekom.Models;

namespace Ekom.Shipping;

public interface IEkomShippingCheckoutService
{
    Task<IReadOnlyList<ShippingService>> GetServicesAsync(
        IShippingProvider provider,
        ShippingLookupRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PickupLocation>> GetPickupLocationsAsync(
        IShippingProvider provider,
        ShippingLookupRequest request,
        CancellationToken cancellationToken = default);

    Task<ValidatedShippingSelection> ValidateAsync(
        IShippingProvider provider,
        string? pickupLocationId,
        ShippingLookupRequest request,
        CancellationToken cancellationToken = default);

    Dictionary<string, string> BuildOrderData(
        ValidatedShippingSelection selection,
        IReadOnlyDictionary<string, string>? existingData = null);

    Task<IOrderInfo> SaveSelectionAsync(
        IShippingProvider provider,
        string? pickupLocationId,
        ShippingLookupRequest request,
        IReadOnlyDictionary<string, string>? existingData = null,
        CancellationToken cancellationToken = default);
}

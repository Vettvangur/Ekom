namespace Ekom.Shipping;

public interface IShippingSelectionValidator
{
    Task<ValidatedShippingSelection> ValidateAsync(
        ShippingSelection selection,
        ShippingLookupRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed class ShippingSelectionValidator : IShippingSelectionValidator
{
    private readonly IShippingCarrierRegistry _registry;

    public ShippingSelectionValidator(IShippingCarrierRegistry registry)
    {
        _registry = registry;
    }

    public async Task<ValidatedShippingSelection> ValidateAsync(
        ShippingSelection selection,
        ShippingLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        var carrier = _registry.GetRequired(selection.CarrierAlias);
        var services = await carrier.GetServicesAsync(
            selection.AccountReference,
            request,
            cancellationToken).ConfigureAwait(false);
        var service = services.FirstOrDefault(x =>
            string.Equals(x.Id, selection.ServiceId, StringComparison.Ordinal));

        if (service is null)
        {
            var available = services.Count == 0
                ? "none"
                : string.Join(", ", services.Select(x => x.Id));
            throw new InvalidShippingSelectionException(
                $"Service '{selection.ServiceId}' is not available from '{selection.CarrierAlias}' " +
                $"(account '{selection.AccountReference}', store '{request.StoreAlias}', " +
                $"country '{request.CountryCode}', postal code '{request.PostalCode ?? "(none)"}'). " +
                $"Available services: {available}.");
        }

        if (!service.RequiresPickupLocation)
        {
            return new ValidatedShippingSelection(selection with { PickupLocationId = null }, service, null);
        }

        if (string.IsNullOrWhiteSpace(selection.PickupLocationId))
        {
            throw new InvalidShippingSelectionException("A pickup location is required for this service.");
        }

        var locations = await carrier.GetPickupLocationsAsync(
            selection.AccountReference,
            service.Id,
            request,
            cancellationToken).ConfigureAwait(false);
        var location = locations.FirstOrDefault(x =>
            string.Equals(x.Id, selection.PickupLocationId, StringComparison.Ordinal));

        if (location is null)
        {
            throw new InvalidShippingSelectionException(
                $"Pickup location '{selection.PickupLocationId}' is not available.");
        }

        return new ValidatedShippingSelection(selection, service, location);
    }
}

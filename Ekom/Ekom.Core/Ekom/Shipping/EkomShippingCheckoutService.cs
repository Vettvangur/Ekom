using Ekom.Models;

namespace Ekom.Shipping;

internal sealed class EkomShippingCheckoutService : IEkomShippingCheckoutService
{
    private readonly IShippingCarrierRegistry _registry;
    private readonly IShippingSelectionValidator _validator;

    public EkomShippingCheckoutService(
        IShippingCarrierRegistry registry,
        IShippingSelectionValidator validator)
    {
        _registry = registry;
        _validator = validator;
    }

    public async Task<IReadOnlyList<ShippingService>> GetServicesAsync(
        IShippingProvider provider,
        ShippingLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        var configuration = EkomShippingMethodConfiguration.FromProvider(provider);
        if (configuration is null)
        {
            return Array.Empty<ShippingService>();
        }

        var carrier = _registry.GetRequired(configuration.CarrierAlias);
        var services = await carrier.GetServicesAsync(
            configuration.AccountReference,
            request,
            cancellationToken).ConfigureAwait(false);
        return services
            .Where(x => string.Equals(x.Id, configuration.ServiceId, StringComparison.Ordinal))
            .ToArray();
    }

    public async Task<IReadOnlyList<PickupLocation>> GetPickupLocationsAsync(
        IShippingProvider provider,
        ShippingLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        var configuration = EkomShippingMethodConfiguration.FromProvider(provider);
        if (configuration is null)
        {
            return Array.Empty<PickupLocation>();
        }

        var carrier = _registry.GetRequired(configuration.CarrierAlias);
        return await carrier.GetPickupLocationsAsync(
            configuration.AccountReference,
            configuration.ServiceId,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<ValidatedShippingSelection> ValidateAsync(
        IShippingProvider provider,
        string? pickupLocationId,
        ShippingLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        var configuration = EkomShippingMethodConfiguration.FromProvider(provider)
            ?? throw new ShippingConfigurationException(
                $"Ekom shipping provider '{provider.Key}' is not connected to a carrier.");
        var selection = new ShippingSelection(
            configuration.CarrierAlias,
            configuration.AccountReference,
            configuration.ServiceId,
            pickupLocationId);

        return _validator.ValidateAsync(selection, request, cancellationToken);
    }

    public Dictionary<string, string> BuildOrderData(
        ValidatedShippingSelection selection,
        IReadOnlyDictionary<string, string>? existingData = null)
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (existingData is not null)
        {
            foreach (var item in existingData)
            {
                data[item.Key] = item.Value;
            }
        }

        RemoveReservedShippingData(data);
        data[EkomShippingPropertyAliases.CustomCarrierAlias] = selection.Selection.CarrierAlias;
        data[EkomShippingPropertyAliases.CustomAccountReference] = selection.Selection.AccountReference;
        data[EkomShippingPropertyAliases.CustomServiceId] = selection.Service.Id;
        data[EkomShippingPropertyAliases.CustomServiceName] = selection.Service.Name;

        if (selection.PickupLocation is { } location)
        {
            data[EkomShippingPropertyAliases.CustomPickupLocationId] = location.Id;
            data[EkomShippingPropertyAliases.CustomPickupLocationName] = location.Name;
            data[EkomShippingPropertyAliases.CustomPickupLocationAddress] = location.Address;
            data[EkomShippingPropertyAliases.CustomPickupLocationPostalCode] = location.PostalCode;
            data[EkomShippingPropertyAliases.CustomPickupLocationCity] = location.City;
            if (!string.IsNullOrWhiteSpace(location.ExternalId))
            {
                data[EkomShippingPropertyAliases.CustomPickupLocationExternalId] = location.ExternalId;
            }
        }

        return data;
    }

    public async Task<IOrderInfo> SaveSelectionAsync(
        IShippingProvider provider,
        string? pickupLocationId,
        ShippingLookupRequest request,
        IReadOnlyDictionary<string, string>? existingData = null,
        CancellationToken cancellationToken = default)
    {
        var selection = await ValidateAsync(
            provider,
            pickupLocationId,
            request,
            cancellationToken).ConfigureAwait(false);
        var data = BuildOrderData(selection, existingData);

        return await global::Ekom.API.Order.Instance.UpdateShippingInformationAsync(
            provider.Key,
            request.StoreAlias,
            data,
            ct: cancellationToken).ConfigureAwait(false);
    }

    private static void RemoveReservedShippingData(Dictionary<string, string> data)
    {
        data.Remove(EkomShippingPropertyAliases.CustomCarrierAlias);
        data.Remove(EkomShippingPropertyAliases.CustomAccountReference);
        data.Remove(EkomShippingPropertyAliases.CustomServiceId);
        data.Remove(EkomShippingPropertyAliases.CustomServiceName);
        data.Remove(EkomShippingPropertyAliases.CustomPickupLocationId);
        data.Remove(EkomShippingPropertyAliases.CustomPickupLocationExternalId);
        data.Remove(EkomShippingPropertyAliases.CustomPickupLocationName);
        data.Remove(EkomShippingPropertyAliases.CustomPickupLocationAddress);
        data.Remove(EkomShippingPropertyAliases.CustomPickupLocationPostalCode);
        data.Remove(EkomShippingPropertyAliases.CustomPickupLocationCity);
    }
}

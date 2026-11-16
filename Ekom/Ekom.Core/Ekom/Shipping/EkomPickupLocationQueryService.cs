using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Utilities;

namespace Ekom.Shipping;

internal sealed class EkomPickupLocationQueryService : IEkomPickupLocationQueryService
{
    private readonly ControllerRequestHelper _requestHelper;
    private readonly IEkomShippingCheckoutService _checkoutService;

    public EkomPickupLocationQueryService(
        ControllerRequestHelper requestHelper,
        IEkomShippingCheckoutService checkoutService)
    {
        _requestHelper = requestHelper;
        _checkoutService = checkoutService;
    }

    public async Task<IReadOnlyList<PickupLocation>?> GetAsync(
        Guid providerKey,
        string? storeAlias,
        string countryCode,
        string? postalCode,
        CancellationToken cancellationToken = default)
    {
        IStore? store;
        try
        {
            store = string.IsNullOrWhiteSpace(storeAlias)
                ? global::Ekom.API.Store.Instance.GetStore()
                : global::Ekom.API.Store.Instance.GetStore(storeAlias);
        }
        catch (StoreNotFoundException)
        {
            return null;
        }

        if (store is null)
        {
            return null;
        }

        _requestHelper.SetEkmRequest(storeAlias: store.Alias);
        var providers = await global::Ekom.API.Providers.Instance.GetShippingProvidersAsync(
            store.Alias,
            countryCode,
            ct: cancellationToken).ConfigureAwait(false);
        var provider = providers.FirstOrDefault(x => x.Key == providerKey);
        if (provider is null)
        {
            return null;
        }

        return await _checkoutService.GetPickupLocationsAsync(
            provider,
            new ShippingLookupRequest(store.Alias, countryCode, postalCode),
            cancellationToken).ConfigureAwait(false);
    }
}

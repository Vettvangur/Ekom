using Ekom.Models;

namespace Ekom.Shipping;

public sealed record EkomShippingMethodConfiguration(
    string CarrierAlias,
    string AccountReference,
    string ServiceId)
{
    public static EkomShippingMethodConfiguration? FromProvider(IShippingProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return FromProperties(provider.Key, provider.Properties);
    }

    public static EkomShippingMethodConfiguration? FromProperties(
        Guid providerKey,
        IReadOnlyDictionary<string, string> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        properties.TryGetValue(EkomShippingPropertyAliases.CarrierAlias, out var carrierAlias);
        properties.TryGetValue(EkomShippingPropertyAliases.AccountReference, out var accountReference);
        properties.TryGetValue(EkomShippingPropertyAliases.ServiceId, out var serviceId);

        if (string.IsNullOrWhiteSpace(carrierAlias) &&
            string.IsNullOrWhiteSpace(accountReference) &&
            string.IsNullOrWhiteSpace(serviceId))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(carrierAlias) ||
            string.IsNullOrWhiteSpace(accountReference) ||
            string.IsNullOrWhiteSpace(serviceId))
        {
            throw new ShippingConfigurationException(
                $"Ekom shipping provider '{providerKey}' has incomplete carrier configuration.");
        }

        return new EkomShippingMethodConfiguration(carrierAlias, accountReference, serviceId);
    }
}

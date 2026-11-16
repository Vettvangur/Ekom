namespace Ekom.Shipping;

internal sealed class ShippingFulfillmentCarrierRegistry : IShippingFulfillmentCarrierRegistry
{
    private readonly IReadOnlyDictionary<string, IShippingFulfillmentCarrier> _carriers;

    public ShippingFulfillmentCarrierRegistry(IEnumerable<IShippingFulfillmentCarrier> carriers)
    {
        var carrierList = carriers.ToArray();
        var duplicate = carrierList
            .GroupBy(x => x.Alias, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"More than one fulfillment carrier uses alias '{duplicate.Key}'.");
        }

        _carriers = carrierList.ToDictionary(x => x.Alias, StringComparer.OrdinalIgnoreCase);
        Carriers = carrierList;
    }

    public IReadOnlyCollection<IShippingFulfillmentCarrier> Carriers { get; }

    public IShippingFulfillmentCarrier GetRequired(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new ArgumentException("A carrier alias is required.", nameof(alias));
        }

        return _carriers.TryGetValue(alias, out var carrier)
            ? carrier
            : throw new ShippingConfigurationException(
                $"Shipping carrier '{alias}' does not support fulfillment.");
    }
}

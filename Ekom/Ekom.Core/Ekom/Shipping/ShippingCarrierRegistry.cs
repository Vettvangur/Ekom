namespace Ekom.Shipping;

internal sealed class ShippingCarrierRegistry : IShippingCarrierRegistry
{
    private readonly IReadOnlyDictionary<string, IShippingCarrier> _carriers;

    public ShippingCarrierRegistry(IEnumerable<IShippingCarrier> carriers)
    {
        var carrierList = carriers.ToArray();
        var duplicate = carrierList
            .GroupBy(x => x.Alias, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException($"More than one shipping carrier uses alias '{duplicate.Key}'.");
        }

        _carriers = carrierList.ToDictionary(x => x.Alias, StringComparer.OrdinalIgnoreCase);
        Carriers = carrierList;
    }

    public IReadOnlyCollection<IShippingCarrier> Carriers { get; }

    public IShippingCarrier GetRequired(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new ArgumentException("A carrier alias is required.", nameof(alias));
        }

        return _carriers.TryGetValue(alias, out var carrier)
            ? carrier
            : throw new ShippingConfigurationException($"Shipping carrier '{alias}' is not registered.");
    }
}

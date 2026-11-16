namespace Ekom.Shipping;

public interface IShippingFulfillmentCarrierRegistry
{
    IReadOnlyCollection<IShippingFulfillmentCarrier> Carriers { get; }

    IShippingFulfillmentCarrier GetRequired(string alias);
}

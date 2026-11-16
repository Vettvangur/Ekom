namespace Ekom.Shipping;

public interface IShippingCarrierRegistry
{
    IReadOnlyCollection<IShippingCarrier> Carriers { get; }

    IShippingCarrier GetRequired(string alias);
}

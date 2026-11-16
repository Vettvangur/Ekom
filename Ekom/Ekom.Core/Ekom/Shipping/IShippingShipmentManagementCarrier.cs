namespace Ekom.Shipping;

public interface IShippingShipmentLookupCarrier
{
    Task<ShippingShipmentDocument> GetShipmentAsync(
        string accountReference,
        string shipmentId,
        CancellationToken cancellationToken = default);
}

public interface IShippingShipmentDeletionCarrier
{
    Task DeleteShipmentAsync(
        string accountReference,
        string shipmentId,
        CancellationToken cancellationToken = default);
}

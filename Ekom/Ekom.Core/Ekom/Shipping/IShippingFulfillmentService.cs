using Ekom.Models;

namespace Ekom.Shipping;

public interface IShippingFulfillmentService
{
    Task<ShippingFulfillmentRecord?> CreateAutomaticAsync(
        IOrderInfo order,
        CancellationToken cancellationToken = default);

    Task<ShippingFulfillmentRecord> CreateAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<ShippingFulfillmentRecord> RetryAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<ShippingFulfillmentRecord?> GetAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<ShippingLabel> GetLabelAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<ShippingShipmentDocument> GetShipmentAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<ShippingFulfillmentRecord> DeleteAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
}

public interface IShippingAutomationRule
{
    Task<bool> CanCreateShipmentAsync(
        IOrderInfo order,
        CancellationToken cancellationToken = default);
}

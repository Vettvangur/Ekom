using Ekom.Models;

namespace Ekom.Shipping;

/// <summary>Loads the order a shipment is booked for.</summary>
internal interface IShippingOrderReader
{
    Task<IOrderInfo?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
}

internal sealed class ShippingOrderReader : IShippingOrderReader
{
    private readonly API.Order _orders;

    public ShippingOrderReader(API.Order orders)
    {
        _orders = orders;
    }

    public Task<IOrderInfo?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
        => _orders.GetOrderAsync(orderId, cancellationToken);
}

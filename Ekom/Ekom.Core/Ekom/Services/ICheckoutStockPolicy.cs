using Ekom.Models;

namespace Ekom.Services;

/// <summary>Shared inventory policy for checkout preparation and completion.</summary>
/// <remarks>Use durable order/customer data: completion can run without an HTTP request.</remarks>
public interface ICheckoutStockPolicy
{
    bool RequiresStock(IOrderInfo order, IOrderLine line);
}

internal sealed class DefaultCheckoutStockPolicy : ICheckoutStockPolicy
{
    public bool RequiresStock(IOrderInfo order, IOrderLine line) => !line.Product.Backorder;
}

using Ekom.Models;

namespace Ekom.Services;

/// <summary>
/// Decides which order lines need stock. Ekom asks it when a line is added to the basket or its quantity
/// changes, when checkout is prepared, and when the order is completed.
/// </summary>
/// <remarks>
/// Use data saved on the order: completion can run without an HTTP request. A basket may not have
/// customer details yet, so decide what the answer is for a customer the order does not identify.
/// </remarks>
public interface ICheckoutStockPolicy
{
    bool RequiresStock(IOrderInfo order, IOrderLine line);
}

internal sealed class DefaultCheckoutStockPolicy : ICheckoutStockPolicy
{
    public bool RequiresStock(IOrderInfo order, IOrderLine line) => !Utilities.StockBufferHelper.AllowsBackorder(line);
}

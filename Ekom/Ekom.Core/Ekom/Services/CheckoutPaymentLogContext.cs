using Ekom.Models;
using Microsoft.Extensions.Logging;

namespace Ekom.Services;

internal sealed class CheckoutPaymentLogContext
{
    private string? _orderNumber;
    private Guid? _orderUniqueId;

    internal void Capture(IOrderInfo order)
    {
        _orderNumber = order.OrderNumber;
        _orderUniqueId = order.UniqueId;
    }

    internal void LogError(ILogger logger, Exception exception)
    {
        logger.LogError(exception,
            "Checkout payment failed! Error: {ErrorMessage}. Order number: {OrderNumber}. UniqueId: {OrderUniqueId}",
            exception.Message, _orderNumber, _orderUniqueId);
    }
}

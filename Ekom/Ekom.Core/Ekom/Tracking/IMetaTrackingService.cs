using Ekom.Models;

namespace Ekom.Tracking;

public interface IMetaTrackingService
{
    MetaPurchaseRequest CreatePurchaseRequest(IOrderInfo orderInfo);
    MetaPurchaseRequest CreateAddedToCartRequest(IOrderInfo orderInfo, IOrderLine orderLine);
    MetaPurchaseRequest CreateRemovedFromCartRequest(IOrderInfo orderInfo, IOrderLine orderLine);
    MetaPurchaseRequest CreateStartedCheckoutRequest(IOrderInfo orderInfo);
    MetaPurchaseRequest CreateAddedShippingInfoRequest(IOrderInfo orderInfo);
    MetaPurchaseRequest CreateAddedPaymentInfoRequest(IOrderInfo orderInfo);
    Task SendPurchaseAsync(MetaPurchaseRequest request, CancellationToken ct = default);
}

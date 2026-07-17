using Ekom.Models;

namespace Ekom.Tracking;

public interface IGa4TrackingService
{
    Ga4PurchaseRequest CreatePurchaseRequest(IOrderInfo orderInfo);
    Ga4PurchaseRequest CreateAddedToCartRequest(IOrderInfo orderInfo, IOrderLine orderLine);
    Ga4PurchaseRequest CreateRemovedFromCartRequest(IOrderInfo orderInfo, IOrderLine orderLine);
    Ga4PurchaseRequest CreateStartedCheckoutRequest(IOrderInfo orderInfo);
    Ga4PurchaseRequest CreateAddedShippingInfoRequest(IOrderInfo orderInfo);
    Ga4PurchaseRequest CreateAddedPaymentInfoRequest(IOrderInfo orderInfo);
    Task SendPurchaseAsync(Ga4PurchaseRequest request, CancellationToken ct = default);
}

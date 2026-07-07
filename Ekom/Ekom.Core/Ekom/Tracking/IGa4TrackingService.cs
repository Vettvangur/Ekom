using Ekom.Models;

namespace Ekom.Tracking;

public interface IGa4TrackingService
{
    Ga4PurchaseRequest CreatePurchaseRequest(IOrderInfo orderInfo);
    Task SendPurchaseAsync(Ga4PurchaseRequest request, CancellationToken ct = default);
}

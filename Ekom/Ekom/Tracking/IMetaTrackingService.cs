using Ekom.Models;

namespace Ekom.Tracking;

public interface IMetaTrackingService
{
    MetaPurchaseRequest CreatePurchaseRequest(IOrderInfo orderInfo);
    Task SendPurchaseAsync(MetaPurchaseRequest request, CancellationToken ct = default);
}

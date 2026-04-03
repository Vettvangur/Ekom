using Ekom.Models;

namespace Ekom.Tracking;

public interface IOrderTrackingService
{
    OrderTracking? ResolveTracking(OrderTracking? manualTracking);
    OrderConsent? ResolveConsent(OrderConsent? manualConsent, string? storeAlias = null);
    void ApplyTracking(OrderInfo orderInfo, OrderTracking tracking, bool replaceExisting);
    void ApplyConsent(OrderInfo orderInfo, OrderConsent consent, bool replaceExisting);
    void ValidateManualReplacement(IOrderInfo orderInfo);
}

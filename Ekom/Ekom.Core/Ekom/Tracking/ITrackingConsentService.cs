using Ekom.Models;
using Microsoft.AspNetCore.Http;

namespace Ekom.Tracking;

public interface ITrackingConsentService
{
    OrderConsent GetConsent(HttpContext httpContext, string? storeAlias = null);
    bool CanCaptureAnalytics(OrderConsent? consent);
    bool CanCaptureMarketing(OrderConsent? consent);
}

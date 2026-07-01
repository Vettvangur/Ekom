using Ekom.Models;
using Microsoft.AspNetCore.Http;

namespace Ekom.Tracking;

public interface IPreConsentTrackingSessionService
{
    OrderTracking? Read(HttpContext httpContext);
    void WriteFirstTouch(HttpContext httpContext, OrderTracking tracking);
    void Clear(HttpContext httpContext);
}

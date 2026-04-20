using Ekom.Models;
using Microsoft.AspNetCore.Http;

namespace Ekom.Tracking;

public interface ITrackingCookieService
{
    OrderTracking? ReadCookie(HttpContext httpContext);
    void WriteCookie(HttpContext httpContext, OrderTracking tracking);
    OrderTracking? CaptureFromRequest(HttpContext httpContext);
    OrderTracking? CaptureAttributionFromRequest(HttpContext httpContext);
}

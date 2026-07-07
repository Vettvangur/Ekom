using Ekom.Models;
using Microsoft.AspNetCore.Http;

namespace Ekom.Tracking;

public interface ITrackingConsentResolver
{
    int Order { get; }
    OrderConsent? Resolve(HttpContext httpContext, string? storeAlias, TrackingConsentOptions options);
}

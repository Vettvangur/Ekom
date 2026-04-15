using Ekom.Models;
using Microsoft.AspNetCore.Http;

namespace Ekom.Tracking;

public sealed class DefaultTrackingConsentResolver : ITrackingConsentResolver
{
    public int Order => 1000;

    public OrderConsent? Resolve(HttpContext httpContext, string? storeAlias, TrackingConsentOptions options)
    {
        bool? analytics = ResolveConsent(httpContext, options.AnalyticsHeaderName, options.AnalyticsCookieName);
        bool? marketing = ResolveConsent(httpContext, options.MarketingHeaderName, options.MarketingCookieName);

        if (!analytics.HasValue && !marketing.HasValue)
        {
            return null;
        }

        return new OrderConsent
        {
            Analytics = analytics,
            Marketing = marketing,
            ResolvedAtUtc = DateTime.UtcNow,
            Source = "request"
        };
    }

    private static bool? ResolveConsent(HttpContext httpContext, string? headerName, string? cookieName)
    {
        string? value = null;

        if (!string.IsNullOrWhiteSpace(headerName)
            && httpContext.Request.Headers.TryGetValue(headerName, out var headerValues))
        {
            value = headerValues.ToString();
        }

        if (string.IsNullOrWhiteSpace(value)
            && !string.IsNullOrWhiteSpace(cookieName)
            && httpContext.Request.Cookies.TryGetValue(cookieName, out var cookieValue))
        {
            value = cookieValue;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (bool.TryParse(value, out var boolResult))
        {
            return boolResult;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "1" => true,
            "0" => false,
            "yes" => true,
            "no" => false,
            "granted" => true,
            "denied" => false,
            _ => null
        };
    }
}

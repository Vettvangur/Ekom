using Ekom.Models;
using Ekom.API;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Json;

using StoreApi = Ekom.API.Store;

namespace Ekom.Tracking;

public sealed class TrackingCookieService : ITrackingCookieService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IOptions<TrackingOptions> _options;
    private readonly ITrackingConsentService _trackingConsentService;

    public TrackingCookieService(IOptions<TrackingOptions> options, ITrackingConsentService trackingConsentService)
    {
        _options = options;
        _trackingConsentService = trackingConsentService;
    }

    public OrderTracking? ReadCookie(HttpContext httpContext)
    {
        if (!httpContext.Request.Cookies.TryGetValue(_options.Value.CookieName, out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            var tracking = JsonSerializer.Deserialize<OrderTracking>(value, JsonOptions);
            return tracking?.HasData() == true ? tracking : null;
        }
        catch
        {
            return null;
        }
    }

    public void WriteCookie(HttpContext httpContext, OrderTracking tracking)
    {
        httpContext.Response.Cookies.Append(
            _options.Value.CookieName,
            JsonSerializer.Serialize(tracking, JsonOptions),
            new CookieOptions
            {
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddDays(_options.Value.CookieLifetimeDays),
                SameSite = SameSiteMode.Lax,
                Secure = httpContext.Request.IsHttps,
                HttpOnly = false
            });
    }

    public OrderTracking? CaptureFromRequest(HttpContext httpContext)
    {
        var request = httpContext.Request;
        var cookies = request.Cookies;
        var storeAlias = httpContext.RequestServices is null
            ? null
            : httpContext.RequestServices.GetService<StoreApi>()?.GetStore()?.Alias;
        var consent = _trackingConsentService.GetConsent(httpContext, storeAlias);

        if (!_trackingConsentService.CanCaptureAnalytics(consent)
            && !_trackingConsentService.CanCaptureMarketing(consent))
        {
            return null;
        }

        var tracking = CaptureAttributionFromRequest(httpContext);
        if (tracking is null)
        {
            return null;
        }

        tracking.HasCookieSupport = request.Cookies.Count > 0 || request.Headers.ContainsKey("Cookie");
        tracking.CaptureMethod = "cookie";
        tracking.Ga4.ClientId = _trackingConsentService.CanCaptureAnalytics(consent) ? ParseGaClientId(cookies["_ga"]) : null;
        tracking.Ga4.SessionId = _trackingConsentService.CanCaptureAnalytics(consent) ? ParseGaSessionId(cookies) : null;
        tracking.Meta.Fbp = _trackingConsentService.CanCaptureMarketing(consent) ? ValueOrNull(cookies["_fbp"]) : null;
        tracking.Meta.Fbc = _trackingConsentService.CanCaptureMarketing(consent) ? ValueOrNull(cookies["_fbc"]) ?? BuildFbc(ValueOrNull(request.Query["fbclid"])) : null;

        return tracking.HasData() ? tracking : null;
    }

    public OrderTracking? CaptureAttributionFromRequest(HttpContext httpContext)
    {
        var request = httpContext.Request;
        var query = request.Query;

        var fbclid = ValueOrNull(query["fbclid"]);
        var gclid = ValueOrNull(query["gclid"]);

        var tracking = new OrderTracking
        {
            CapturedAtUtc = DateTime.UtcNow,
            Source = ValueOrNull(query["utm_source"]),
            Medium = ValueOrNull(query["utm_medium"]),
            Campaign = ValueOrNull(query["utm_campaign"]),
            Term = ValueOrNull(query["utm_term"]),
            Content = ValueOrNull(query["utm_content"]),
            ClickId = gclid ?? fbclid,
            ClickIdType = !string.IsNullOrWhiteSpace(gclid) ? "gclid" : !string.IsNullOrWhiteSpace(fbclid) ? "fbclid" : null,
            LandingUrl = request.GetEncodedUrl(),
            Referrer = ValueOrNull(request.Headers.Referer.ToString()),
            Ga4 = new Ga4OrderTracking
            {
                Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            },
            Meta = new MetaOrderTracking
            {
                Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            }
        };

        return tracking.HasData() ? tracking : null;
    }

    private static string? ParseGaClientId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var parts = value.Split('.');
        return parts.Length >= 4 ? $"{parts[2]}.{parts[3]}" : null;
    }

    private static string? ParseGaSessionId(IRequestCookieCollection cookies)
    {
        var gaCookie = cookies.FirstOrDefault(x => x.Key.StartsWith("_ga_", StringComparison.Ordinal) && x.Value.StartsWith("GS", StringComparison.Ordinal)).Value;
        if (string.IsNullOrWhiteSpace(gaCookie))
            return null;

        var parts = gaCookie.Split('.');
        return parts.Length >= 3 ? ValueOrNull(parts[2]) : null;
    }

    private static string? BuildFbc(string? fbclid)
    {
        if (string.IsNullOrWhiteSpace(fbclid))
            return null;

        return $"fb.1.{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.{fbclid.Trim()}";
    }

    private static string? ValueOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

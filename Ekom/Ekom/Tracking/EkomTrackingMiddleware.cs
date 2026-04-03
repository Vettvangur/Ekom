using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Ekom.Tracking;

public sealed class EkomTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptions<TrackingOptions> _options;
    private readonly ITrackingCookieService _trackingCookieService;

    public EkomTrackingMiddleware(
        RequestDelegate next,
        IOptions<TrackingOptions> options,
        ITrackingCookieService trackingCookieService)
    {
        _next = next;
        _options = options;
        _trackingCookieService = trackingCookieService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_options.Value.Enabled && _options.Value.CaptureEnabled)
        {
            var captured = _trackingCookieService.CaptureFromRequest(context);
            if (captured is not null)
            {
                var existing = _trackingCookieService.ReadCookie(context);
                _trackingCookieService.WriteCookie(context, Merge(existing, captured));
            }
        }

        await _next(context).ConfigureAwait(false);
    }

    private static Models.OrderTracking Merge(Models.OrderTracking? existing, Models.OrderTracking incoming)
    {
        if (existing is null)
            return incoming;

        var merged = existing.Clone();
        merged.CapturedAtUtc = incoming.CapturedAtUtc ?? merged.CapturedAtUtc;
        merged.Source = incoming.Source ?? merged.Source;
        merged.Medium = incoming.Medium ?? merged.Medium;
        merged.Campaign = incoming.Campaign ?? merged.Campaign;
        merged.Term = incoming.Term ?? merged.Term;
        merged.Content = incoming.Content ?? merged.Content;
        merged.ClickId = incoming.ClickId ?? merged.ClickId;
        merged.ClickIdType = incoming.ClickIdType ?? merged.ClickIdType;
        merged.LandingUrl = incoming.LandingUrl ?? merged.LandingUrl;
        merged.Referrer = incoming.Referrer ?? merged.Referrer;
        merged.HasCookieSupport = incoming.HasCookieSupport ?? merged.HasCookieSupport;
        merged.CaptureMethod = incoming.CaptureMethod ?? merged.CaptureMethod;
        merged.Ga4.ClientId = incoming.Ga4.ClientId ?? merged.Ga4.ClientId;
        merged.Ga4.SessionId = incoming.Ga4.SessionId ?? merged.Ga4.SessionId;
        foreach (var item in incoming.Ga4.Data)
            merged.Ga4.Data[item.Key] = item.Value;

        merged.Meta.Fbp = incoming.Meta.Fbp ?? merged.Meta.Fbp;
        merged.Meta.Fbc = incoming.Meta.Fbc ?? merged.Meta.Fbc;
        foreach (var item in incoming.Meta.Data)
            merged.Meta.Data[item.Key] = item.Value;

        return merged;
    }
}

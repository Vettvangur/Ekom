using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.IO;

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
        var captured = _options.Value.Enabled && _options.Value.CaptureEnabled && IsEligibleRequest(context.Request)
            ? _trackingCookieService.CaptureFromRequest(context)
            : null;

        if (captured is not null)
        {
            context.Response.OnStarting(static state =>
            {
                var (httpContext, trackingCookieService, tracking) = ((HttpContext, ITrackingCookieService, Models.OrderTracking))state;

                if (ShouldPersistTracking(httpContext))
                {
                    var existing = trackingCookieService.ReadCookie(httpContext);
                    trackingCookieService.WriteCookie(httpContext, Merge(existing, tracking));
                }

                return Task.CompletedTask;
            }, (context, _trackingCookieService, captured));
        }

        await _next(context).ConfigureAwait(false);
    }

    private static bool IsEligibleRequest(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
        {
            return false;
        }

        if (request.Path.StartsWithSegments("/umbraco", StringComparison.OrdinalIgnoreCase)
            || request.Path.StartsWithSegments("/ekom", StringComparison.OrdinalIgnoreCase)
            || request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            || request.Path.StartsWithSegments("/webapi", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !Path.HasExtension(request.Path.Value);
    }

    private static bool ShouldPersistTracking(HttpContext context)
    {
        var contentType = context.Response.ContentType;
        return !string.IsNullOrWhiteSpace(contentType)
            && contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase);
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

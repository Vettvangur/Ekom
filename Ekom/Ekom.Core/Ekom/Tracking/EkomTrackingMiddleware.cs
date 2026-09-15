using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.IO;
using StoreApi = Ekom.API.Store;

namespace Ekom.Tracking;

public sealed class EkomTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptions<TrackingOptions> _options;
    private readonly ITrackingConsentService _trackingConsentService;
    private readonly ITrackingCookieService _trackingCookieService;
    private readonly IPreConsentTrackingSessionService _preConsentTrackingSessionService;

    public EkomTrackingMiddleware(
        RequestDelegate next,
        IOptions<TrackingOptions> options,
        ITrackingConsentService trackingConsentService,
        ITrackingCookieService trackingCookieService,
        IPreConsentTrackingSessionService preConsentTrackingSessionService)
    {
        _next = next;
        _options = options;
        _trackingConsentService = trackingConsentService;
        _trackingCookieService = trackingCookieService;
        _preConsentTrackingSessionService = preConsentTrackingSessionService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        Models.OrderTracking? captured = null;
        var isEligibleTrackingRequest = false;
        var canCaptureTracking = false;
        var canCaptureMarketing = false;

        if (_options.Value.Enabled && _options.Value.CaptureEnabled && IsEligibleRequest(context.Request))
        {
            isEligibleTrackingRequest = true;
            var storeAlias = context.RequestServices is null
                ? null
                : context.RequestServices.GetService<StoreApi>()?.GetStore()?.Alias;
            var consent = _trackingConsentService.GetConsent(context, storeAlias);
            canCaptureMarketing = _trackingConsentService.CanCaptureMarketing(consent);
            canCaptureTracking = _trackingConsentService.CanCaptureAnalytics(consent)
                || canCaptureMarketing;

            if (canCaptureTracking)
            {
                captured = _trackingCookieService.CaptureFromRequest(context);
                if (!canCaptureMarketing)
                {
                    var mailchimpTracking = ExtractMailchimpTracking(
                        _trackingCookieService.CaptureAttributionFromRequest(context));
                    if (mailchimpTracking is not null)
                    {
                        _preConsentTrackingSessionService.WriteFirstTouch(context, mailchimpTracking);
                    }
                }
            }
            else
            {
                var preConsentTracking = _trackingCookieService.CaptureAttributionFromRequest(context);
                if (preConsentTracking is not null)
                {
                    _preConsentTrackingSessionService.WriteFirstTouch(context, preConsentTracking);
                }
            }
        }

        if (isEligibleTrackingRequest)
        {
            context.Response.OnStarting(static state =>
            {
                var (httpContext, trackingCookieService, preConsentTrackingSessionService, tracking, captureAllowed, marketingAllowed) = ((HttpContext, ITrackingCookieService, IPreConsentTrackingSessionService, Models.OrderTracking?, bool, bool))state;

                if (ShouldPersistTracking(httpContext))
                {
                    var preConsentTracking = preConsentTrackingSessionService.Read(httpContext);
                    var existing = trackingCookieService.ReadCookie(httpContext);
                    var existingMailchimpTracking = ExtractMailchimpTracking(existing);

                    if (!captureAllowed)
                    {
                        if (existingMailchimpTracking is not null && existing is not null)
                        {
                            var retainedTracking = preConsentTracking?.Clone() ?? new Models.OrderTracking();
                            retainedTracking.Mailchimp = existingMailchimpTracking.Mailchimp.Clone();
                            preConsentTrackingSessionService.Clear(httpContext);
                            preConsentTrackingSessionService.WriteFirstTouch(httpContext, retainedTracking);

                            var sanitizedExisting = existing.Clone();
                            sanitizedExisting.Mailchimp = new Models.MailchimpOrderTracking();
                            trackingCookieService.WriteCookie(httpContext, sanitizedExisting);
                        }

                        return Task.CompletedTask;
                    }

                    var merged = tracking;
                    var retainedMailchimpTracking = !marketingAllowed
                        ? existingMailchimpTracking ?? ExtractMailchimpTracking(preConsentTracking)
                        : null;

                    if (preConsentTracking is not null)
                    {
                        var promotableTracking = preConsentTracking;
                        if (!marketingAllowed)
                        {
                            promotableTracking = preConsentTracking.Clone();
                            promotableTracking.Mailchimp = new Models.MailchimpOrderTracking();
                        }

                        if (promotableTracking.HasData())
                        {
                            merged = merged is null
                                ? promotableTracking
                                : Merge(merged, promotableTracking);
                        }
                    }

                    var existingForMerge = existing;
                    if (!marketingAllowed && existingForMerge is not null)
                    {
                        existingForMerge = existingForMerge.Clone();
                        existingForMerge.Mailchimp = new Models.MailchimpOrderTracking();
                    }

                    if (merged is not null || existingMailchimpTracking is not null)
                    {
                        var result = merged is null
                            ? existingForMerge!
                            : Merge(existingForMerge, merged);
                        if (marketingAllowed)
                        {
                            var firstTouchMailchimpTracking = existingMailchimpTracking
                                ?? ExtractMailchimpTracking(preConsentTracking)
                                ?? ExtractMailchimpTracking(tracking);
                            result.Mailchimp = firstTouchMailchimpTracking?.Mailchimp.Clone()
                                ?? new Models.MailchimpOrderTracking();
                        }

                        trackingCookieService.WriteCookie(httpContext, result);
                    }

                    if (preConsentTracking is not null || retainedMailchimpTracking is not null)
                    {
                        preConsentTrackingSessionService.Clear(httpContext);
                        if (retainedMailchimpTracking is not null)
                        {
                            preConsentTrackingSessionService.WriteFirstTouch(httpContext, retainedMailchimpTracking);
                        }
                    }
                }

                return Task.CompletedTask;
            }, (context, _trackingCookieService, _preConsentTrackingSessionService, captured, canCaptureTracking, canCaptureMarketing));
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

        merged.Mailchimp ??= new Models.MailchimpOrderTracking();
        merged.Mailchimp.CampaignId = incoming.Mailchimp?.CampaignId ?? merged.Mailchimp.CampaignId;
        merged.Mailchimp.TrackingCode = incoming.Mailchimp?.TrackingCode ?? merged.Mailchimp.TrackingCode;

        return merged;
    }

    private static Models.OrderTracking? ExtractMailchimpTracking(Models.OrderTracking? tracking)
    {
        if (tracking?.Mailchimp?.HasData() != true)
        {
            return null;
        }

        return new Models.OrderTracking
        {
            CapturedAtUtc = tracking.CapturedAtUtc,
            Mailchimp = tracking.Mailchimp.Clone()
        };
    }
}

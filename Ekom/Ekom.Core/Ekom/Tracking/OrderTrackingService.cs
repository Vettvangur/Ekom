using Ekom.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Ekom.Tracking;

public sealed class OrderTrackingService : IOrderTrackingService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITrackingCookieService _trackingCookieService;
    private readonly ITrackingConsentService _trackingConsentService;
    private readonly IOptions<TrackingOptions> _options;

    public OrderTrackingService(IHttpContextAccessor httpContextAccessor, ITrackingCookieService trackingCookieService, ITrackingConsentService trackingConsentService, IOptions<TrackingOptions> options)
    {
        _httpContextAccessor = httpContextAccessor;
        _trackingCookieService = trackingCookieService;
        _trackingConsentService = trackingConsentService;
        _options = options;
    }

    public OrderTracking? ResolveTracking(OrderTracking? manualTracking)
    {
        if (manualTracking?.HasData() == true)
            return manualTracking.Clone();

        if (!_options.Value.Enabled || !_options.Value.CaptureEnabled)
            return null;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return null;

        return _trackingCookieService.ReadCookie(httpContext)?.Clone();
    }

    public OrderConsent? ResolveConsent(OrderConsent? manualConsent, string? storeAlias = null)
    {
        if (manualConsent != null)
        {
            return manualConsent.Clone();
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return null;
        }

        return _trackingConsentService.GetConsent(httpContext, storeAlias);
    }

    public void ApplyTracking(OrderInfo orderInfo, OrderTracking tracking, bool replaceExisting)
    {
        if (!replaceExisting && orderInfo.Tracking?.HasData() == true)
        {
            RemoveMailchimpAttributionWithoutConsent(orderInfo);
            return;
        }

        orderInfo.Tracking = tracking.Clone();
        RemoveMailchimpAttributionWithoutConsent(orderInfo);
    }

    public void ApplyConsent(OrderInfo orderInfo, OrderConsent consent, bool replaceExisting)
    {
        if (!replaceExisting && orderInfo.Consent != null)
        {
            RemoveMailchimpAttributionWithoutConsent(orderInfo);
            return;
        }

        orderInfo.Consent = consent.Clone();
        RemoveMailchimpAttributionWithoutConsent(orderInfo);
    }

    private static void RemoveMailchimpAttributionWithoutConsent(OrderInfo orderInfo)
    {
        if (orderInfo.Consent?.Marketing != true && orderInfo.Tracking is not null)
        {
            orderInfo.Tracking.Mailchimp = new MailchimpOrderTracking();
        }
    }

    public void ValidateManualReplacement(IOrderInfo orderInfo)
    {
        if (API.Order.IsOrderFinal(orderInfo.OrderStatus))
            throw new InvalidOperationException("Tracking can only be replaced before order completion.");
    }
}

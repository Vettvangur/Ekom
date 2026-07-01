using Ekom.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Ekom.Tracking;

public sealed class TrackingConsentService : ITrackingConsentService
{
    private readonly IOptions<TrackingOptions> _options;
    private readonly IEnumerable<ITrackingConsentResolver> _resolvers;

    public TrackingConsentService(IOptions<TrackingOptions> options, IEnumerable<ITrackingConsentResolver> resolvers)
    {
        _options = options;
        _resolvers = resolvers.OrderBy(x => x.Order).ToArray();
    }

    public OrderConsent GetConsent(HttpContext httpContext, string? storeAlias = null)
    {
        var consentOptions = ResolveOptions(storeAlias);

        foreach (var resolver in _resolvers)
        {
            var consent = resolver.Resolve(httpContext, storeAlias, consentOptions);
            if (consent != null)
            {
                consent.Analytics ??= consentOptions.FallbackAnalyticsConsent;
                consent.Marketing ??= consentOptions.FallbackMarketingConsent;
                consent.ResolvedAtUtc ??= DateTime.UtcNow;
                consent.Source ??= resolver.GetType().Name;
                return consent;
            }
        }

        return new OrderConsent
        {
            Analytics = consentOptions.FallbackAnalyticsConsent,
            Marketing = consentOptions.FallbackMarketingConsent,
            ResolvedAtUtc = DateTime.UtcNow,
            Source = "fallback"
        };
    }

    public bool CanCaptureAnalytics(OrderConsent? consent)
        => consent?.Analytics == true;

    public bool CanCaptureMarketing(OrderConsent? consent)
        => consent?.Marketing == true;

    private TrackingConsentOptions ResolveOptions(string? storeAlias)
    {
        var defaults = _options.Value.Consent;
        if (string.IsNullOrWhiteSpace(storeAlias))
        {
            return Clone(defaults);
        }

        var storeOptions = defaults.Stores.FirstOrDefault(x => x.Alias.Equals(storeAlias, StringComparison.OrdinalIgnoreCase));
        if (storeOptions == null)
        {
            return Clone(defaults);
        }

        var resolved = Clone(defaults);
        resolved.FallbackAnalyticsConsent = storeOptions.FallbackAnalyticsConsent ?? resolved.FallbackAnalyticsConsent;
        resolved.FallbackMarketingConsent = storeOptions.FallbackMarketingConsent ?? resolved.FallbackMarketingConsent;
        resolved.AnalyticsCookieName = storeOptions.AnalyticsCookieName ?? resolved.AnalyticsCookieName;
        resolved.AnalyticsHeaderName = storeOptions.AnalyticsHeaderName ?? resolved.AnalyticsHeaderName;
        resolved.MarketingCookieName = storeOptions.MarketingCookieName ?? resolved.MarketingCookieName;
        resolved.MarketingHeaderName = storeOptions.MarketingHeaderName ?? resolved.MarketingHeaderName;
        return resolved;
    }

    private static TrackingConsentOptions Clone(TrackingConsentOptions source)
        => new()
        {
            FallbackAnalyticsConsent = source.FallbackAnalyticsConsent,
            FallbackMarketingConsent = source.FallbackMarketingConsent,
            AnalyticsCookieName = source.AnalyticsCookieName,
            AnalyticsHeaderName = source.AnalyticsHeaderName,
            MarketingCookieName = source.MarketingCookieName,
            MarketingHeaderName = source.MarketingHeaderName
        };
}

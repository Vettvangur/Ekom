using System.Text.Json.Serialization;

namespace Ekom.Tracking;

public sealed class TrackingOptions
{
    public bool Enabled { get; set; }
    public bool CaptureEnabled { get; set; } = true;
    public bool LogEventData { get; set; }
    public bool LogPurchaseEventData { get; set; }
    public string CookieName { get; set; } = "EkomTracking";
    public double CookieLifetimeDays { get; set; } = 30;
    public string? SiteBaseUrl { get; set; }
    public List<TrackingStoreUrlOptions> Stores { get; set; } = [];
    public TrackingConsentOptions Consent { get; set; } = new();
    public Ga4TrackingProviderOptions Ga4 { get; set; } = new();
    public MetaTrackingProviderOptions Meta { get; set; } = new();

    public bool ShouldLogEventData(string eventName)
        => LogEventData || (LogPurchaseEventData && string.Equals(eventName, "purchase", StringComparison.OrdinalIgnoreCase));
}

public sealed class TrackingStoreUrlOptions
{
    public string Alias { get; set; } = string.Empty;
    public string? SiteBaseUrl { get; set; }
}

public class TrackingConsentOptions
{
    public bool FallbackAnalyticsConsent { get; set; }
    public bool FallbackMarketingConsent { get; set; }
    public string? AnalyticsCookieName { get; set; }
    public string? AnalyticsHeaderName { get; set; }
    public string? MarketingCookieName { get; set; }
    public string? MarketingHeaderName { get; set; }
    public List<TrackingConsentStoreOptions> Stores { get; set; } = [];
}

public sealed class TrackingConsentStoreOptions
{
    public string Alias { get; set; } = string.Empty;
    public bool? FallbackAnalyticsConsent { get; set; }
    public bool? FallbackMarketingConsent { get; set; }
    public string? AnalyticsCookieName { get; set; }
    public string? AnalyticsHeaderName { get; set; }
    public string? MarketingCookieName { get; set; }
    public string? MarketingHeaderName { get; set; }
}

public class TrackingProviderOptions
{
    public bool Enabled { get; set; }
    public bool Testing { get; set; }
    public TrackingDispatchOptions Dispatching { get; set; } = new();
    public List<TrackingStoreOptions> Stores { get; set; } = [];
}

public sealed class Ga4TrackingProviderOptions : TrackingProviderOptions
{
    public Ga4TrackingEventsOptions Events { get; set; } = new();
}

public sealed class Ga4TrackingEventsOptions
{
    public bool AddedToCart { get; set; }
    public bool StartedCheckout { get; set; }
}

public sealed class MetaTrackingProviderOptions : TrackingProviderOptions
{
    public MetaTrackingEventsOptions Events { get; set; } = new();
}

public sealed class MetaTrackingEventsOptions
{
    public bool AddedToCart { get; set; }
    public bool StartedCheckout { get; set; }
}

public sealed class TrackingDispatchOptions
{
    public int Capacity { get; set; } = 1000;
    public int MaxConcurrency { get; set; } = 2;
}

public sealed class TrackingStoreOptions
{
    public string Alias { get; set; } = string.Empty;
    public string? MeasurementId { get; set; }
    public string? ApiSecret { get; set; }
    public string? PixelId { get; set; }
    public string? AccessToken { get; set; }
    public string? TestEventCode { get; set; }
}

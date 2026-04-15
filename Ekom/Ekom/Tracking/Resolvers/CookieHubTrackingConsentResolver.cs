using Ekom.Models;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Ekom.Tracking;

public sealed class CookieHubTrackingConsentResolver : ITrackingConsentResolver
{
    public int Order => 100;

    public OrderConsent? Resolve(HttpContext httpContext, string? storeAlias, TrackingConsentOptions options)
    {
        if (!UsesCookieHub(options))
        {
            return null;
        }

        if (!httpContext.Request.Cookies.TryGetValue("cookiehub", out var rawCookie)
            || string.IsNullOrWhiteSpace(rawCookie))
        {
            return null;
        }

        try
        {
            var decoded = Uri.UnescapeDataString(rawCookie);
            using var document = JsonDocument.Parse(decoded);
            if (!document.RootElement.TryGetProperty("categories", out var categories))
            {
                return null;
            }

            return new OrderConsent
            {
                Analytics = ReadCategory(categories, "analytics"),
                Marketing = ReadCategory(categories, "marketing"),
                ResolvedAtUtc = ReadTimestamp(document.RootElement),
                Source = "cookiehub"
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool UsesCookieHub(TrackingConsentOptions options)
        => string.Equals(options.AnalyticsCookieName, "cookiehub", StringComparison.OrdinalIgnoreCase)
        || string.Equals(options.MarketingCookieName, "cookiehub", StringComparison.OrdinalIgnoreCase);

    private static bool? ReadCategory(JsonElement categories, string propertyName)
    {
        if (!categories.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.True
            ? true
            : property.ValueKind == JsonValueKind.False
                ? false
                : null;
    }

    private static DateTime? ReadTimestamp(JsonElement root)
    {
        if (!root.TryGetProperty("timestamp", out var timestampProperty)
            || timestampProperty.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return DateTime.TryParse(timestampProperty.GetString(), out var timestamp)
            ? timestamp.ToUniversalTime()
            : null;
    }
}

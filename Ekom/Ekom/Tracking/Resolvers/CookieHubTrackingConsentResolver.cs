using Ekom.Models;
using Microsoft.AspNetCore.Http;
using System.Text;
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
            using var document = JsonDocument.Parse(DecodePayload(rawCookie));
            var consent = new OrderConsent
            {
                Analytics = ReadAnalyticsConsent(document.RootElement),
                Marketing = ReadMarketingConsent(document.RootElement),
                ResolvedAtUtc = ReadTimestamp(document.RootElement),
                Source = "cookiehub"
            };

            return consent.Analytics.HasValue || consent.Marketing.HasValue
                ? consent
                : null;
        }
        catch (Exception ex) when (ex is JsonException || ex is FormatException || ex is ArgumentException)
        {
            return null;
        }
    }

    private static bool UsesCookieHub(TrackingConsentOptions options)
        => string.Equals(options.AnalyticsCookieName, "cookiehub", StringComparison.OrdinalIgnoreCase)
        || string.Equals(options.MarketingCookieName, "cookiehub", StringComparison.OrdinalIgnoreCase);

    private static string DecodePayload(string rawCookie)
    {
        var decoded = Uri.UnescapeDataString(rawCookie);
        if (LooksLikeJson(decoded))
        {
            return decoded;
        }

        return Encoding.UTF8.GetString(DecodeBase64(decoded));
    }

    private static byte[] DecodeBase64(string value)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            var normalized = value.Replace('-', '+').Replace('_', '/');
            var padding = normalized.Length % 4;
            if (padding > 0)
            {
                normalized = normalized.PadRight(normalized.Length + (4 - padding), '=');
            }

            return Convert.FromBase64String(normalized);
        }
    }

    private static bool LooksLikeJson(string value)
    {
        var trimmed = value.TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal)
            || trimmed.StartsWith("[", StringComparison.Ordinal);
    }

    private static bool? ReadAnalyticsConsent(JsonElement root)
        => ReadCategoryConsent(root, "analytics", 3);

    private static bool? ReadMarketingConsent(JsonElement root)
        => ReadCategoryConsent(root, "marketing", 4);

    private static bool? ReadCategoryConsent(JsonElement root, string propertyName, int categoryId)
    {
        if (TryReadAllAllowed(root, out var allAllowed) && allAllowed)
        {
            return true;
        }

        if (!root.TryGetProperty("categories", out var categories))
        {
            return null;
        }

        return categories.ValueKind switch
        {
            JsonValueKind.Object => ReadNamedCategory(categories, propertyName),
            JsonValueKind.Array => ReadCategoryId(categories, categoryId),
            _ => null
        };
    }

    private static bool TryReadAllAllowed(JsonElement root, out bool allAllowed)
    {
        if (root.TryGetProperty("allAllowed", out var property))
        {
            if (property.ValueKind == JsonValueKind.True)
            {
                allAllowed = true;
                return true;
            }

            if (property.ValueKind == JsonValueKind.False)
            {
                allAllowed = false;
                return true;
            }
        }

        allAllowed = false;
        return false;
    }

    private static bool? ReadNamedCategory(JsonElement categories, string propertyName)
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

    private static bool ReadCategoryId(JsonElement categories, int categoryId)
    {
        foreach (var item in categories.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var value) && value == categoryId)
            {
                return true;
            }
        }

        return false;
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

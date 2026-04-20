using Ekom.Models;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Ekom.Tracking;

public sealed class PreConsentTrackingSessionService : IPreConsentTrackingSessionService
{
    private const string SessionKey = "EkomTrackingPreConsent";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public OrderTracking? Read(HttpContext httpContext)
    {
        var value = httpContext.Session.GetString(SessionKey);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            var tracking = JsonSerializer.Deserialize<OrderTracking>(value, JsonOptions);
            return tracking?.HasData() == true ? tracking : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void WriteFirstTouch(HttpContext httpContext, OrderTracking tracking)
    {
        if (tracking.HasData() != true || Read(httpContext) is not null)
        {
            return;
        }

        httpContext.Session.SetString(SessionKey, JsonSerializer.Serialize(tracking, JsonOptions));
    }

    public void Clear(HttpContext httpContext)
        => httpContext.Session.Remove(SessionKey);
}

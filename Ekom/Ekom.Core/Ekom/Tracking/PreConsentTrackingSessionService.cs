using Ekom.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Text.Json;

namespace Ekom.Tracking;

public sealed class PreConsentTrackingSessionService : IPreConsentTrackingSessionService
{
    private const string SessionKey = "EkomTrackingPreConsent";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public OrderTracking? Read(HttpContext httpContext)
    {
        var session = GetSession(httpContext);
        if (session is null)
        {
            return null;
        }

        var value = session.GetString(SessionKey);
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
        var session = GetSession(httpContext);
        if (session is null || tracking.HasData() != true || Read(httpContext) is not null)
        {
            return;
        }

        session.SetString(SessionKey, JsonSerializer.Serialize(tracking, JsonOptions));
    }

    public void Clear(HttpContext httpContext)
        => GetSession(httpContext)?.Remove(SessionKey);

    private static ISession? GetSession(HttpContext httpContext)
        => httpContext.Features.Get<ISessionFeature>()?.Session;
}

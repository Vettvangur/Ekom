using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Net;
using System.Text.Json;

namespace Ekom.Site.Controllers;

[ApiController]
[Route("dev/tracking-simulation")]
public sealed class TrackingSimulationController : ControllerBase
{
    private const string CookieHubCookieName = "cookiehub";
    private const string GaCookieName = "_ga";
    private const string GaSessionCookieName = "_ga_SIMULATED";
    private const string MetaBrowserCookieName = "_fbp";
    private const string MetaClickCookieName = "_fbc";

    [HttpGet("simple")]
    public IActionResult SimulateSimple([FromQuery] string? provider = null, [FromQuery] string? redirectPath = null)
    {
        if (!IsLocalRequest())
        {
            return NotFound();
        }

        DeleteCookies();

        var targetProvider = string.IsNullOrWhiteSpace(provider)
            ? "both"
            : provider.Trim().ToUpperInvariant() switch
            {
                "GA4" => "ga4",
                "META" => "meta",
                _ => "both"
            };

        var now = DateTimeOffset.UtcNow;
        var fbclid = "test-fbclid-123";

        WriteCookie(CookieHubCookieName, BuildCookieHubCookie(now));

        if (targetProvider is "ga4" or "both")
        {
            WriteCookie(GaCookieName, "GA1.1.123456789.1710000000");
            WriteCookie(GaSessionCookieName, "GS1.1.1710000000.1.1.1710000001.0.0.0");
        }

        if (targetProvider is "meta" or "both")
        {
            WriteCookie(MetaBrowserCookieName, $"fb.1.{now.ToUnixTimeMilliseconds()}.1234567890");
            WriteCookie(MetaClickCookieName, $"fb.1.{now.ToUnixTimeMilliseconds()}.{fbclid}");
        }

        return Redirect(NormalizeRedirectPath(redirectPath));
    }

    [HttpGet("")]
    public IActionResult Simulate([FromQuery] string? provider = null, [FromQuery] string? redirectPath = null)
    {
        if (!IsLocalRequest())
        {
            return NotFound();
        }

        DeleteCookies();

        var targetProvider = string.IsNullOrWhiteSpace(provider)
            ? "both"
            : provider.Trim().ToUpperInvariant() switch
            {
                "GA4" => "ga4",
                "META" => "meta",
                _ => "both"
            };

        var now = DateTimeOffset.UtcNow;
        var gclid = "test-gclid-123";
        var fbclid = "test-fbclid-123";

        WriteCookie(CookieHubCookieName, BuildCookieHubCookie(now));

        if (targetProvider is "ga4" or "both")
        {
            WriteCookie(GaCookieName, "GA1.1.123456789.1710000000");
            WriteCookie(GaSessionCookieName, "GS1.1.1710000000.1.1.1710000001.0.0.0");
        }

        if (targetProvider is "meta" or "both")
        {
            WriteCookie(MetaBrowserCookieName, $"fb.1.{now.ToUnixTimeMilliseconds()}.1234567890");
            WriteCookie(MetaClickCookieName, $"fb.1.{now.ToUnixTimeMilliseconds()}.{fbclid}");
        }

        var finalRedirectPath = NormalizeRedirectPath(redirectPath);

        var url = QueryHelpers.AddQueryString(finalRedirectPath, new Dictionary<string, string?>
        {
            ["utm_source"] = targetProvider == "meta" ? "facebook" : "google",
            ["utm_medium"] = targetProvider == "meta" ? "paid-social" : "cpc",
            ["utm_campaign"] = "tracking_simulation",
            ["utm_term"] = "ekom-demo",
            ["utm_content"] = targetProvider == "meta" ? "meta_ad_a" : "ga4_ad_a",
            ["gclid"] = targetProvider is "ga4" or "both" ? gclid : null,
            ["fbclid"] = targetProvider is "meta" or "both" ? fbclid : null,
        });

        return Redirect(url);
    }

    [HttpGet("clear")]
    public IActionResult Clear([FromQuery] string? redirectPath = null)
    {
        if (!IsLocalRequest())
        {
            return NotFound();
        }

        DeleteCookies();

        return Redirect(NormalizeRedirectPath(redirectPath));
    }

    private void DeleteCookies()
    {
        DeleteCookie(CookieHubCookieName);
        DeleteCookie(GaCookieName);
        DeleteCookie(GaSessionCookieName);
        DeleteCookie(MetaBrowserCookieName);
        DeleteCookie(MetaClickCookieName);
    }

    private bool IsLocalRequest()
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress;
        if (remoteIp == null)
        {
            return false;
        }

        if (IPAddress.IsLoopback(remoteIp))
        {
            return true;
        }

        var localIp = HttpContext.Connection.LocalIpAddress;
        return localIp != null && remoteIp.Equals(localIp);
    }

    private static string NormalizeRedirectPath(string? redirectPath)
        => !string.IsNullOrWhiteSpace(redirectPath) && redirectPath.StartsWith("/", StringComparison.Ordinal)
            ? redirectPath
            : "/";

    private void WriteCookie(string name, string value)
        => Response.Cookies.Append(name, value, CreateCookieOptions());

    private void DeleteCookie(string name)
        => Response.Cookies.Delete(name, CreateCookieOptions());

    private CookieOptions CreateCookieOptions()
        => new()
        {
            Path = "/",
            HttpOnly = false,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        };

    private static string BuildCookieHubCookie(DateTimeOffset now)
    {
        var payload = JsonSerializer.Serialize(new
        {
            categories = new
            {
                analytics = true,
                marketing = true
            },
            timestamp = now.UtcDateTime.ToString("O")
        });

        return Uri.EscapeDataString(payload);
    }
}

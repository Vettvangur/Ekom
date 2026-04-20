using Ekom.Tracking;
using Microsoft.AspNetCore.Http;
using System.Text;
using Xunit;

namespace Ekom.Tests.Tests;

public class CookieHubTrackingConsentResolverTests
{
    [Fact]
    public void Resolve_Returns_Consent_When_CookieHub_Is_Configured()
    {
        var sut = new CookieHubTrackingConsentResolver();
        var httpContext = CreateHttpContext("%7B%22categories%22%3A%7B%22analytics%22%3Atrue%2C%22marketing%22%3Afalse%7D%2C%22timestamp%22%3A%222026-04-03T12%3A00%3A00Z%22%7D");

        var result = sut.Resolve(httpContext, "Store", CreateOptions("cookiehub", "cookiehub"));

        Assert.NotNull(result);
        Assert.True(result.Analytics);
        Assert.False(result.Marketing);
        Assert.Equal("cookiehub", result.Source);
        Assert.Equal(new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc), result.ResolvedAtUtc);
    }

    [Fact]
    public void Resolve_Returns_Null_When_CookieHub_Is_Not_Configured()
    {
        var sut = new CookieHubTrackingConsentResolver();
        var httpContext = CreateHttpContext("%7B%22categories%22%3A%7B%22analytics%22%3Atrue%7D%7D");

        var result = sut.Resolve(httpContext, "Store", CreateOptions("ekom_consent_analytics", "ekom_consent_marketing"));

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_Returns_Null_When_Cookie_Is_Invalid_Json()
    {
        var sut = new CookieHubTrackingConsentResolver();
        var httpContext = CreateHttpContext("not-json");

        var result = sut.Resolve(httpContext, "Store", CreateOptions("cookiehub", "cookiehub"));

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_Returns_Consent_From_CookieHub_Category_Array()
    {
        var sut = new CookieHubTrackingConsentResolver();
        var payload = "{\"answered\":true,\"allAllowed\":false,\"categories\":[1,2,3],\"timestamp\":\"2026-04-20T14:06:20.128Z\"}";
        var httpContext = CreateHttpContext(ToBase64Url(payload));

        var result = sut.Resolve(httpContext, "Store", CreateOptions("cookiehub", "cookiehub"));

        Assert.NotNull(result);
        Assert.True(result!.Analytics);
        Assert.False(result.Marketing);
    }

    [Fact]
    public void Resolve_Returns_Full_Consent_When_AllAllowed_Is_True()
    {
        var sut = new CookieHubTrackingConsentResolver();
        var payload = "{\"answered\":true,\"allAllowed\":true,\"categories\":[],\"timestamp\":\"2026-04-20T14:06:20.128Z\"}";
        var httpContext = CreateHttpContext(ToBase64Url(payload));

        var result = sut.Resolve(httpContext, "Store", CreateOptions("cookiehub", "cookiehub"));

        Assert.NotNull(result);
        Assert.True(result!.Analytics);
        Assert.True(result.Marketing);
    }

    private static DefaultHttpContext CreateHttpContext(string cookieValue)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = $"cookiehub={cookieValue}";
        return httpContext;
    }

    private static TrackingConsentOptions CreateOptions(string? analyticsCookieName, string? marketingCookieName)
        => new()
        {
            AnalyticsCookieName = analyticsCookieName,
            MarketingCookieName = marketingCookieName
        };

    private static string ToBase64Url(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}

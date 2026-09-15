using Ekom.Models;
using Ekom.Tracking;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class TrackingCookieServiceTests
{
    [Fact]
    public void CaptureAttributionFromRequest_Captures_Mailchimp_Attribution()
    {
        var httpContext = CreateHttpContext(
            "GS2.1.s1784207205$o16$g0$t1784207205$j60$l0$h0",
            "?mc_cid=campaign-1&mc_tc=prec");
        var sut = CreateSut();

        var result = sut.CaptureAttributionFromRequest(httpContext);

        Assert.NotNull(result);
        Assert.Equal("campaign-1", result!.Mailchimp.CampaignId);
        Assert.Equal("prec", result.Mailchimp.TrackingCode);
    }

    [Fact]
    public void CaptureFromRequest_Does_Not_Capture_Mailchimp_Attribution_Without_Marketing_Consent()
    {
        var httpContext = CreateHttpContext(
            "GS2.1.s1784207205$o16$g0$t1784207205$j60$l0$h0",
            "?utm_source=mailchimp&mc_cid=campaign-1&mc_tc=prec");
        var sut = CreateSut(analytics: true, marketing: false);

        var result = sut.CaptureFromRequest(httpContext);

        Assert.NotNull(result);
        Assert.False(result!.Mailchimp.HasData());
    }

    [Fact]
    public void CaptureFromRequest_Captures_Mailchimp_Attribution_With_Marketing_Consent()
    {
        var httpContext = CreateHttpContext(
            "GS2.1.s1784207205$o16$g0$t1784207205$j60$l0$h0",
            "?mc_cid=campaign-1&mc_tc=prec");
        var sut = CreateSut(analytics: false, marketing: true);

        var result = sut.CaptureFromRequest(httpContext);

        Assert.NotNull(result);
        Assert.Equal("campaign-1", result!.Mailchimp.CampaignId);
        Assert.Equal("prec", result.Mailchimp.TrackingCode);
    }

    [Fact]
    public void OrderTracking_Handles_Explicit_Null_Mailchimp_Json()
    {
        var tracking = JsonSerializer.Deserialize<OrderTracking>("{\"source\":\"mailchimp\",\"mailchimp\":null}", new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(tracking);
        Assert.True(tracking!.HasData());
        Assert.NotNull(tracking.Clone().Mailchimp);
    }

    [Fact]
    public void CaptureFromRequest_Extracts_Session_Id_From_Ga4_Cookie()
    {
        var httpContext = CreateHttpContext("GS2.1.s1784207205$o16$g0$t1784207205$j60$l0$h0");
        var sut = CreateSut();

        var result = sut.CaptureFromRequest(httpContext);

        Assert.NotNull(result);
        Assert.Equal("1784207205", result!.Ga4.SessionId);
    }

    [Fact]
    public void CaptureFromRequest_Extracts_Session_Id_From_Legacy_Ga4_Cookie()
    {
        var httpContext = CreateHttpContext("GS1.1.1784207205.16.0.1784207205.0.0.0");
        var sut = CreateSut();

        var result = sut.CaptureFromRequest(httpContext);

        Assert.NotNull(result);
        Assert.Equal("1784207205", result!.Ga4.SessionId);
    }

    [Theory]
    [InlineData("GS2.1.o16$g0$t1784207205")]
    [InlineData("GS2.1.snot-a-number$o16")]
    [InlineData("GS2.1.s0$o16")]
    public void CaptureFromRequest_Returns_Null_Session_Id_For_Invalid_Ga4_Cookie(string cookieValue)
    {
        var httpContext = CreateHttpContext(cookieValue);
        var sut = CreateSut();

        var result = sut.CaptureFromRequest(httpContext);

        Assert.NotNull(result);
        Assert.Null(result!.Ga4.SessionId);
    }

    private static TrackingCookieService CreateSut(bool analytics = true, bool marketing = false)
        => new(
            Options.Create(new TrackingOptions()),
            new TestTrackingConsentService(analytics, marketing));

    private static DefaultHttpContext CreateHttpContext(
        string cookieValue,
        string queryString = "?utm_source=google")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString(queryString);
        httpContext.Request.Headers.Cookie = $"_ga_QY3LLRVE58={cookieValue}";
        return httpContext;
    }

    private sealed class TestTrackingConsentService : ITrackingConsentService
    {
        private readonly bool _analytics;
        private readonly bool _marketing;

        public TestTrackingConsentService(bool analytics, bool marketing)
        {
            _analytics = analytics;
            _marketing = marketing;
        }

        public OrderConsent GetConsent(HttpContext httpContext, string? storeAlias = null)
            => new()
            {
                Analytics = _analytics,
                Marketing = _marketing
            };

        public bool CanCaptureAnalytics(OrderConsent? consent)
            => consent?.Analytics == true;

        public bool CanCaptureMarketing(OrderConsent? consent)
            => consent?.Marketing == true;
    }
}

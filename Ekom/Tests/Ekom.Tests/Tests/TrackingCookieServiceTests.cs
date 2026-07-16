using Ekom.Models;
using Ekom.Tracking;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class TrackingCookieServiceTests
{
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

    private static TrackingCookieService CreateSut()
        => new(
            Options.Create(new TrackingOptions()),
            new TestTrackingConsentService());

    private static DefaultHttpContext CreateHttpContext(string cookieValue)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?utm_source=google");
        httpContext.Request.Headers.Cookie = $"_ga_QY3LLRVE58={cookieValue}";
        return httpContext;
    }

    private sealed class TestTrackingConsentService : ITrackingConsentService
    {
        public OrderConsent GetConsent(HttpContext httpContext, string? storeAlias = null)
            => new()
            {
                Analytics = true
            };

        public bool CanCaptureAnalytics(OrderConsent? consent)
            => consent?.Analytics == true;

        public bool CanCaptureMarketing(OrderConsent? consent)
            => consent?.Marketing == true;
    }
}

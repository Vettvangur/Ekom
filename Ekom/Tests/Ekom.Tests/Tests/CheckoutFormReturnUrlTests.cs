using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using System.Net;
using Xunit;

namespace Ekom.Tests.Tests;

public class CheckoutFormReturnUrlTests
{
    private static HttpRequest Request(string queryString)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("shop.example");
        context.Request.Path = "/checkout/";
        context.Request.QueryString = new QueryString(queryString);
        return context.Request;
    }

    [Fact]
    public void ReturnUrl_CannotBreakOutOfTheHiddenFieldAttribute()
    {
        var returnUrl = HtmlHelperExtensions.BuildCheckoutReturnUrl(
            Request("?x=%27%3E%3Cscript%3Ealert(1)%3C%2Fscript%3E"));

        var written = WebUtility.HtmlEncode(returnUrl);

        Assert.DoesNotContain("'", written);
        Assert.DoesNotContain("<", written);
        Assert.DoesNotContain(">", written);
    }

    [Fact]
    public void ReturnUrl_KeepsQueryValuesUrlEncoded()
    {
        var returnUrl = HtmlHelperExtensions.BuildCheckoutReturnUrl(Request("?step=delivery&note=a%26b%3Dc"));

        Assert.Equal("https://shop.example/checkout/?step=delivery&note=a%26b%3Dc", returnUrl);
    }

    [Theory]
    [InlineData("errorStatus")]
    [InlineData("errorMessage")]
    [InlineData("errorReason")]
    [InlineData("errorType")]
    [InlineData("canRetry")]
    [InlineData("orderline")]
    [InlineData("ERRORSTATUS")]
    public void ReturnUrl_DropsTheErrorValuesTheCheckoutAdds(string key)
    {
        var returnUrl = HtmlHelperExtensions.BuildCheckoutReturnUrl(Request($"?step=payment&{key}=stockError"));

        Assert.Equal("https://shop.example/checkout/?step=payment", returnUrl);
    }

    [Fact]
    public void ReturnUrl_WithoutQueryHasNoQuestionMark()
    {
        Assert.Equal("https://shop.example/checkout/", HtmlHelperExtensions.BuildCheckoutReturnUrl(Request("")));
    }
}

using Ekom.Controllers;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using System.Reflection;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class CheckoutReturnUrlTests
{
    [Theory]
    [InlineData("https://bxss.me/")]
    [InlineData("//bxss.me/")]
    [InlineData("/%2fbxss.me/")]
    [InlineData("/\\bxss.me/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://www.hverslun.is.evil.test/")]
    [InlineData("https://www.hverslun.is@evil.test/")]
    [InlineData("http://www.hverslun.is/karfa/")]
    [InlineData("https://www.hverslun.is:444/karfa/")]
    [InlineData("https://www.hverslun.is//evil.test/")]
    [InlineData("/karfa/%0d%0aLocation:%20https://evil.test/")]
    [InlineData("https://www.hverslun.is/karfa/?canRetry=false&#x27;&quot;()&amp;%&lt;zzz&gt;&lt;ScRiPt >30Rj(9998)</ScRiPt>")]
    [InlineData("/karfa/?x=%3Cscript%3Ealert(1)%3C/script%3E")]
    [InlineData("/karfa/?step=1&STEP=2")]
    public async Task InvalidTargetsAreRejectedBeforePayment(string target)
    {
        var controller = Controller();
        var result = Assert.IsType<RedirectResult>(await controller.Pay(new PaymentRequest { ReturnUrl = target }));
        Assert.Equal("/?errorStatus=badReturnUrl", result.Url);
    }

    [Theory]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("/karfa/?step=3#payment", "/karfa/?step=3#payment")]
    [InlineData("https://www.hverslun.is/karfa/?step=3", "/karfa/?step=3")]
    [InlineData("https://www.hverslun.is:443/karfa/", "/karfa/")]
    [InlineData("~/karfa/", "/shop/karfa/")]
    public void StoreTargetsAreNormalized(string? target, string expected)
    {
        var controller = Controller();
        controller.Request.PathBase = "/shop";
        Assert.True(CheckoutReturnUrl.TryNormalize(target, controller.Request, out var actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task DuplicateFormTargetsAreRejected()
    {
        var controller = Controller();
        controller.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["returnUrl"] = new StringValues(["/karfa/", "https://bxss.me/"]),
        });
        var result = Assert.IsType<RedirectResult>(await controller.Pay(new PaymentRequest { ReturnUrl = "/karfa/" }));
        Assert.Equal("/?errorStatus=badReturnUrl", result.Url);
    }

    [Fact]
    public async Task ExceptionRedirectRetainsValidatedLocalTarget()
    {
        // A missing service forces the catch path after return URL validation.
        var result = Assert.IsType<RedirectResult>(await Controller().Pay(new PaymentRequest
        {
            ReturnUrl = "https://www.hverslun.is/karfa/?errorStatus=forged&step=3",
        }));
        Assert.Equal("/karfa/?step=3&errorStatus=serverError", result.Url);
    }

    [Theory]
    [InlineData("https://bxss.me/", "/")]
    [InlineData("https://www.hverslun.is/karfa/?canRetry=true&CANRETRY=true&%65rrorStatus=forged&errorMessage=forged&errorReason=forged&errorType=forged&orderline=forged&n936389=v967166", "/karfa/")]
    public void ConflictRedirectsValidateTargetsAndReplaceReservedParameters(string target, string expectedPath)
    {
        var response = new CheckoutResponse
        {
            HttpStatusCode = 409,
            ReturnUrl = target,
            ResponseBody = new CheckoutStateError(CheckoutConflictReason.Busy),
        };
        var result = Assert.IsType<RedirectResult>(typeof(CheckoutController)
            .GetMethod("ResponseHandler", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Controller(), [response]));
        var uri = new Uri("https://www.hverslun.is" + result.Url);
        Assert.Equal(expectedPath, uri.AbsolutePath);
        var query = QueryHelpers.ParseQuery(uri.Query);
        Assert.Equal("false", Assert.Single(query["canRetry"]));
        Assert.Equal("checkoutBusy", Assert.Single(query["errorStatus"]));
        Assert.Single(query["errorMessage"]);
        Assert.False(query.ContainsKey("errorReason"));
        Assert.False(query.ContainsKey("errorType"));
        Assert.False(query.ContainsKey("orderline"));
        if (expectedPath == "/karfa/") Assert.Equal("v967166", query["n936389"].ToString());
    }

    [Fact]
    public void EncodedDelimitersRemainInsideTheirQueryValue()
    {
        Assert.True(CheckoutReturnUrl.TryNormalize("/karfa/?note=hello%26canRetry%3Dtrue", Controller().Request, out var target));
        var query = QueryHelpers.ParseQuery(new Uri("https://www.hverslun.is" + target).Query);
        Assert.Single(query);
        Assert.Equal("hello&canRetry=true", query["note"].ToString());
    }

    [Fact]
    public void PaymentProviderRedirectStillAllowsExternalPaymentPage()
    {
        const string providerUrl = "https://payments.example.test/session/123";
        var result = Assert.IsType<RedirectResult>(typeof(CheckoutController)
            .GetMethod("ResponseHandler", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Controller(), [new CheckoutResponse
            {
                HttpStatusCode = 300,
                ResponseBody = providerUrl,
                ReturnUrl = "/karfa/",
            }]));
        Assert.Equal(providerUrl, result.Url);
    }

    [Theory]
    [InlineData("/payment-error/?step=3", "/payment-error/?step=3&errorStatus=badReturnUrl")]
    [InlineData("https://www.hverslun.is/payment-error/", "/payment-error/?errorStatus=badReturnUrl")]
    [InlineData("https://other.example/", "/?errorStatus=badReturnUrl")]
    public async Task DuplicateTargetsUseConfiguredLocalProviderErrorPage(string configuredUrl, string expected)
    {
        var controller = Controller(configuredUrl);
        controller.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["returnUrl"] = new StringValues(["/karfa/", "/karfa/"]),
        });
        var result = Assert.IsType<RedirectResult>(await controller.Pay(new PaymentRequest { ReturnUrl = "/karfa/" }));
        Assert.Equal(expected, result.Url);
    }

    private sealed class ErrorUrlService(HttpContext context, string? errorUrl) : CheckoutControllerService(
        NullLogger.Instance, null!, null!, null!, new HttpContextAccessor { HttpContext = context }, null!, null!, null!)
    {
        internal override Task<string?> ResolveCheckoutErrorUrlAsync(PaymentRequest request, CancellationToken ct)
            => Task.FromResult(errorUrl);
    }

    private static CheckoutController Controller(string? errorUrl = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("www.hverslun.is");
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>());
        return new CheckoutController(NullLogger<CheckoutController>.Instance, new ErrorUrlService(context, errorUrl))
        {
            ControllerContext = new ControllerContext { HttpContext = context },
        };
    }
}

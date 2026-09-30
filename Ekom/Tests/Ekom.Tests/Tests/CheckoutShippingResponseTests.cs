using Ekom.Controllers;
using Ekom.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using Xunit;

namespace Ekom.Tests.Tests;

public class CheckoutShippingResponseTests
{
    [Fact]
    public void ApiShippingValidationErrorReturnsStructuredBody()
    {
        var error = new ShippingValidationError { Reason = "belowMinimumAmount", Message = "Free shipping requires 10,000." };
        var controller = new EkomCheckoutApiController(NullLogger<EkomCheckoutApiController>.Instance, null!);
        var method = typeof(EkomCheckoutApiController).GetMethod("ResponseHandler", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var result = Assert.IsType<BadRequestObjectResult>(method.Invoke(controller,
            [new CheckoutResponse { HttpStatusCode = 400, ResponseBody = error }]));

        Assert.Same(error, result.Value);
    }

    [Fact]
    public void MvcShippingValidationErrorRedirectIncludesEncodedReasonAndMessage()
    {
        var error = new ShippingValidationError
        {
            Reason = "belowMinimumAmount",
            Message = "Free shipping & pickup requires 10,000. Please select shipping again.",
        };
        var controller = new CheckoutController(NullLogger<CheckoutController>.Instance, null!);
        var method = typeof(CheckoutController).GetMethod("ResponseHandler", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var result = Assert.IsType<RedirectResult>(method.Invoke(controller,
            [new CheckoutResponse { HttpStatusCode = 400, ResponseBody = error, ReturnUrl = "/checkout/payment?step=3" }]));
        var query = QueryHelpers.ParseQuery(new Uri("https://example.test" + result.Url).Query);

        Assert.Equal("invalidShippingProvider", query["errorStatus"].ToString());
        Assert.Equal(error.Reason, query["errorReason"].ToString());
        Assert.Equal(error.Message, query["errorMessage"].ToString());
        Assert.Equal("3", query["step"].ToString());
    }

    [Fact]
    public void OtherValidationErrorsKeepExistingResponses()
    {
        var api = new EkomCheckoutApiController(NullLogger<EkomCheckoutApiController>.Instance, null!);
        var mvc = new CheckoutController(NullLogger<CheckoutController>.Instance, null!);
        var response = new CheckoutResponse { HttpStatusCode = 400, ReturnUrl = "/checkout" };

        Assert.IsType<BadRequestResult>(typeof(EkomCheckoutApiController)
            .GetMethod("ResponseHandler", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(api, [response]));
        var result = Assert.IsType<RedirectResult>(typeof(CheckoutController)
            .GetMethod("ResponseHandler", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(mvc, [response]));
        Assert.Equal("/checkout?errorStatus=invalidData", result.Url);
    }
}

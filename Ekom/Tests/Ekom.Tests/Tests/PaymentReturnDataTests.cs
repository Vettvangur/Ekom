using Ekom.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class PaymentReturnDataTests
{
    [Fact]
    public async Task ProviderPostHandsOffToGetWithAttemptAndCaseDistinctValues()
    {
        var orderId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.PathBase = "/shop";
        context.Request.Path = "/ekom/checkout/payment-return";
        context.Request.QueryString = new QueryString($"?orderId={orderId}&attemptId={attemptId}&outcome=cancel&orderid=provider-reference");
        var controller = new EkomCheckoutApiController(NullLogger<EkomCheckoutApiController>.Instance, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
        };

        var result = Assert.IsType<RedirectResult>(await controller.PaymentReturnAsync());

        Assert.StartsWith("/shop/ekom/checkout/payment-return?", result.Url, StringComparison.Ordinal);
        var values = EkomCheckoutApiController.ParsePaymentReturnData(result.Url[(result.Url.IndexOf('?'))..]);
        Assert.Equal(orderId.ToString(), values["orderId"]);
        Assert.Equal(attemptId.ToString(), values["attemptId"]);
        Assert.Equal("provider-reference", values["orderid"]);
        Assert.Equal("cancel", values["outcome"]);
    }

    [Fact]
    public async Task OrderIdInPathSurvivesAProviderThatReplacesTheQueryString()
    {
        // Teya drops the return URL's query string and appends its own status and orderid.
        var orderId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = $"/ekom/checkout/payment-return/cancel/{orderId}";
        context.Request.QueryString = new QueryString("?status=CANCEL&orderid=32204");
        var controller = new EkomCheckoutApiController(NullLogger<EkomCheckoutApiController>.Instance, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
        };

        var result = Assert.IsType<RedirectResult>(await controller.PaymentReturnAsync("cancel", orderId));

        var values = EkomCheckoutApiController.ParsePaymentReturnData(result.Url[(result.Url.IndexOf('?'))..]);
        Assert.Equal(orderId.ToString(), values["orderId"]);
        Assert.Equal("cancel", values["outcome"]);
        Assert.Equal("32204", values["orderid"]);
        Assert.Equal("CANCEL", values["status"]);
    }

    [Fact]
    public void RedirectToTheCancelPageHasNoKeysThatDifferOnlyByCase()
    {
        var orderId = Guid.NewGuid();
        var attemptId = Guid.NewGuid().ToString();
        var callbackData = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["orderid"] = "32204",
            ["Outcome"] = "provider-outcome",
            ["status"] = "CANCEL",
            ["orderId"] = "stale",
            ["attemptId"] = attemptId,
            ["empty"] = null,
        };

        var query = EkomCheckoutApiController.BuildPaymentReturnRedirectQuery(callbackData, orderId, "cancel");

        Assert.Equal(4, query.Count);
        Assert.Equal(orderId.ToString(), query["orderId"]);
        Assert.Equal("cancel", query["outcome"]);
        Assert.Equal(attemptId, query["attemptId"]);
        Assert.Equal("CANCEL", query["status"]);
        Assert.Contains("orderId", query.Keys);
        Assert.DoesNotContain("orderid", query.Keys);
    }

    [Fact]
    public async Task InvalidAttemptTokenIsRejectedBeforeAnyOrderLookup()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.QueryString = new QueryString($"?orderId={Guid.NewGuid()}&attemptId=not-a-guid&outcome=cancel");
        var controller = new EkomCheckoutApiController(NullLogger<EkomCheckoutApiController>.Instance, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
        };

        Assert.IsType<BadRequestObjectResult>(await controller.PaymentReturnAsync());
    }

    [Fact]
    public void Preserves_Case_Distinct_Order_Id_Callback_Values()
    {
        const string ekomOrderId = "8e32fc81-ac6b-4444-a2cd-5fa7468aa4e8";
        const string providerOrderId = "54173eb6-d255-4d48-b411-01f0927f0cfa";

        var callbackData = EkomCheckoutApiController.ParsePaymentReturnData(
            $"?reference={providerOrderId}&orderId={ekomOrderId}&outcome=cancel&orderid={providerOrderId}");

        Assert.Equal(ekomOrderId, callbackData["orderId"]);
        Assert.Equal(providerOrderId, callbackData["orderid"]);
        Assert.Equal("cancel", callbackData["outcome"]);
        Assert.True(Guid.TryParse(callbackData["orderId"], out var orderId));
        Assert.Equal(Guid.Parse(ekomOrderId), orderId);

        var redirectUrl = QueryHelpers.AddQueryString("/cancel", callbackData);

        Assert.Contains($"orderId={ekomOrderId}", redirectUrl, StringComparison.Ordinal);
        Assert.Contains($"orderid={providerOrderId}", redirectUrl, StringComparison.Ordinal);
    }
}

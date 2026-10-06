using Ekom.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class CheckoutPaymentAttemptUrlTests
{
    [Fact]
    public async Task ReturnUrlsPreservePathBaseAndIdentifyCurrentAttempt()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("shop.example.test");
        context.Request.PathBase = "/store";
        var controller = new UrlController(context);
        var orderId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        await using var operation = new CheckoutPaymentOperationScope(orderId, "test-owner", null)
        {
            AttemptId = attemptId,
        };

        using (operation.Enter())
        {
            var uri = new Uri(controller.ReturnUrl(orderId, "cancel"));
            var values = QueryHelpers.ParseQuery(uri.Query);
            Assert.Equal("/store/ekom/checkout/payment-return", uri.AbsolutePath);
            Assert.Equal(orderId.ToString(), values["orderId"]);
            Assert.Equal(attemptId.ToString(), values["attemptId"]);
            Assert.Equal("cancel", values["outcome"]);
            Assert.Equal(attemptId, controller.AttemptId);
        }

        Assert.Null(controller.AttemptId);
    }

    private sealed class UrlController(HttpContext context) : CheckoutControllerService(
        NullLogger.Instance, null!, null!, null!, new HttpContextAccessor { HttpContext = context },
        null!, null!, null!)
    {
        public Guid? AttemptId => CurrentPaymentAttemptId;
        public string ReturnUrl(Guid orderId, string outcome) => BuildPaymentReturnUrl(orderId, outcome);
    }
}

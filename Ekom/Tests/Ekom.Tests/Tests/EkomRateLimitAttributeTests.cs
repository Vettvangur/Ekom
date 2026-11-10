using Ekom.ActionFilters;
using Ekom.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using System.Net;
using System.Reflection;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class EkomRateLimitAttributeTests
{
    [Fact]
    public void RejectsWith429OncePerClientLimitIsReachedAndLeavesOtherClientsAlone()
    {
        var filter = new EkomRateLimitAttribute(EkomRateLimitPolicies.OrderCoupon);
        var attacker = IPAddress.Parse("203.0.113.10");
        var other = IPAddress.Parse("203.0.113.11");

        var results = Enumerable.Range(0, 20).Select(_ => Execute(filter, attacker)).ToList();

        Assert.Equal(13, results.Count(x => x == null));
        Assert.All(results.Skip(13), x => Assert.Equal(429, Assert.IsType<StatusCodeResult>(x).StatusCode));
        Assert.Null(Execute(filter, other));
    }

    [Theory]
    [InlineData(typeof(EkomOrderController), nameof(EkomOrderController.AddToOrder), EkomRateLimitPolicies.OrderAdd)]
    [InlineData(typeof(EkomOrderController), nameof(EkomOrderController.ApplyCouponToOrder), EkomRateLimitPolicies.OrderCoupon)]
    [InlineData(typeof(EkomOrderController), nameof(EkomOrderController.AddGiftcard), EkomRateLimitPolicies.OrderGiftcard)]
    [InlineData(typeof(EkomCheckoutApiController), nameof(EkomCheckoutApiController.Pay), EkomRateLimitPolicies.CheckoutPay)]
    public void PublicWriteEndpointsCarryALimit(Type controller, string action, string policy)
    {
        var attribute = controller.GetMethod(action)!.GetCustomAttribute<EkomRateLimitAttribute>();

        Assert.Equal(policy, attribute?.Policy);
    }

    private static IActionResult? Execute(EkomRateLimitAttribute filter, IPAddress client)
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = client;
        var context = new ResourceExecutingContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(),
            new List<IValueProviderFactory>());

        filter.OnResourceExecuting(context);
        return context.Result;
    }
}

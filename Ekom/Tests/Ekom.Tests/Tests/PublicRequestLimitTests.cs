using Ekom.ActionFilters;
using Ekom.Controllers;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class PublicRequestLimitTests
{
    [Theory]
    [InlineData("/ekom/order/updatecustomer", 200_000, 413)]
    [InlineData("/EKOM/checkout/pay", 200_000, 413)]
    [InlineData("/ekom/order/updatecustomer", 1_000, 200)]
    [InlineData("/ekom/manager/changeOrderStatus", 200_000, 200)]
    [InlineData("/ekom/payments/NetgiroResponse", 200_000, 200)]
    [InlineData("/some-page", 200_000, 200)]
    public async Task OversizedBodiesAreRejectedOnPublicEkomRoutesOnly(string path, long contentLength, int expectedStatus)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.ContentLength = contentLength;
        var reachedNext = false;
        var middleware = new EkomRequestSizeLimitMiddleware(_ => { reachedNext = true; return Task.CompletedTask; }, CreateConfiguration());

        await middleware.InvokeAsync(context);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal(expectedStatus == 200, reachedNext);
    }

    [Fact]
    public async Task BodiesWithoutContentLengthGetTheServerSideCap()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/ekom/order/updatecustomer";
        var feature = new BodySizeFeature();
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(feature);
        var middleware = new EkomRequestSizeLimitMiddleware(_ => Task.CompletedTask, CreateConfiguration());

        await middleware.InvokeAsync(context);

        Assert.Equal(128_000, feature.MaxRequestBodySize);
    }

    [Fact]
    public async Task CapCanBeChangedOrDisabledThroughConfiguration()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/ekom/order/updatecustomer";
        context.Request.ContentLength = 200_000;
        var middleware = new EkomRequestSizeLimitMiddleware(_ => Task.CompletedTask, CreateConfiguration("0"));

        await middleware.InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public void RequestFieldsAreLimitedInLengthAndCount()
    {
        OrderFieldLimits.ValidateRequestFields(new Dictionary<string, string> { ["customerNotes"] = new string('x', 4096) });

        Assert.Throws<CustomerInformationValidationException>(() => OrderFieldLimits.ValidateRequestFields(
            new Dictionary<string, string> { ["customerNotes"] = new string('x', 4097) }));
        Assert.Throws<CustomerInformationValidationException>(() => OrderFieldLimits.ValidateRequestFields(
            new Dictionary<string, string> { [new string('k', 101)] = "v" }));
        Assert.Throws<CustomerInformationValidationException>(() => OrderFieldLimits.ValidateRequestFields(
            Enumerable.Range(0, 201).ToDictionary(i => "customer" + i, _ => "v")));
    }

    [Fact]
    public void AnOrderCannotAccumulateUnlimitedCustomerFields()
    {
        var customer = new Customer();
        for (var i = 0; i < 500; i++) customer.Properties["customerField" + i] = "v";

        // Updating existing fields is still allowed at the cap.
        OrderService.ValidateCustomerInformation(new Dictionary<string, string> { ["customerField1"] = "new" }, customer);

        Assert.Throws<CustomerInformationValidationException>(() => OrderService.ValidateCustomerInformation(
            new Dictionary<string, string> { ["customerOneMore"] = "v" }, customer));
    }

    [Theory]
    [InlineData(nameof(EkomOrderController.UpdateCustomerInformation))]
    [InlineData(nameof(EkomOrderController.UpdateTracking))]
    [InlineData(nameof(EkomOrderController.UpdateShippingProvider))]
    [InlineData(nameof(EkomOrderController.UpdatePaymentProvider))]
    public void OrderUpdateRoutesAreRateLimited(string action)
    {
        var attribute = typeof(EkomOrderController).GetMethod(action)!.GetCustomAttribute<EkomRateLimitAttribute>();

        Assert.Equal(EkomRateLimitPolicies.OrderUpdate, attribute?.Policy);
    }

    private static Configuration CreateConfiguration(string? maxBodySize = null)
        => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ekom:Api:MaxRequestBodySize"] = maxBodySize,
        }).Build());

    private sealed class BodySizeFeature : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly => false;
        public long? MaxRequestBodySize { get; set; }
    }
}

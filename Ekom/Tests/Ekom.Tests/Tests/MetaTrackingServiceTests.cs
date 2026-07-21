using Ekom.Repositories;
using Ekom.Tracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Reflection;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class MetaTrackingServiceTests
{
    [Fact]
    public void MetaEvents_AreDisabledByDefault()
    {
        var options = new TrackingOptions();

        Assert.False(options.Meta.Events.AddedToCart);
        Assert.False(options.Meta.Events.RemovedFromCart);
        Assert.False(options.Meta.Events.StartedCheckout);
        Assert.False(options.Meta.Events.AddedShippingInfo);
        Assert.False(options.Meta.Events.AddedPaymentInfo);
    }

    [Fact]
    public async Task SendPurchaseAsync_Does_Not_Send_Or_Clear_Data_When_Marketing_Consent_Is_Missing()
    {
        using var handler = new CountingHttpMessageHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://graph.facebook.com/v20.0/")
        };
        var request = new MetaPurchaseRequest
        {
            HasMarketingConsent = false,
            StoreAlias = "store",
            Email = "customer@example.com",
            Value = 10,
            Currency = "ISK"
        };
        var sut = new MetaTrackingService(
            httpClient,
            Options.Create(new TrackingOptions()),
            new ThrowingServiceScopeFactory(),
            NullLogger<MetaTrackingService>.Instance);

        await sut.SendPurchaseAsync(request);

        Assert.Equal(0, handler.CallCount);
        Assert.Equal("customer@example.com", request.Email);
    }

    [Theory]
    [InlineData("uk", false, true)]
    [InlineData("UK", false, true)]
    [InlineData("other", false, false)]
    [InlineData("uk", true, false)]
    public void BuildEventSourceUrl_Uses_Landing_Url_Then_Store_Override_Then_Global_Fallback(string storeAlias, bool hasLandingUrl, bool usesStoreOverride)
    {
        var options = new TrackingOptions
        {
            SiteBaseUrl = "https://default.example.com",
            Stores =
            [
                new TrackingStoreUrlOptions
                {
                    Alias = "uk",
                    SiteBaseUrl = "https://uk.example.com"
                }
            ]
        };
        var sut = new MetaTrackingService(
            new HttpClient(),
            Options.Create(options),
            new ThrowingServiceScopeFactory(),
            NullLogger<MetaTrackingService>.Instance);
        var orderInfo = new Mock<Ekom.Models.IOrderInfo>();
        orderInfo.SetupGet(x => x.StoreInfo).Returns(CreateStoreInfo(storeAlias));
        var landingUrl = hasLandingUrl ? "https://landing.example.com" : null;
        var expectedUrl = hasLandingUrl
            ? landingUrl
            : usesStoreOverride ? "https://uk.example.com" : "https://default.example.com";

        var result = InvokeBuildEventSourceUrl(sut, orderInfo.Object, new Ekom.Models.OrderTracking { LandingUrl = landingUrl });

        Assert.Equal(expectedUrl, result);
    }

    private static string? InvokeBuildEventSourceUrl(MetaTrackingService service, Ekom.Models.IOrderInfo orderInfo, Ekom.Models.OrderTracking tracking)
    {
        var method = typeof(MetaTrackingService).GetMethod("BuildEventSourceUrl", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        return (string?)method.Invoke(service, [orderInfo, tracking]);
    }

    private static Ekom.Models.StoreInfo CreateStoreInfo(string alias)
        => new(
            Guid.NewGuid(),
            new Ekom.Models.CurrencyModel(),
            [],
            "en-GB",
            alias,
            false,
            0,
            false);

    private sealed class CountingHttpMessageHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class ThrowingServiceScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope()
            => throw new InvalidOperationException("Scope should not be created when consent is missing.");
    }
}

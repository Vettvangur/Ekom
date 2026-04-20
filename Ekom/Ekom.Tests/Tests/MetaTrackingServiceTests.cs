using Ekom.Repositories;
using Ekom.Tracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class MetaTrackingServiceTests
{
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

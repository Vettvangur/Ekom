using Ekom.Repositories;
using Ekom.Tracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class Ga4DebugModeTests
{
    [Fact]
    public async Task SendPurchaseAsync_UsesDebugModeWithoutDebugEndpoint()
    {
        using var handler = new RecordingHttpMessageHandler();
        using var client = new HttpClient(handler);
        var sut = CreateService(client, useDebugEndpoint: false, debugMode: true);

        await sut.SendPurchaseAsync(CreateRequest());

        Assert.Equal("/mp/collect", handler.RequestUri!.AbsolutePath);
        using var document = JsonDocument.Parse(handler.Payload);
        var parameters = document.RootElement.GetProperty("events")[0].GetProperty("params");
        Assert.True(parameters.GetProperty("debug_mode").GetBoolean());
    }

    [Fact]
    public async Task SendPurchaseAsync_UsesDebugEndpointWithoutDebugMode()
    {
        using var handler = new RecordingHttpMessageHandler();
        using var client = new HttpClient(handler);
        var sut = CreateService(client, useDebugEndpoint: true, debugMode: false);

        await sut.SendPurchaseAsync(CreateRequest());

        Assert.Equal("/debug/mp/collect", handler.RequestUri!.AbsolutePath);
        using var document = JsonDocument.Parse(handler.Payload);
        var parameters = document.RootElement.GetProperty("events")[0].GetProperty("params");
        Assert.False(parameters.TryGetProperty("debug_mode", out _));
    }

    [Fact]
    public async Task SendPurchaseAsync_UsesTestingAsFallbackForBothDebugSettings()
    {
        using var handler = new RecordingHttpMessageHandler();
        using var client = new HttpClient(handler);
        var sut = CreateService(client, useDebugEndpoint: null, debugMode: null, testing: true);

        await sut.SendPurchaseAsync(CreateRequest());

        Assert.Equal("/debug/mp/collect", handler.RequestUri!.AbsolutePath);
        using var document = JsonDocument.Parse(handler.Payload);
        var parameters = document.RootElement.GetProperty("events")[0].GetProperty("params");
        Assert.True(parameters.GetProperty("debug_mode").GetBoolean());
    }

    private static Ga4TrackingService CreateService(HttpClient client, bool? useDebugEndpoint, bool? debugMode, bool testing = false)
        => new(
            new StaticHttpClientFactory(client),
            Options.Create(new TrackingOptions
            {
                Ga4 = new Ga4TrackingProviderOptions
                {
                    Testing = testing,
                    UseDebugEndpoint = useDebugEndpoint,
                    DebugMode = debugMode,
                    Stores =
                    [
                        new TrackingStoreOptions
                        {
                            Alias = "Store",
                            MeasurementId = "G-XXXXXXXXXX",
                            ApiSecret = "api-secret",
                        },
                    ],
                },
            }),
            new ThrowingServiceScopeFactory(),
            NullLogger<Ga4TrackingService>.Instance);

    private static Ga4PurchaseRequest CreateRequest()
        => new()
        {
            StoreAlias = "Store",
            ClientId = "123.456",
            TransactionId = "ORDER-1",
            Value = 10,
            Currency = "ISK",
        };

    private sealed class StaticHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string Payload { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Payload = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class ThrowingServiceScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope()
            => throw new InvalidOperationException("Activity logging should not affect tracking delivery.");
    }
}

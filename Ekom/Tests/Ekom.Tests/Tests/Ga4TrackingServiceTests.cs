using Ekom.Repositories;
using Ekom.Tracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class Ga4TrackingServiceTests
{
    [Fact]
    public async Task SendPurchaseAsync_AddToCartEvent_DoesNotIncludePurchaseParameters()
    {
        using var handler = new RecordingHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var sut = new Ga4TrackingService(
            new StaticHttpClientFactory(httpClient),
            Options.Create(new TrackingOptions
            {
                Ga4 = new Ga4TrackingProviderOptions
                {
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

        await sut.SendPurchaseAsync(new Ga4PurchaseRequest
        {
            StoreAlias = "Store",
            ClientId = "123.456",
            EventName = "add_to_cart",
            Value = 10,
            Currency = "ISK",
            Items =
            [
                new Ga4PurchaseItem
                {
                    ItemId = "SKU-1",
                    ItemName = "Product",
                    Price = 10,
                    Quantity = 1,
                },
            ],
        });

        using var document = JsonDocument.Parse(handler.Payload);
        var ga4Event = document.RootElement.GetProperty("events")[0];
        var parameters = ga4Event.GetProperty("params");

        Assert.Equal("add_to_cart", ga4Event.GetProperty("name").GetString());
        Assert.False(parameters.TryGetProperty("transaction_id", out _));
        Assert.False(parameters.TryGetProperty("shipping", out _));
        Assert.Equal(10, parameters.GetProperty("value").GetDecimal());
    }

    [Fact]
    public void Ga4Events_AreDisabledByDefault()
    {
        var options = new TrackingOptions();

        Assert.False(options.Ga4.Events.AddedToCart);
        Assert.False(options.Ga4.Events.RemovedFromCart);
        Assert.False(options.Ga4.Events.StartedCheckout);
        Assert.False(options.Ga4.Events.AddedShippingInfo);
        Assert.False(options.Ga4.Events.AddedPaymentInfo);
    }

    [Theory]
    [InlineData("add_shipping_info", "shipping_tier", "Delivery")]
    [InlineData("add_payment_info", "payment_type", "Card")]
    public async Task SendPurchaseAsync_CheckoutEvent_IncludesSelectedProvider(string eventName, string parameterName, string parameterValue)
    {
        using var handler = new RecordingHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var sut = new Ga4TrackingService(
            new StaticHttpClientFactory(httpClient),
            Options.Create(new TrackingOptions
            {
                Ga4 = new Ga4TrackingProviderOptions
                {
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

        await sut.SendPurchaseAsync(new Ga4PurchaseRequest
        {
            StoreAlias = "Store",
            ClientId = "123.456",
            EventName = eventName,
            Value = 10,
            Currency = "ISK",
            ShippingTier = eventName == "add_shipping_info" ? parameterValue : null,
            PaymentType = eventName == "add_payment_info" ? parameterValue : null,
            Items =
            [
                new Ga4PurchaseItem
                {
                    ItemId = "SKU-1",
                    ItemName = "Product",
                    Price = 10,
                    Quantity = 1,
                },
            ],
        });

        using var document = JsonDocument.Parse(handler.Payload);
        var parameters = document.RootElement.GetProperty("events")[0].GetProperty("params");

        Assert.Equal(parameterValue, parameters.GetProperty(parameterName).GetString());
        Assert.False(parameters.TryGetProperty("transaction_id", out _));
    }

    private sealed class StaticHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public string Payload { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
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

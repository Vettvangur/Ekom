using Ekom.Repositories;
using Ekom.Tracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Reflection;
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
            HasAnalyticsConsent = true,
            HasCapturedSessionId = true,
            SessionId = 123,
            EventName = "add_to_cart",
            Value = 12,
            Currency = "ISK",
            Source = "Klaviyo",
            Medium = "email",
            Campaign = "spring-sale",
            Term = "coffee",
            Content = "newsletter",
            Items =
            [
                new Ga4PurchaseItem
                {
                    ItemId = "SKU-1",
                    ItemName = "Product",
                    Price = 10,
                    Discount = 2,
                    Quantity = 1.5m,
                    Coupon = "ITEM-COUPON"
                },
            ],
        });

        using var document = JsonDocument.Parse(handler.Payload);
        var ga4Event = document.RootElement.GetProperty("events")[0];
        var parameters = ga4Event.GetProperty("params");

        Assert.Equal("add_to_cart", ga4Event.GetProperty("name").GetString());
        Assert.False(parameters.TryGetProperty("transaction_id", out _));
        Assert.False(parameters.TryGetProperty("shipping", out _));
        Assert.Equal(12, parameters.GetProperty("value").GetDecimal());
        Assert.Equal(1, parameters.GetProperty("engagement_time_msec").GetInt32());
        Assert.Equal("Klaviyo", parameters.GetProperty("campaign_source").GetString());
        Assert.Equal("email", parameters.GetProperty("campaign_medium").GetString());
        Assert.Equal("spring-sale", parameters.GetProperty("campaign_name").GetString());
        Assert.Equal("coffee", parameters.GetProperty("campaign_term").GetString());
        Assert.Equal("newsletter", parameters.GetProperty("campaign_content").GetString());
        Assert.False(parameters.TryGetProperty("source", out _));
        Assert.False(parameters.TryGetProperty("medium", out _));
        Assert.False(parameters.TryGetProperty("campaign", out _));
        Assert.False(parameters.TryGetProperty("term", out _));
        Assert.False(parameters.TryGetProperty("content", out _));
        var item = parameters.GetProperty("items")[0];
        Assert.Equal(2, item.GetProperty("discount").GetDecimal());
        Assert.Equal(1.5m, item.GetProperty("quantity").GetDecimal());
        Assert.Equal("ITEM-COUPON", item.GetProperty("coupon").GetString());
    }

    [Fact]
    public async Task SendPurchaseAsync_DoesNotIncludeEngagementTime_When_AnalyticsConsent_IsMissing()
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
            HasCapturedSessionId = true,
            SessionId = 123,
            EventName = "add_to_cart",
            Value = 10,
            Currency = "ISK",
        });

        using var document = JsonDocument.Parse(handler.Payload);
        var parameters = document.RootElement.GetProperty("events")[0].GetProperty("params");

        Assert.False(parameters.TryGetProperty("engagement_time_msec", out _));
    }

    [Fact]
    public void Ga4Events_AreDisabledByDefault()
    {
        var options = new TrackingOptions();

        Assert.False(options.Ga4.Events.AddedToCart);
        Assert.False(options.Ga4.Events.StartedCheckout);
    }

    [Theory]
    [InlineData(false, false, "purchase", false)]
    [InlineData(false, true, "purchase", true)]
    [InlineData(false, true, "add_to_cart", false)]
    [InlineData(true, false, "add_to_cart", true)]
    [InlineData(true, true, "InitiateCheckout", true)]
    public void ShouldLogEventData_Respects_AllEvent_And_PurchaseOnly_Options(bool logEventData, bool logPurchaseEventData, string eventName, bool expected)
    {
        var options = new TrackingOptions
        {
            LogEventData = logEventData,
            LogPurchaseEventData = logPurchaseEventData
        };

        Assert.Equal(expected, options.ShouldLogEventData(eventName));
    }

    [Theory]
    [InlineData(12.845814977973568, 2, 2, 6.42)]
    [InlineData(4960, 4, 0, 1240)]
    public void CalculateUnitPrice_Uses_The_Rounded_Unit_Price(decimal lineAmount, decimal quantity, int currencyDecimalDigits, decimal expectedPrice)
    {
        var result = InvokeCalculateUnitPrice(lineAmount, quantity, currencyDecimalDigits);

        Assert.Equal(expectedPrice, result);
    }

    [Fact]
    public void CalculateEventValue_Uses_Rounded_Unit_Prices_And_Quantities()
    {
        var items = new List<Ga4PurchaseItem>
        {
            new() { Price = 6.42m, Discount = 0.42m, Quantity = 2 },
            new() { Price = 6.16m, Discount = 0.16m, Quantity = 3 }
        };

        var result = InvokeCalculateEventValue(items, 2);

        Assert.Equal(30m, result);
    }

    [Theory]
    [InlineData("fi-FI", 2)]
    [InlineData("is-IS", 0)]
    public void CurrencyDecimalDigits_Uses_The_Store_Currency(string currencyValue, int expectedDecimalDigits)
    {
        var currency = new Ekom.Models.CurrencyModel { CurrencyValue = currencyValue };

        Assert.Equal(expectedDecimalDigits, currency.CurrencyDecimalDigits);
    }

    [Fact]
    public void UseCulture_Sets_And_Restores_The_Order_Culture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUICulture = CultureInfo.CurrentUICulture;

        using (InvokeUseCulture("fi-FI"))
        {
            Assert.Equal("fi-FI", CultureInfo.CurrentCulture.Name);
            Assert.Equal("fi-FI", CultureInfo.CurrentUICulture.Name);
        }

        Assert.Equal(originalCulture, CultureInfo.CurrentCulture);
        Assert.Equal(originalUICulture, CultureInfo.CurrentUICulture);
    }

    private static decimal InvokeCalculateUnitPrice(decimal lineAmount, decimal quantity, int currencyDecimalDigits)
    {
        var method = typeof(Ga4TrackingService).GetMethod("CalculateUnitPrice", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        return (decimal)method.Invoke(null, [lineAmount, quantity, currencyDecimalDigits])!;
    }

    private static decimal InvokeCalculateEventValue(IEnumerable<Ga4PurchaseItem> items, int currencyDecimalDigits)
    {
        var method = typeof(Ga4TrackingService).GetMethod("CalculateEventValue", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        return (decimal)method.Invoke(null, [items, currencyDecimalDigits])!;
    }

    private static IDisposable InvokeUseCulture(string culture)
    {
        var method = typeof(Ga4TrackingService).GetMethod("UseCulture", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        return (IDisposable)method.Invoke(null, [culture])!;
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

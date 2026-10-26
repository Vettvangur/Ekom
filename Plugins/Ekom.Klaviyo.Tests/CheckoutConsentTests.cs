using Ekom.Klaviyo.Mappers;
using Ekom.Klaviyo.Models.Profiles;
using Ekom.Models;
using Moq;
using System.Text.Json.Nodes;
using Xunit;

namespace Ekom.Klaviyo.Tests;

public class CheckoutConsentTests
{
    [Theory]
    [InlineData("false", "false", false, false)]
    [InlineData("true", "false", true, false)]
    [InlineData("false", "true", false, true)]
    [InlineData("true", "true", true, true)]
    [InlineData("1", "ON", true, true)]
    [InlineData("TRUE", null, true, false)]
    [InlineData(null, "on", false, true)]
    [InlineData(null, null, false, false)]
    public void MapsOnlySelectedChannels(string? email, string? sms, bool expectEmail, bool expectSms)
    {
        var properties = new Dictionary<string, string>();
        SetProperty(properties, "customerKlaviyoConsentToSubscribeEmail", email);
        SetProperty(properties, "customerKlaviyoConsentToSubscribeSms", sms);

        AssertConsent(properties, expectEmail, expectSms);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("1", true)]
    [InlineData("ON", true)]
    [InlineData("false", false)]
    public void PreservesLegacyConsentWhenNewFieldsAreAbsent(string legacy, bool expected)
    {
        AssertConsent(new Dictionary<string, string>
        {
            ["customerKlaviyoConsentToSubscribe"] = legacy,
        }, expected, expected);
    }

    [Theory]
    [InlineData("false", "false", false, false)]
    [InlineData("true", "false", true, false)]
    [InlineData("false", "true", false, true)]
    [InlineData("false", null, false, false)]
    [InlineData(null, "false", false, false)]
    [InlineData("", null, false, false)]
    public void ExplicitChannelsOverrideLegacyConsent(string? email, string? sms, bool expectEmail, bool expectSms)
    {
        var properties = new Dictionary<string, string>
        {
            ["customerKlaviyoConsentToSubscribe"] = "true",
        };
        SetProperty(properties, "customerKlaviyoConsentToSubscribeEmail", email);
        SetProperty(properties, "customerKlaviyoConsentToSubscribeSms", sms);

        AssertConsent(properties, expectEmail, expectSms);
    }

    private static void AssertConsent(Dictionary<string, string> properties, bool expectEmail, bool expectSms)
    {
        properties["customerEmail"] = "customer@example.com";
        properties["customerPhone"] = "+3545551234";
        var customerInformation = new CustomerInfo
        {
            Customer = new Customer { Properties = properties },
            CustomerIpAddress = "127.0.0.1",
        };
        var price = Mock.Of<ICalculatedPrice>();
        var store = new StoreInfo(Guid.NewGuid(), new CurrencyModel { CurrencyValue = "en-US" },
            new List<CurrencyModel>(), "en-US", "Store", false, 0m, false);
        var order = new Mock<IOrderInfo>();
        order.SetupGet(x => x.CustomerInformation).Returns(customerInformation);
        order.SetupGet(x => x.StoreInfo).Returns(store);
        order.SetupGet(x => x.Culture).Returns("en-US");
        order.SetupGet(x => x.OrderLines).Returns(Array.Empty<IOrderLine>());
        order.SetupGet(x => x.ChargedAmount).Returns(price);
        order.SetupGet(x => x.Vat).Returns(price);
        order.SetupGet(x => x.DiscountAmount).Returns(price);

        var options = new KlaviyoOptions
        {
            Revision = "2026-01-15",
            SiteBaseUrl = "https://example.com",
        };
        var consent = order.Object.ToKlaviyoPlacedOrder(options, DateTimeOffset.UtcNow).Consent;

        if (!expectEmail && !expectSms)
        {
            Assert.Null(consent);
            return;
        }

        Assert.NotNull(consent);
        Assert.Equal("customer@example.com", consent.Email);
        Assert.Equal("+3545551234", consent.PhoneNumber);
        Assert.Equal("en-US", consent.CustomProperties!["locale"]);
        Assert.NotNull(consent.Consents);
        Assert.Equal((expectEmail ? 1 : 0) + (expectSms ? 1 : 0), consent.Consents.Count);
        Assert.Equal(expectEmail, consent.Consents.Any(x => x.Channel == KlaviyoProfileConsentChannel.Email));
        Assert.Equal(expectSms, consent.Consents.Any(x => x.Channel == KlaviyoProfileConsentChannel.Sms));
        Assert.All(consent.Consents, change =>
        {
            Assert.Equal(KlaviyoProfileConsentState.Subscribed, change.State);
            Assert.Equal("checkout", change.Source);
            Assert.Equal("127.0.0.1", change.Ip);
        });

        var payload = Assert.IsType<JsonObject>(consent.ToBulkSubscribeJobRequest());
        var attributes = payload["data"]!["attributes"]!["profiles"]!["data"]![0]!["attributes"]!;
        Assert.Equal("+3545551234", attributes["phone_number"]!.GetValue<string>());
        var subscriptions = Assert.IsType<JsonObject>(attributes["subscriptions"]);
        Assert.Equal(expectEmail, subscriptions.ContainsKey("email"));
        Assert.Equal(expectSms, subscriptions.ContainsKey("sms"));
        foreach (var subscription in subscriptions)
        {
            Assert.Equal("SUBSCRIBED", subscription.Value!["marketing"]!["consent"]!.GetValue<string>());
        }
    }

    private static void SetProperty(Dictionary<string, string> properties, string name, string? value)
    {
        if (value is not null)
        {
            properties[name] = value;
        }
    }
}

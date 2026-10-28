using Ekom.Mailchimp.Mappers;
using Ekom.Models;

namespace Ekom.Mailchimp.Tests;

public sealed class MailchimpOrderMapperTests
{
    [Theory]
    [InlineData("https://shop.example.com", "/media/product.jpg", "https://shop.example.com/media/product.jpg")]
    [InlineData("https://shop.example.com", "/products/item?color=blue#details", "https://shop.example.com/products/item?color=blue#details")]
    [InlineData("https://shop.example.com/store/", "/media/product.jpg", "https://shop.example.com/media/product.jpg")]
    [InlineData("https://shop.example.com/store/", "products/item", "https://shop.example.com/store/products/item")]
    [InlineData("https://shop.example.com", "//cdn.example.com/product.jpg", "https://cdn.example.com/product.jpg")]
    [InlineData("https://shop.example.com", "https://cdn.example.com/product.jpg", "https://cdn.example.com/product.jpg")]
    [InlineData(null, "https://cdn.example.com/product.jpg", "https://cdn.example.com/product.jpg")]
    [InlineData(null, "/media/product.jpg", null)]
    [InlineData(null, "//cdn.example.com/product.jpg", null)]
    [InlineData("https://shop.example.com", null, null)]
    [InlineData("https://shop.example.com", "   ", null)]
    [InlineData("https://shop.example.com", "file:///media/product.jpg", null)]
    [InlineData("https://shop.example.com", "mailto:someone@example.com", null)]
    [InlineData("/store/", "/media/product.jpg", null)]
    [InlineData("file:///store/", "/media/product.jpg", null)]
    public void CombineUrl_ResolvesWebUrls(string? baseUrl, string? path, string? expected)
    {
        Uri? siteBaseUrl = baseUrl == null ? null : new Uri(baseUrl, UriKind.RelativeOrAbsolute);

        Uri? result = MailchimpOrderMapper.CombineUrl(siteBaseUrl, path);

        Assert.Equal(expected, result?.AbsoluteUri);
    }

    [Fact]
    public void ResolveCampaignId_PrefersPersistedOrderTracking()
    {
        var tracking = new OrderTracking
        {
            Mailchimp = new MailchimpOrderTracking { CampaignId = "tracked-campaign" },
        };
        var customer = new Customer();
        customer.Properties["mc_cid"] = "legacy-campaign";

        string? campaignId = MailchimpOrderMapper.ResolveCampaignId(tracking, customer, hasMarketingConsent: true);

        Assert.Equal("tracked-campaign", campaignId);
    }

    [Fact]
    public void ResolveCampaignId_FallsBackToCustomerProperties()
    {
        var customer = new Customer();
        customer.Properties["mc_cid"] = "legacy-campaign";

        string? campaignId = MailchimpOrderMapper.ResolveCampaignId(null, customer, hasMarketingConsent: true);

        Assert.Equal("legacy-campaign", campaignId);
    }

    [Fact]
    public void ResolveTrackingCode_UsesOnlySupportedCode()
    {
        var tracking = new OrderTracking
        {
            Mailchimp = new MailchimpOrderTracking { TrackingCode = "unsupported" },
        };
        var customer = new Customer();
        customer.Properties["mc_tc"] = "prec";

        string? trackingCode = MailchimpOrderMapper.ResolveTrackingCode(tracking, customer, hasMarketingConsent: true);

        Assert.Equal("prec", trackingCode);
    }

    [Fact]
    public void Attribution_IsSuppressedWithoutMarketingConsent()
    {
        var tracking = new OrderTracking
        {
            Mailchimp = new MailchimpOrderTracking
            {
                CampaignId = "tracked-campaign",
                TrackingCode = "prec",
            },
        };
        var customer = new Customer();
        customer.Properties["mc_cid"] = "legacy-campaign";
        customer.Properties["mc_tc"] = "prec";

        string? campaignId = MailchimpOrderMapper.ResolveCampaignId(tracking, customer, hasMarketingConsent: false);
        string? trackingCode = MailchimpOrderMapper.ResolveTrackingCode(tracking, customer, hasMarketingConsent: false);

        Assert.Null(campaignId);
        Assert.Null(trackingCode);
    }
}

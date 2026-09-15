using Ekom.Mailchimp.Mappers;
using Ekom.Models;

namespace Ekom.Mailchimp.Tests;

public sealed class MailchimpOrderMapperTests
{
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

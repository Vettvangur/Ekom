using Ekom.Models;
using Ekom.Tracking;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class OrderTrackingServiceTests
{
    [Fact]
    public void ApplyTracking_RemovesMailchimpAttributionWithoutMarketingConsent()
    {
        var order = new OrderInfo(new OrderData())
        {
            Consent = new OrderConsent { Marketing = false },
        };
        var tracking = new OrderTracking
        {
            Mailchimp = new MailchimpOrderTracking
            {
                CampaignId = "campaign-1",
                TrackingCode = "prec",
            },
        };
        OrderTrackingService sut = CreateSut();

        sut.ApplyTracking(order, tracking, replaceExisting: true);

        Assert.NotNull(order.Tracking);
        Assert.False(order.Tracking!.Mailchimp.HasData());
    }

    [Fact]
    public void ApplyConsent_RemovesPersistedMailchimpAttributionWhenConsentIsWithdrawn()
    {
        var order = new OrderInfo(new OrderData())
        {
            Consent = new OrderConsent { Marketing = true },
            Tracking = new OrderTracking
            {
                Mailchimp = new MailchimpOrderTracking
                {
                    CampaignId = "campaign-1",
                    TrackingCode = "prec",
                },
            },
        };
        OrderTrackingService sut = CreateSut();

        sut.ApplyConsent(order, new OrderConsent { Marketing = false }, replaceExisting: true);

        Assert.NotNull(order.Tracking);
        Assert.False(order.Tracking!.Mailchimp.HasData());
    }

    private static OrderTrackingService CreateSut()
        => new(
            new HttpContextAccessor(),
            Mock.Of<ITrackingCookieService>(),
            Mock.Of<ITrackingConsentService>(),
            Options.Create(new TrackingOptions()));
}

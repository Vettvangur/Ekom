using Ekom.Mailchimp.Helpers;

namespace Ekom.Mailchimp.Tests;

public sealed class MailchimpSubscriberHashTests
{
    [Fact]
    public void Create_NormalizesEmailAndReturnsLowercaseMd5()
    {
        string normalized = MailchimpSubscriberHash.Create("person@example.com");
        string mixedCase = MailchimpSubscriberHash.Create(" Person@Example.COM ");

        Assert.Equal(normalized, mixedCase);
        Assert.Matches("^[0-9a-f]{32}$", normalized);
    }
}

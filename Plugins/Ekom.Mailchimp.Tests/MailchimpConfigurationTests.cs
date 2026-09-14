using Microsoft.Extensions.Options;

namespace Ekom.Mailchimp.Tests;

public sealed class MailchimpConfigurationTests
{
    [Fact]
    public void Resolve_UsesStoreOverridesAndDerivesServerPrefix()
    {
        var mailchimpOptions = new MailchimpOptions
        {
            Enabled = true,
            ApiKey = "global-us1",
            AudienceId = "global-audience",
            EcommerceStoreId = "global-store",
        };
        mailchimpOptions.Stores.Add(new MailchimpStoreOptions
        {
            Alias = "is",
            ApiKey = "override-us20",
            AudienceId = "is-audience",
            EcommerceStoreId = "is-store",
        });
        var options = Options.Create(mailchimpOptions);
        var resolver = new MailchimpConfigurationResolver(options);

        MailchimpStoreConfiguration configuration = resolver.Resolve("IS");

        Assert.Equal("override-us20", configuration.ApiKey);
        Assert.Equal("us20", configuration.ServerPrefix);
        Assert.Equal("is-audience", configuration.AudienceId);
        Assert.Equal("is-store", configuration.EcommerceStoreId);
    }

    [Fact]
    public void Validate_AllowsFullyConfiguredStoresWithoutGlobalCredentials()
    {
        var options = new MailchimpOptions
        {
            Enabled = true,
        };
        options.Stores.Add(new MailchimpStoreOptions
        {
            Alias = "is",
            ApiKey = "key-us1",
            AudienceId = "audience",
            EcommerceStoreId = "store",
        });

        ValidateOptionsResult result = new MailchimpOptionsValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Resolve_AllowsSubscriptionOnlyConfigurationWithoutEcommerceStore()
    {
        var options = Options.Create(new MailchimpOptions
        {
            Enabled = true,
            ApiKey = "global-us1",
            AudienceId = "audience",
            Purchases = new MailchimpPurchaseOptions { Enabled = false },
        });
        var resolver = new MailchimpConfigurationResolver(options);

        MailchimpStoreConfiguration configuration = resolver.Resolve("default");

        Assert.Equal(string.Empty, configuration.EcommerceStoreId);
    }
}

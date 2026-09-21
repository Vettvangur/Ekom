using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ekom.Mailchimp.Tests;

public sealed class MailchimpConfigurationTests
{
    [Fact]
    public void ResolveGlobal_IgnoresStoreOverrides()
    {
        var options = new MailchimpOptions
        {
            Enabled = true,
            ApiKey = "global-us1",
            AudienceId = "global-audience",
        };
        options.Stores.Add(new MailchimpStoreOptions
        {
            Alias = "is",
            ApiKey = "store-us20",
            AudienceId = "store-audience",
        });
        var resolver = new MailchimpConfigurationResolver(
            Options.Create(options),
            NullLogger<MailchimpConfigurationResolver>.Instance);

        MailchimpStoreConfiguration configuration = resolver.ResolveGlobal();

        Assert.Equal("global-us1", configuration.ApiKey);
        Assert.Equal("us1", configuration.ServerPrefix);
        Assert.Equal("global-audience", configuration.AudienceId);
    }

    [Fact]
    public void ResolveGlobal_MissingGlobalConfigurationThrows()
    {
        var resolver = new MailchimpConfigurationResolver(
            Options.Create(new MailchimpOptions { Enabled = true }),
            NullLogger<MailchimpConfigurationResolver>.Instance);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => resolver.ResolveGlobal());

        Assert.Equal("Global Mailchimp configuration is incomplete.", exception.Message);
    }

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
        var resolver = new MailchimpConfigurationResolver(
            options,
            NullLogger<MailchimpConfigurationResolver>.Instance);

        MailchimpStoreConfiguration configuration = resolver.Resolve("IS");

        Assert.Equal("override-us20", configuration.ApiKey);
        Assert.Equal("us20", configuration.ServerPrefix);
        Assert.Equal("is-audience", configuration.AudienceId);
        Assert.Equal("is-store", configuration.EcommerceStoreId);
    }

    [Fact]
    public void Resolve_UsesStoreApiKeyWithGlobalIds()
    {
        var mailchimpOptions = new MailchimpOptions
        {
            Enabled = true,
            AudienceId = "global-audience",
            EcommerceStoreId = "global-store",
        };
        mailchimpOptions.Stores.Add(new MailchimpStoreOptions
        {
            Alias = "HVerslun",
            ApiKey = "store-key-us21",
        });
        var resolver = new MailchimpConfigurationResolver(
            Options.Create(mailchimpOptions),
            NullLogger<MailchimpConfigurationResolver>.Instance);

        MailchimpStoreConfiguration configuration = resolver.Resolve("hverslun", requireEcommerceStore: true);

        Assert.Equal("store-key-us21", configuration.ApiKey);
        Assert.Equal("us21", configuration.ServerPrefix);
        Assert.Equal("global-audience", configuration.AudienceId);
        Assert.Equal("global-store", configuration.EcommerceStoreId);
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
        var resolver = new MailchimpConfigurationResolver(
            options,
            NullLogger<MailchimpConfigurationResolver>.Instance);

        MailchimpStoreConfiguration configuration = resolver.Resolve("default");

        Assert.Equal(string.Empty, configuration.EcommerceStoreId);
    }

    [Fact]
    public void Validate_AllowsMissingOperationalConfiguration()
    {
        var options = new MailchimpOptions
        {
            Enabled = true,
        };

        ValidateOptionsResult result = new MailchimpOptionsValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_RejectsDuplicateStoreAliasesCaseInsensitively()
    {
        var options = new MailchimpOptions
        {
            Enabled = true,
        };
        options.Stores.Add(new MailchimpStoreOptions { Alias = "is" });
        options.Stores.Add(new MailchimpStoreOptions { Alias = "IS" });

        ValidateOptionsResult result = new MailchimpOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, x => x.Contains("configured more than once", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsInvalidDispatcherConfiguration()
    {
        var options = new MailchimpOptions();
        options.Dispatching.MaxQueueSize = 0;

        ValidateOptionsResult result = new MailchimpOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, x => x.Contains("MaxQueueSize", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsTransactionalMessageLimitAtMandrillLimit()
    {
        var options = new MailchimpOptions();
        options.Transactional.Dispatching.MaxMessageBytes = 10_000_000;

        ValidateOptionsResult result = new MailchimpOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, x => x.Contains("MaxMessageBytes", StringComparison.Ordinal));
    }

    [Fact]
    public void TryResolve_MissingEcommerceStore_AllowsAudienceButRejectsPurchase()
    {
        var options = Options.Create(new MailchimpOptions
        {
            Enabled = true,
            ApiKey = "global-us1",
            AudienceId = "audience",
        });
        var resolver = new MailchimpConfigurationResolver(
            options,
            NullLogger<MailchimpConfigurationResolver>.Instance);

        bool audienceReady = resolver.TryResolve("default", requireEcommerceStore: false, out _);
        bool purchaseReady = resolver.TryResolve("default", requireEcommerceStore: true, out _);

        Assert.True(audienceReady);
        Assert.False(purchaseReady);
    }

    [Fact]
    public void TryResolve_InvalidStoreDoesNotAffectValidStore()
    {
        var options = new MailchimpOptions
        {
            Enabled = true,
        };
        options.Stores.Add(new MailchimpStoreOptions
        {
            Alias = "valid",
            ApiKey = "key-us1",
            AudienceId = "audience",
            EcommerceStoreId = "store",
        });
        options.Stores.Add(new MailchimpStoreOptions { Alias = "invalid" });
        var resolver = new MailchimpConfigurationResolver(
            Options.Create(options),
            NullLogger<MailchimpConfigurationResolver>.Instance);

        bool invalidReady = resolver.TryResolve("invalid", requireEcommerceStore: true, out _);
        bool validReady = resolver.TryResolve("VALID", requireEcommerceStore: true, out _);

        Assert.False(invalidReady);
        Assert.True(validReady);
    }

    [Fact]
    public async Task StartupService_LogsEachMissingSettingOnce()
    {
        var options = Options.Create(new MailchimpOptions
        {
            Enabled = true,
        });
        var logger = new RecordingLogger<MailchimpConfigurationResolver>();
        var resolver = new MailchimpConfigurationResolver(options, logger);
        var transactionalResolver = new MailchimpTransactionalConfigurationResolver(
            options,
            NullLogger<MailchimpTransactionalConfigurationResolver>.Instance);
        var service = new MailchimpConfigurationStartupService(options, resolver, transactionalResolver);

        await service.StartAsync(CancellationToken.None);
        await service.StartAsync(CancellationToken.None);
        resolver.TryResolve("default", requireEcommerceStore: true, out _);

        Assert.Equal(4, logger.Errors.Count);
        Assert.Contains(logger.Errors, x => x.Contains("ApiKey", StringComparison.Ordinal));
        Assert.Contains(logger.Errors, x => x.Contains("ServerPrefix", StringComparison.Ordinal));
        Assert.Contains(logger.Errors, x => x.Contains("AudienceId", StringComparison.Ordinal));
        Assert.Contains(logger.Errors, x => x.Contains("EcommerceStoreId", StringComparison.Ordinal));
    }

    [Fact]
    public void TransactionalResolver_UsesStoreOverridesAndGlobalFallbacks()
    {
        var mailchimpOptions = new MailchimpOptions
        {
            Enabled = true,
            Transactional = new MailchimpTransactionalOptions
            {
                Enabled = true,
                ApiKey = "global-transactional-key",
                DefaultFromEmail = "orders@example.com",
                DefaultFromName = "Global Store",
            },
        };
        mailchimpOptions.Stores.Add(new MailchimpStoreOptions
        {
            Alias = "is",
            Transactional = new MailchimpStoreTransactionalOptions
            {
                ApiKey = "is-transactional-key",
                DefaultFromName = "Iceland Store",
            },
        });
        var resolver = new MailchimpTransactionalConfigurationResolver(
            Options.Create(mailchimpOptions),
            NullLogger<MailchimpTransactionalConfigurationResolver>.Instance);

        bool resolved = resolver.TryResolve("IS", out MailchimpTransactionalStoreConfiguration? configuration);

        Assert.True(resolved);
        Assert.NotNull(configuration);
        Assert.Equal("is-transactional-key", configuration.ApiKey);
        Assert.Equal("orders@example.com", configuration.DefaultFromEmail);
        Assert.Equal("Iceland Store", configuration.DefaultFromName);
    }

    [Fact]
    public void TransactionalResolver_StoreCanEnableTransactionalFeature()
    {
        var mailchimpOptions = new MailchimpOptions { Enabled = true };
        mailchimpOptions.Stores.Add(new MailchimpStoreOptions
        {
            Alias = "is",
            Transactional = new MailchimpStoreTransactionalOptions
            {
                Enabled = true,
                ApiKey = "is-transactional-key",
            },
        });
        var resolver = new MailchimpTransactionalConfigurationResolver(
            Options.Create(mailchimpOptions),
            NullLogger<MailchimpTransactionalConfigurationResolver>.Instance);

        bool resolved = resolver.TryResolve("is", out _);

        Assert.True(resolved);
    }

    [Fact]
    public void TransactionalResolver_BlankStoreValuesUseGlobalFallbacks()
    {
        var mailchimpOptions = new MailchimpOptions
        {
            Enabled = true,
            Transactional = new MailchimpTransactionalOptions
            {
                Enabled = true,
                ApiKey = "global-key",
                DefaultFromEmail = "orders@example.com",
            },
        };
        mailchimpOptions.Stores.Add(new MailchimpStoreOptions
        {
            Alias = "is",
            Transactional = new MailchimpStoreTransactionalOptions
            {
                ApiKey = " ",
                DefaultFromEmail = " ",
            },
        });
        var resolver = new MailchimpTransactionalConfigurationResolver(
            Options.Create(mailchimpOptions),
            NullLogger<MailchimpTransactionalConfigurationResolver>.Instance);

        bool resolved = resolver.TryResolve("is", out MailchimpTransactionalStoreConfiguration? configuration);

        Assert.True(resolved);
        Assert.NotNull(configuration);
        Assert.Equal("global-key", configuration.ApiKey);
        Assert.Equal("orders@example.com", configuration.DefaultFromEmail);
    }

    [Fact]
    public async Task TryResolve_LogsMissingSettingsOnceAcrossConcurrentCalls()
    {
        var options = Options.Create(new MailchimpOptions
        {
            Enabled = true,
        });
        var logger = new RecordingLogger<MailchimpConfigurationResolver>();
        var resolver = new MailchimpConfigurationResolver(options, logger);

        Task[] calls = Enumerable.Range(0, 20)
            .Select(index => Task.Run(() => resolver.TryResolve(
                index % 2 == 0 ? "default" : "DEFAULT",
                requireEcommerceStore: true,
                out _)))
            .ToArray();
        await Task.WhenAll(calls);

        Assert.Equal(4, logger.Errors.Count);
    }
}

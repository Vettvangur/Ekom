using Ekom.Mailchimp.Dispatching;
using Ekom.Mailchimp.Models;
using Ekom.Mailchimp.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ekom.Mailchimp.Tests;

public sealed class MailchimpServiceTests
{
    [Fact]
    public async Task SubscribeAsync_MissingAudienceConfigurationDoesNotEnqueue()
    {
        var options = new MailchimpOptions { Enabled = true };
        var dispatcher = new RecordingMailchimpDispatcher();
        MailchimpService service = CreateService(options, dispatcher);

        await service.SubscribeAsync(new MailchimpSubscribeRequest
        {
            StoreAlias = "default",
            Email = "person@example.com",
            Status = MailchimpSubscriptionStatus.Pending,
        });

        Assert.Empty(dispatcher.Items);
    }

    [Fact]
    public async Task MissingEcommerceStore_AllowsSubscriptionButSkipsPurchase()
    {
        var options = new MailchimpOptions
        {
            Enabled = true,
            ApiKey = "key-us1",
            AudienceId = "audience",
        };
        var dispatcher = new RecordingMailchimpDispatcher();
        MailchimpService service = CreateService(options, dispatcher);

        await service.SubscribeAsync(new MailchimpSubscribeRequest
        {
            StoreAlias = "default",
            Email = "person@example.com",
            Status = MailchimpSubscriptionStatus.Pending,
        });
        await service.TrackPurchaseAsync(CreatePurchase("default"));

        Assert.Collection(dispatcher.Items, item => Assert.IsType<SubscribeWorkItem>(item));
    }

    [Fact]
    public async Task InvalidStoreDoesNotPreventValidStoreFromEnqueueing()
    {
        var options = new MailchimpOptions { Enabled = true };
        options.Stores.Add(new MailchimpStoreOptions
        {
            Alias = "valid",
            ApiKey = "key-us1",
            AudienceId = "audience",
            EcommerceStoreId = "store",
        });
        options.Stores.Add(new MailchimpStoreOptions { Alias = "invalid" });
        var dispatcher = new RecordingMailchimpDispatcher();
        MailchimpService service = CreateService(options, dispatcher);

        await service.TrackPurchaseAsync(CreatePurchase("invalid"));
        await service.TrackPurchaseAsync(CreatePurchase("valid"));

        Assert.Collection(dispatcher.Items, item => Assert.IsType<PurchaseWorkItem>(item));
    }

    [Fact]
    public async Task StoreApiKeyWithGlobalIds_EnqueuesSubscriptionsAndPurchases()
    {
        var options = new MailchimpOptions
        {
            Enabled = true,
            AudienceId = "global-audience",
            EcommerceStoreId = "global-store",
        };
        options.Stores.Add(new MailchimpStoreOptions
        {
            Alias = "HVerslun",
            ApiKey = "store-key-us21",
        });
        var dispatcher = new RecordingMailchimpDispatcher();
        MailchimpService service = CreateService(options, dispatcher);

        await service.SubscribeAsync(new MailchimpSubscribeRequest
        {
            StoreAlias = "hverslun",
            Email = "person@example.com",
            Status = MailchimpSubscriptionStatus.Pending,
        });
        await service.TrackPurchaseAsync(CreatePurchase("HVERSLUN"));

        Assert.Collection(
            dispatcher.Items,
            item => Assert.IsType<SubscribeWorkItem>(item),
            item => Assert.IsType<PurchaseWorkItem>(item));
    }

    private static MailchimpService CreateService(
        MailchimpOptions options,
        IMailchimpDispatcher dispatcher)
    {
        var wrappedOptions = Options.Create(options);
        var resolver = new MailchimpConfigurationResolver(
            wrappedOptions,
            NullLogger<MailchimpConfigurationResolver>.Instance);
        return new MailchimpService(wrappedOptions, resolver, dispatcher, []);
    }

    private static MailchimpPurchase CreatePurchase(string storeAlias) => new()
    {
        StoreAlias = storeAlias,
        StoreName = "Store",
        OrderId = "order-1",
        CurrencyCode = "USD",
        OrderTotal = 10,
        ProcessedAt = DateTimeOffset.UtcNow,
        Customer = new MailchimpPurchaseCustomer
        {
            Id = "customer-1",
            Email = "person@example.com",
        },
        Lines =
        [
            new MailchimpPurchaseLine
            {
                Id = "line-1",
                ProductId = "product-1",
                ProductVariantId = "variant-1",
                ProductTitle = "Product",
                VariantTitle = "Variant",
                Quantity = 1,
                Price = 10,
            },
        ],
    };

    private sealed class RecordingMailchimpDispatcher : IMailchimpDispatcher
    {
        public List<MailchimpWorkItem> Items { get; } = [];

        public ValueTask EnqueueAsync(MailchimpWorkItem item, CancellationToken ct)
        {
            Items.Add(item);
            return ValueTask.CompletedTask;
        }
    }
}

using Ekom.Mailchimp.Dispatching;
using Ekom.Mailchimp.Exceptions;
using Ekom.Mailchimp.Models;
using Microsoft.Extensions.Options;

namespace Ekom.Mailchimp.Tests;

public sealed class MailchimpSubscriptionRateLimiterTests
{
    [Fact]
    public void ActionsAndConfiguredStoresHaveIndependentBudgets()
    {
        var options = CreateOptions();
        options.Stores.Add(new MailchimpStoreOptions { Alias = "first" });
        options.Stores.Add(new MailchimpStoreOptions { Alias = "second" });
        using var limiter = new MailchimpSubscriptionRateLimiter(options);

        limiter.Acquire(Subscribe("first"));
        var exception = Assert.Throws<MailchimpRateLimitExceededException>(() => limiter.Acquire(Subscribe("FIRST")));
        Assert.Equal("subscribe", exception.Action);
        Assert.Equal("FIRST", exception.StoreAlias);
        Assert.True(exception.RetryAfter > TimeSpan.Zero);
        limiter.Acquire(Subscribe("second"));
        limiter.Acquire(Unsubscribe("first"));
        Assert.Throws<MailchimpRateLimitExceededException>(() => limiter.Acquire(Unsubscribe("first")));
    }

    [Fact]
    public void UnconfiguredAliasesShareFallbackBudget()
    {
        using var limiter = new MailchimpSubscriptionRateLimiter(CreateOptions());
        limiter.Acquire(Subscribe("unknown-one"));
        Assert.Throws<MailchimpRateLimitExceededException>(() => limiter.Acquire(Subscribe("unknown-two")));
        limiter.Acquire(Unsubscribe("unknown-two"));
    }

    [Fact]
    public void LimitsCanBeDisabledIndependently()
    {
        var options = CreateOptions();
        options.Subscriptions.SubscribeRateLimit.Enabled = false;
        using var limiter = new MailchimpSubscriptionRateLimiter(options);
        for (var index = 0; index < 100; index++) limiter.Acquire(Subscribe("store"));
        limiter.Acquire(Unsubscribe("store"));
        Assert.Throws<MailchimpRateLimitExceededException>(() => limiter.Acquire(Unsubscribe("store")));
    }

    [Fact]
    public async Task ConcurrentRequestsCannotExceedBudget()
    {
        var options = CreateOptions();
        options.Subscriptions.SubscribeRateLimit.PermitLimit = 10;
        using var limiter = new MailchimpSubscriptionRateLimiter(options);
        var accepted = 0;
        await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
        {
            try
            {
                limiter.Acquire(Subscribe("store"));
                Interlocked.Increment(ref accepted);
            }
            catch (MailchimpRateLimitExceededException) { }
        })));
        Assert.Equal(10, accepted);
    }

    [Fact]
    public async Task BudgetReplenishesAfterWindow()
    {
        var options = CreateOptions();
        options.Subscriptions.SubscribeRateLimit.WindowSeconds = 1;
        using var limiter = new MailchimpSubscriptionRateLimiter(options);
        limiter.Acquire(Subscribe("store"));
        Assert.Throws<MailchimpRateLimitExceededException>(() => limiter.Acquire(Subscribe("store")));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            await Task.Delay(50, timeout.Token);
            try
            {
                limiter.Acquire(Subscribe("store"));
                break;
            }
            catch (MailchimpRateLimitExceededException) { }
        }
    }

    [Fact]
    public void ExhaustedSubscriptionBudgetDoesNotLimitPurchases()
    {
        using var limiter = new MailchimpSubscriptionRateLimiter(CreateOptions());
        limiter.Acquire(Subscribe("store"));
        limiter.Acquire(Unsubscribe("store"));
        var purchase = new PurchaseWorkItem(new MailchimpPurchase
        {
            StoreAlias = "store",
            StoreName = "Store",
            OrderId = "order",
            CurrencyCode = "ISK",
            OrderTotal = 100,
            ProcessedAt = DateTimeOffset.UtcNow,
            Customer = new MailchimpPurchaseCustomer { Id = "customer", Email = "person@example.com" },
        });
        for (var index = 0; index < 100; index++) limiter.Acquire(purchase);
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(1, 0)]
    [InlineData(-1, 60)]
    public void InvalidLimitsFailValidation(int permits, int seconds)
    {
        var options = CreateOptions();
        options.Subscriptions.SubscribeRateLimit.PermitLimit = permits;
        options.Subscriptions.UnsubscribeRateLimit.WindowSeconds = seconds;
        Assert.True(new MailchimpOptionsValidator().Validate(Options.DefaultName, options).Failed);
    }

    private static MailchimpOptions CreateOptions() => new()
    {
        Subscriptions = new MailchimpSubscriptionOptions
        {
            SubscribeRateLimit = new MailchimpActionRateLimitOptions { PermitLimit = 1 },
            UnsubscribeRateLimit = new MailchimpActionRateLimitOptions { PermitLimit = 1 },
        },
    };

    private static SubscribeWorkItem Subscribe(string alias) => new(new MailchimpSubscribeRequest
    {
        StoreAlias = alias,
        Email = "person@example.com",
        Status = MailchimpSubscriptionStatus.Pending,
    });

    private static UnsubscribeWorkItem Unsubscribe(string alias) => new(new MailchimpUnsubscribeRequest
    {
        StoreAlias = alias,
        Email = "person@example.com",
    });
}

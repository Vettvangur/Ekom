using Ekom.Mailchimp.Clients;
using Ekom.Mailchimp.Dispatching;
using Ekom.Mailchimp.Exceptions;
using Ekom.Mailchimp.Models;
using Microsoft.Extensions.Options;
using System.Net;

namespace Ekom.Mailchimp.Tests;

public sealed class MailchimpDispatcherTests
{
    [Fact]
    public async Task Dispatcher_LogsSanitizedMailchimpApiError()
    {
        const string email = "person@example.com";
        const string subscriberHash = "9e5472907687e54299815f8f1bc8cadd";
        var attempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var audienceClient = new FailingAudienceClient(() =>
        {
            attempted.TrySetResult();
            throw new MailchimpApiException(
                HttpStatusCode.BadRequest,
                $"lists/audience/members/{subscriberHash}",
                $$"""{"title":"Invalid Resource","detail":"Email {{email}} and {{subscriberHash}} are invalid.","errors":[{"field":"email_address"}]}""");
        });
        var logger = new RecordingLogger<MailchimpDispatcher>();
        using var dispatcher = new MailchimpDispatcher(
            Options.Create(new MailchimpOptions
            {
                Dispatching = new MailchimpDispatcherOptions
                {
                    MaxConcurrency = 1,
                    MaxAttempts = 1,
                    MaxQueueSize = 10,
                }
            }),
            audienceClient,
            new NoopEcommerceClient(),
            logger);

        await dispatcher.StartAsync(CancellationToken.None);
        await dispatcher.EnqueueAsync(new SubscribeWorkItem(new MailchimpSubscribeRequest
        {
            StoreAlias = "HVERSLUN",
            Email = email,
            Status = MailchimpSubscriptionStatus.Pending,
        }), CancellationToken.None);
        await attempted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(SpinWait.SpinUntil(() => !logger.Errors.IsEmpty, TimeSpan.FromSeconds(5)));
        await dispatcher.StopAsync(CancellationToken.None);

        string error = Assert.Single(logger.Errors);
        Assert.Contains("Invalid Resource", error, StringComparison.Ordinal);
        Assert.Contains("[redacted-email]", error, StringComparison.Ordinal);
        Assert.Contains("[redacted-subscriber-hash]", error, StringComparison.Ordinal);
        Assert.Contains("email_address", error, StringComparison.Ordinal);
        Assert.DoesNotContain(email, error, StringComparison.Ordinal);
        Assert.DoesNotContain(subscriberHash, error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdmissionRejectsExcessSubscriptionsWithoutLoggingErrors()
    {
        var options = new MailchimpOptions();
        options.Subscriptions.SubscribeRateLimit.PermitLimit = 1;
        var logger = new RecordingLogger<MailchimpDispatcher>();
        using var dispatcher = new MailchimpDispatcher(Options.Create(options),
            new FailingAudienceClient(() => { }), new NoopEcommerceClient(), logger);
        var item = new SubscribeWorkItem(new MailchimpSubscribeRequest
        {
            StoreAlias = "store",
            Email = "person@example.com",
            Status = MailchimpSubscriptionStatus.Pending,
        });
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => dispatcher.EnqueueAsync(item, cancelled.Token).AsTask());
        await dispatcher.EnqueueAsync(item, CancellationToken.None);
        await Assert.ThrowsAsync<MailchimpRateLimitExceededException>(() => dispatcher.EnqueueAsync(item, CancellationToken.None).AsTask());
        await dispatcher.EnqueueAsync(new UnsubscribeWorkItem(new MailchimpUnsubscribeRequest
        {
            StoreAlias = "store",
            Email = "person@example.com",
        }), CancellationToken.None);
        Assert.Empty(logger.Errors);
    }

    [Fact]
    public async Task FullQueueDoesNotSilentlyLoseUnsubscribe()
    {
        var options = new MailchimpOptions();
        options.Dispatching.MaxQueueSize = 1;
        using var dispatcher = new MailchimpDispatcher(Options.Create(options),
            new FailingAudienceClient(() => { }), new NoopEcommerceClient(), new RecordingLogger<MailchimpDispatcher>());
        var item = new UnsubscribeWorkItem(new MailchimpUnsubscribeRequest
        {
            StoreAlias = "store",
            Email = "person@example.com",
        });
        await dispatcher.EnqueueAsync(item, CancellationToken.None);
        await Assert.ThrowsAsync<MailchimpSubscriptionQueueFullException>(() => dispatcher.EnqueueAsync(item, CancellationToken.None).AsTask());
    }

    private sealed class FailingAudienceClient : IMailchimpAudienceClient
    {
        private readonly Action _subscribe;

        public FailingAudienceClient(Action subscribe)
        {
            _subscribe = subscribe;
        }

        public Task<IReadOnlyList<MailchimpTag>> GetTagsAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<MailchimpTag>>([]);

        public Task SubscribeAsync(MailchimpSubscribeRequest request, CancellationToken ct)
        {
            _subscribe();
            return Task.CompletedTask;
        }

        public Task UnsubscribeAsync(MailchimpUnsubscribeRequest request, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed class NoopEcommerceClient : IMailchimpEcommerceClient
    {
        public Task TrackPurchaseAsync(MailchimpPurchase purchase, CancellationToken ct)
            => Task.CompletedTask;
    }
}

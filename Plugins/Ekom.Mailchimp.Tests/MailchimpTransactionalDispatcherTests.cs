using Ekom.Mailchimp.Clients;
using Ekom.Mailchimp.Dispatching;
using Ekom.Mailchimp.Exceptions;
using Ekom.Mailchimp.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;

namespace Ekom.Mailchimp.Tests;

public sealed class MailchimpTransactionalDispatcherTests
{
    [Fact]
    public void TryEnqueue_FullQueueReturnsFalseWithoutWaiting()
    {
        var client = new RecordingClient((_, _) => Task.FromResult(SentResults()));
        using MailchimpTransactionalDispatcher dispatcher = CreateDispatcher(client, maxQueueSize: 1);

        bool first = dispatcher.TryEnqueue(CreateWorkItem("first"));
        bool second = dispatcher.TryEnqueue(CreateWorkItem("second"));

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public void TryEnqueue_ByteBudgetExceededReturnsFalseWithoutWaiting()
    {
        var client = new RecordingClient((_, _) => Task.FromResult(SentResults()));
        using MailchimpTransactionalDispatcher dispatcher = CreateDispatcher(
            client,
            maxQueueSize: 10,
            maxQueueBytes: 100);

        bool first = dispatcher.TryEnqueue(CreateWorkItem("first", payloadBytes: 60));
        bool second = dispatcher.TryEnqueue(CreateWorkItem("second", payloadBytes: 60));

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public async Task Dispatcher_ExplicitServerFailureIsRetried()
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new RecordingClient((attempt, _) =>
        {
            if (attempt == 1)
            {
                throw new MailchimpTransactionalApiException(
                    HttpStatusCode.ServiceUnavailable,
                    "messages/send.json",
                    "ServiceUnavailable",
                    "Try again");
            }

            completed.TrySetResult();
            return Task.FromResult(SentResults());
        });
        using MailchimpTransactionalDispatcher dispatcher = CreateDispatcher(client);

        await dispatcher.StartAsync(CancellationToken.None);
        dispatcher.TryEnqueue(CreateWorkItem("retry"));
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await dispatcher.StopAsync(CancellationToken.None);

        Assert.Equal(2, client.Attempts);
    }

    [Fact]
    public async Task Dispatcher_TimeoutIsNotRetried()
    {
        var attempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new RecordingClient((_, _) =>
        {
            attempted.TrySetResult();
            throw new TimeoutException("Ambiguous timeout");
        });
        using MailchimpTransactionalDispatcher dispatcher = CreateDispatcher(client);

        await dispatcher.StartAsync(CancellationToken.None);
        dispatcher.TryEnqueue(CreateWorkItem("timeout"));
        await attempted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(100);
        await dispatcher.StopAsync(CancellationToken.None);

        Assert.Equal(1, client.Attempts);
    }

    private static MailchimpTransactionalDispatcher CreateDispatcher(
        IMailchimpTransactionalClient client,
        int maxQueueSize = 10,
        long maxQueueBytes = 1_024)
    {
        var options = new MailchimpOptions();
        options.Transactional.Dispatching.MaxQueueSize = maxQueueSize;
        options.Transactional.Dispatching.MaxQueueBytes = maxQueueBytes;
        options.Transactional.Dispatching.MaxConcurrency = 1;
        options.Transactional.Dispatching.MaxAttempts = 2;
        options.Transactional.Dispatching.InitialRetryDelaySeconds = 0;
        return new MailchimpTransactionalDispatcher(
            Options.Create(options),
            client,
            NullLogger<MailchimpTransactionalDispatcher>.Instance);
    }

    private static TransactionalMessageWorkItem CreateWorkItem(string correlationId, long payloadBytes = 10)
        => new(new MailchimpTransactionalMessage
        {
            StoreAlias = "default",
            CorrelationId = correlationId,
            FromEmail = "orders@example.com",
            Subject = "Receipt",
            Text = "Thanks",
            Recipients = [new MailchimpTransactionalRecipient { Email = "person@example.com" }],
        }, payloadBytes);

    private static IReadOnlyList<MailchimpTransactionalSendResult> SentResults()
        => [new("person@example.com", "sent", null, "message-id")];

    private sealed class RecordingClient : IMailchimpTransactionalClient
    {
        private readonly Func<int, CancellationToken, Task<IReadOnlyList<MailchimpTransactionalSendResult>>> _send;

        public RecordingClient(
            Func<int, CancellationToken, Task<IReadOnlyList<MailchimpTransactionalSendResult>>> send)
        {
            _send = send;
        }

        public int Attempts { get; private set; }

        public Task<IReadOnlyList<MailchimpTransactionalSendResult>> SendAsync(
            MailchimpTransactionalMessage message,
            CancellationToken ct)
        {
            Attempts++;
            return _send(Attempts, ct);
        }

        public Task<IReadOnlyList<MailchimpTransactionalSendResult>> SendTemplateAsync(
            MailchimpTransactionalTemplateMessage message,
            CancellationToken ct)
        {
            Attempts++;
            return _send(Attempts, ct);
        }
    }
}

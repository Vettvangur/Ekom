using Ekom.Brevo.Dispatching;
using Ekom.Brevo.Events;
using Ekom.Brevo.Models;
using Ekom.Brevo.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json.Nodes;
using Xunit;

namespace Ekom.Brevo.Tests;

public sealed class BrevoTests
{
    private static BrevoContact Contact => new() { StoreAlias = "shop", Email = "customer@example.com", FirstName = "First" };
    private static BrevoTransactionalMessage Email => new()
    {
        StoreAlias = "shop", To = [new("customer@example.com")], Subject = "Order", TextContent = "Thank you",
    };

    internal static IHost CreateHost(RecordingHandler handler, Action<BrevoOptions>? configure = null)
        => Host.CreateDefaultBuilder().ConfigureLogging(x => x.ClearProviders()).ConfigureServices(services =>
        {
            services.AddBrevo(options =>
            {
                options.Enabled = true;
                options.ApiKey = "global-key";
                options.Subscriptions.DefaultListId = 12;
                options.Subscriptions.DoubleOptInTemplateId = 42;
                options.Subscriptions.DoubleOptInRedirectUrl = new Uri("https://shop.example.com/confirmed");
                options.Transactional.Enabled = true;
                options.Transactional.DefaultFromEmail = "sender@example.com";
                options.Dispatching.InitialRetryDelaySeconds = 1;
                options.Transactional.Dispatching.InitialRetryDelaySeconds = 1;
                configure?.Invoke(options);
            });
            services.AddHttpClient("Ekom.Brevo").ConfigurePrimaryHttpMessageHandler(() => handler);
        }).Build();

    internal static async Task<BrevoDeliveryResult> Complete(BrevoSubmission submission)
    {
        Assert.Equal(BrevoSubmissionStatus.Queued, submission.Status);
        Assert.NotNull(submission.Completion);
        return await submission.Completion.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task UpsertPreservesSuppressionAndUsesStoreKey()
    {
        using var handler = new RecordingHandler();
        using var host = CreateHost(handler, o => o.Stores.Add(new() { Alias = "SHOP", ApiKey = "store-key" }));
        await host.StartAsync();
        var result = await host.Services.GetRequiredService<IBrevoContactsService>().UpsertAsync(Contact);
        Assert.Equal(BrevoDeliveryStatus.Accepted, (await Complete(result)).Status);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("store-key", request.Key);
        Assert.Equal("/v3/contacts", request.Path);
        Assert.True(request.Body!["updateEnabled"]!.GetValue<bool>());
        Assert.Null(request.Body["emailBlacklisted"]);
        Assert.Null(request.Body["smsBlacklisted"]);
        Assert.Null(request.Body["listIds"]);
        await host.StopAsync();
    }

    [Fact]
    public async Task DoubleOptInDoesNotSubscribeBeforeConfirmation()
    {
        using var handler = new RecordingHandler();
        using var host = CreateHost(handler);
        await host.StartAsync();
        await Complete(await host.Services.GetRequiredService<IBrevoContactsService>().SubscribeAsync(Contact, 99));
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/v3/contacts/doubleOptinConfirmation", request.Path);
        Assert.Equal(99, request.Body!["includeListIds"]![0]!.GetValue<int>());
        Assert.Equal(42, request.Body["templateId"]!.GetValue<int>());
        Assert.Null(request.Body["emailBlacklisted"]);
        Assert.Null(request.Body["listIds"]);
        await host.StopAsync();
    }

    [Fact]
    public async Task ExplicitSubscribeAndUnsubscribeHaveDifferentListSemantics()
    {
        using var handler = new RecordingHandler();
        using var host = CreateHost(handler, o => o.Subscriptions.DoubleOptIn = false);
        await host.StartAsync();
        var contacts = host.Services.GetRequiredService<IBrevoContactsService>();
        await Complete(await contacts.SubscribeAsync(Contact));
        await Complete(await contacts.RemoveFromListAsync("shop", Contact.Email, 12));
        await Complete(await contacts.UnsubscribeAsync("shop", Contact.Email));
        Assert.False(handler.Requests[0].Body!["emailBlacklisted"]!.GetValue<bool>());
        Assert.Equal(12, handler.Requests[1].Body!["unlinkListIds"]![0]!.GetValue<int>());
        Assert.Null(handler.Requests[1].Body!["emailBlacklisted"]);
        Assert.True(handler.Requests[2].Body!["emailBlacklisted"]!.GetValue<bool>());
        Assert.Equal(HttpMethod.Put, handler.Requests[2].Method);
        await host.StopAsync();
    }

    [Fact]
    public async Task EmailIsSnapshottedWithAttachmentsAndStoreDefaults()
    {
        using var handler = new RecordingHandler();
        using var host = CreateHost(handler, o => o.Stores.Add(new()
        {
            Alias = "shop", ApiKey = "store-key",
            Transactional = new() { ApiKey = "email-key", DefaultFromEmail = "store@example.com", DefaultReplyTo = "reply@example.com" },
        }));
        byte[] bytes = [1, 2, 3];
        var recipients = new List<BrevoEmailAddress> { new("original@example.com") };
        var submission = await host.Services.GetRequiredService<IBrevoTransactionalService>().SendAsync(Email with
        {
            To = recipients, Cc = [new("cc@example.com")], Bcc = [new("bcc@example.com")], Attachments = [new("invoice.pdf", bytes)],
        });
        bytes[0] = 99;
        recipients.Clear();
        await host.StartAsync();
        var delivery = await Complete(submission);
        Assert.Equal("message-123", delivery.MessageId);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("email-key", request.Key);
        Assert.Equal("/v3/smtp/email", request.Path);
        Assert.Equal("store@example.com", request.Body!["sender"]!["email"]!.GetValue<string>());
        Assert.Equal("original@example.com", request.Body["to"]![0]!["email"]!.GetValue<string>());
        Assert.Equal("AQID", request.Body["attachment"]![0]!["content"]!.GetValue<string>());
        Assert.Equal("reply@example.com", request.Body["replyTo"]!["email"]!.GetValue<string>());
        Assert.NotNull(request.Body["cc"]);
        Assert.NotNull(request.Body["bcc"]);
        await host.StopAsync();
    }

    [Fact]
    public async Task TemplateCanUseBrevoSenderAndSubject()
    {
        using var handler = new RecordingHandler();
        using var host = CreateHost(handler, o => o.Transactional.DefaultFromEmail = null);
        await host.StartAsync();
        var submission = await host.Services.GetRequiredService<IBrevoTransactionalService>().SendAsync(new()
        {
            StoreAlias = "shop", To = [new("customer@example.com")], TemplateId = 7,
            Parameters = new Dictionary<string, object?> { ["ORDER"] = "42" },
        });
        await Complete(submission);
        var body = Assert.Single(handler.Requests).Body!;
        Assert.Equal(7, body["templateId"]!.GetValue<int>());
        Assert.Equal("42", body["params"]!["ORDER"]!.GetValue<string>());
        Assert.Null(body["sender"]);
        Assert.Null(body["subject"]);
        await host.StopAsync();
    }

    [Fact]
    public async Task QueueFullIsExplicitAndQueuesAreIndependent()
    {
        using var handler = new RecordingHandler();
        using var host = CreateHost(handler, o => o.Transactional.Dispatching.MaxQueueSize = 1);
        var emails = host.Services.GetRequiredService<IBrevoTransactionalService>();
        var first = await emails.SendAsync(Email);
        Assert.Equal(BrevoSubmissionStatus.QueueFull, (await emails.SendAsync(Email)).Status);
        var contact = await host.Services.GetRequiredService<IBrevoContactsService>().UpsertAsync(Contact);
        Assert.Equal(BrevoSubmissionStatus.Queued, contact.Status);
        await host.StartAsync();
        await Complete(first);
        await Complete(contact);
        await host.StopAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AmbiguousEmailFailuresAreNotRetried(bool timeout)
    {
        using var handler = new RecordingHandler(_ => timeout ? throw new TaskCanceledException() : new(HttpStatusCode.InternalServerError));
        using var host = CreateHost(handler);
        await host.StartAsync();
        var result = await Complete(await host.Services.GetRequiredService<IBrevoTransactionalService>().SendAsync(Email));
        Assert.Equal(BrevoDeliveryStatus.Unknown, result.Status);
        Assert.Single(handler.Requests);
        await host.StopAsync();
    }

    [Fact]
    public async Task RateLimitIsRetriedButValidationFailureIsNot()
    {
        int attempt = 0;
        using var handler = new RecordingHandler(_ => new(++attempt == 1 ? HttpStatusCode.TooManyRequests : HttpStatusCode.BadRequest));
        using var host = CreateHost(handler);
        await host.StartAsync();
        var result = await Complete(await host.Services.GetRequiredService<IBrevoTransactionalService>().SendAsync(Email));
        Assert.Equal(BrevoDeliveryStatus.Failed, result.Status);
        Assert.Equal(400, result.HttpStatus);
        Assert.Equal(2, handler.Requests.Count);
        await host.StopAsync();
    }

    [Fact]
    public async Task RetryAfterAboveBudgetDoesNotRetryEarly()
    {
        using var handler = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new(TimeSpan.FromHours(1));
            return response;
        });
        using var host = CreateHost(handler);
        await host.StartAsync();
        Assert.Equal(BrevoDeliveryStatus.Failed, (await Complete(await host.Services.GetRequiredService<IBrevoTransactionalService>().SendAsync(Email))).Status);
        Assert.Single(handler.Requests);
        await host.StopAsync();
    }

    [Fact]
    public async Task ListsArePaginatedWithStoreCredentials()
    {
        using var handler = new RecordingHandler(request =>
        {
            var lists = new JsonArray();
            if (request.RequestUri!.Query.Contains("offset=0", StringComparison.Ordinal))
                for (int i = 1; i <= 50; i++) lists.Add(new JsonObject { ["id"] = i, ["name"] = "List " + i });
            return new(HttpStatusCode.OK) { Content = new StringContent(new JsonObject { ["lists"] = lists }.ToJsonString()) };
        });
        using var host = CreateHost(handler);
        var lists = await host.Services.GetRequiredService<IBrevoContactsService>().GetListsAsync("shop");
        Assert.Equal(50, lists.Count);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, x => Assert.Equal("global-key", x.Key));
    }

    [Fact]
    public async Task DisabledAndMissingConfigurationDoNotSend()
    {
        using var handler = new RecordingHandler();
        using var disabled = CreateHost(handler, o => o.Enabled = false);
        Assert.Equal(BrevoSubmissionStatus.Disabled, (await disabled.Services.GetRequiredService<IBrevoTransactionalService>().SendAsync(Email)).Status);
        using var missing = CreateHost(handler, o => o.ApiKey = null);
        Assert.Equal(BrevoSubmissionStatus.ConfigurationMissing, (await missing.Services.GetRequiredService<IBrevoTransactionalService>().SendAsync(Email)).Status);
        Assert.Equal(BrevoSubmissionStatus.Disabled, (await disabled.Services.GetRequiredService<IBrevoContactsService>().SubscribeAsync(Contact)).Status);
        Assert.Equal(BrevoSubmissionStatus.ConfigurationMissing, (await missing.Services.GetRequiredService<IBrevoContactsService>().SubscribeAsync(Contact)).Status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task InvalidPayloadAndCancellationNeverEnterQueue()
    {
        using var handler = new RecordingHandler();
        using var host = CreateHost(handler);
        var emails = host.Services.GetRequiredService<IBrevoTransactionalService>();
        await Assert.ThrowsAsync<ArgumentException>(async () => await emails.SendAsync(Email with { To = [] }));
        await Assert.ThrowsAsync<ArgumentException>(async () => await emails.SendAsync(Email with { TemplateId = 1 }));
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await emails.SendAsync(Email, new CancellationToken(true)));
        await Assert.ThrowsAsync<ArgumentException>(async () => await host.Services.GetRequiredService<IBrevoContactsService>().UpsertAsync(Contact with
        {
            Attributes = new Dictionary<string, object?> { ["EMAIL"] = "different@example.com" },
        }));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task OptionsRejectDuplicateStores()
    {
        using var handler = new RecordingHandler();
        using var host = CreateHost(handler, o => { o.Stores.Add(new() { Alias = "shop" }); o.Stores.Add(new() { Alias = "SHOP" }); });
        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    [Fact]
    public async Task MessageAndQueueByteLimitsAreEnforced()
    {
        using var handler = new RecordingHandler();
        using var host = CreateHost(handler, o =>
        {
            o.Transactional.Dispatching.MaxMessageBytes = 300;
            o.Transactional.Dispatching.MaxQueueBytes = 300;
        });
        var emails = host.Services.GetRequiredService<IBrevoTransactionalService>();
        await Assert.ThrowsAsync<ArgumentException>(async () => await emails.SendAsync(Email with { TextContent = new string('x', 400) }));
        var first = await emails.SendAsync(Email with { TextContent = new string('x', 100) });
        Assert.Equal(BrevoSubmissionStatus.Queued, first.Status);
        Assert.Equal(BrevoSubmissionStatus.QueueFull, (await emails.SendAsync(Email with { TextContent = new string('x', 100) })).Status);
        await host.StartAsync();
        await Complete(first);
        await host.StopAsync();
    }

    [Fact]
    public async Task ShutdownCompletesPendingAndInFlightSubmissions()
    {
        using var handler = new BlockingHandler();
        // A separate host lets this test control the in-flight request precisely.
        using var controlled = Host.CreateDefaultBuilder().ConfigureLogging(x => x.ClearProviders()).ConfigureServices(services =>
        {
            services.AddBrevo(o => { o.Enabled = true; o.ApiKey = "key"; o.Transactional.Enabled = true; o.Transactional.DefaultFromEmail = "sender@example.com"; });
            services.AddHttpClient("Ekom.Brevo").ConfigurePrimaryHttpMessageHandler(() => handler);
        }).Build();
        var emails = controlled.Services.GetRequiredService<IBrevoTransactionalService>();
        var first = await emails.SendAsync(Email);
        var pending = await emails.SendAsync(Email);
        await controlled.StartAsync();
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await controlled.StopAsync();
        Assert.Equal(BrevoDeliveryStatus.Unknown, (await Complete(first)).Status);
        Assert.Equal(BrevoDeliveryStatus.Cancelled, (await Complete(pending)).Status);
        Assert.Equal(BrevoSubmissionStatus.QueueFull, (await emails.SendAsync(Email)).Status);
    }

    [Fact]
    public async Task SafeContactFailureIsRetriedInOrder()
    {
        int attempt = 0;
        using var handler = new RecordingHandler(_ => new(++attempt == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.NoContent));
        using var host = CreateHost(handler);
        var contacts = host.Services.GetRequiredService<IBrevoContactsService>();
        var update = await contacts.UpsertAsync(Contact);
        var unsubscribe = await contacts.UnsubscribeAsync("shop", Contact.Email);
        await host.StartAsync();
        Assert.Equal(BrevoDeliveryStatus.Accepted, (await Complete(update)).Status);
        Assert.Equal(BrevoDeliveryStatus.Accepted, (await Complete(unsubscribe)).Status);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.Equal(HttpMethod.Put, handler.Requests[2].Method);
        await host.StopAsync();
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("on", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public void CheckoutConsentRequiresExplicitOptIn(string? value, bool expected)
        => Assert.Equal(expected, BrevoEkomEvents.HasConsent(value));
}

internal sealed record RecordedRequest(string Path, HttpMethod Method, string Key, JsonNode? Body);
internal sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage>? respond = null) : HttpMessageHandler
{
    public List<RecordedRequest> Requests { get; } = [];
    [SuppressMessage("Reliability", "CA2000", Justification = "Response ownership transfers to the HTTP client caller.")]
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content == null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken));
        lock (Requests) Requests.Add(new(request.RequestUri!.AbsolutePath, request.Method, request.Headers.GetValues("api-key").Single(), body));
        return respond?.Invoke(request) ?? new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent("{\"messageId\":\"message-123\"}"),
        };
    }
}

internal sealed class BlockingHandler : HttpMessageHandler
{
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Started.TrySetResult();
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("Unreachable");
    }
}

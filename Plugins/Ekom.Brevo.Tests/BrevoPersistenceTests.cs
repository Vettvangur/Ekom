using Ekom.Brevo.Models;
using Ekom.Brevo.Persistence;
using Ekom.Brevo.Services;
using Ekom.Brevo.Webhooks;
using LinqToDB;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Ekom.Brevo.Tests;

public sealed class BrevoPersistenceTests
{
    private const string Token = "test-webhook-secret-at-least-32-characters";
    private static BrevoTransactionalMessage Email => new()
    {
        StoreAlias = "shop", OrderId = "order-42", To = [new("customer@example.com")], Subject = "Order", TextContent = "Thanks",
    };
    private static IBrevoTransactionalService Emails(IHost host) => host.Services.GetRequiredService<IBrevoTransactionalService>();
    private static BrevoEmailStore Store(IHost host) => host.Services.GetRequiredService<BrevoEmailStore>();

    [Fact]
    public async Task UnsentEmailSurvivesRestartAndUsesCurrentKey()
    {
        using var database = new BrevoTestDatabase();
        using var handler = new RecordingHandler();
        string id;
        using (var first = BrevoTests.CreateHost(handler, databasePath: database.Path))
            id = (await Emails(first).SendAsync(Email)).CorrelationId;
        using var second = BrevoTests.CreateHost(handler, o => o.ApiKey = "rotated-key", database.Path);
        await second.StartAsync();
        var status = await WaitForAsync(second, id, BrevoQueueState.Accepted);
        Assert.Equal("order-42", status.OrderId);
        Assert.Equal("rotated-key", Assert.Single(handler.Requests).Key);
        await using var db = database.Factory.GetDatabase();
        Assert.Empty((await db.GetTable<BrevoEmailRow>().SingleAsync()).Payload);
        await second.StopAsync();
    }

    [Fact]
    public async Task TwoWorkersSendEachEmailOnceAndCompleteOriginatingTasks()
    {
        using var database = new BrevoTestDatabase();
        using var handler = new RecordingHandler();
        using var first = BrevoTests.CreateHost(handler, databasePath: database.Path);
        using var second = BrevoTests.CreateHost(handler, databasePath: database.Path);
        var submissions = new List<BrevoSubmission>();
        for (int i = 0; i < 8; i++) submissions.Add(await Emails(first).SendAsync(Email));
        await Task.WhenAll(first.StartAsync(), second.StartAsync());
        await Task.WhenAll(submissions.Select(BrevoTests.Complete));
        Assert.Equal(8, handler.Requests.Count);
        Assert.Equal(8, handler.Requests.Select(x => x.Body!["headers"]!["X-Mailin-custom"]!.GetValue<string>()).Distinct().Count());
        await Task.WhenAll(first.StopAsync(), second.StopAsync());
    }

    [Fact]
    public async Task AdmissionIsAtomicAcrossInstancesAndDeduplicationSurvivesRestart()
    {
        using var database = new BrevoTestDatabase();
        using var handler = new RecordingHandler();
        using var first = BrevoTests.CreateHost(handler, o => o.Transactional.Dispatching.MaxQueueSize = 1, database.Path);
        using var second = BrevoTests.CreateHost(handler, o => o.Transactional.Dispatching.MaxQueueSize = 1, database.Path);
        await Store(first).InitializeAsync(default);
        await Store(second).InitializeAsync(default);
        var keyed = Email with { IdempotencyKey = "confirmation:42" };
        var results = await Task.WhenAll(Task.Run(async () => await Emails(first).SendAsync(keyed)), Task.Run(async () => await Emails(second).SendAsync(keyed)));
        Assert.Single(results, x => x.Status == BrevoSubmissionStatus.Queued);
        Assert.Single(results, x => x.Status == BrevoSubmissionStatus.Duplicate);
        Assert.Equal(results[0].CorrelationId, results[1].CorrelationId);
        Assert.Equal(BrevoSubmissionStatus.QueueFull, (await Emails(second).SendAsync(Email)).Status);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await Emails(second).SendAsync(keyed with { TextContent = "changed" }));
        using var restarted = BrevoTests.CreateHost(handler, databasePath: database.Path);
        Assert.Equal(BrevoSubmissionStatus.Duplicate, (await Emails(restarted).SendAsync(keyed)).Status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ExpiredClaimBecomesUnknownAndCannotBeReplayed()
    {
        using var handler = new RecordingHandler();
        using var host = BrevoTests.CreateHost(handler);
        var submission = await Emails(host).SendAsync(Email);
        Assert.Empty(await Store(host).GetPendingCompletionsAsync([submission.CorrelationId], default));
        var row = Assert.IsType<BrevoEmailRow>(await Store(host).ClaimAsync(default));
        await ExpireLeaseAsync(host, row.Id);
        Assert.Null(await Store(host).ClaimAsync(default));
        Assert.Equal(BrevoQueueState.Unknown, (await Store(host).GetAsync("shop", submission.CorrelationId))!.State);
        Assert.False(await Store(host).RetryAsync("shop", submission.CorrelationId));
        Assert.Single(await Store(host).GetFailuresAsync("SHOP"));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task RetryScheduleAndAttemptCountSurviveRestart()
    {
        using var database = new BrevoTestDatabase();
        using var handler = new RecordingHandler();
        string id;
        using (var host = BrevoTests.CreateHost(handler, databasePath: database.Path))
        {
            id = (await Emails(host).SendAsync(Email)).CorrelationId;
            var row = Assert.IsType<BrevoEmailRow>(await Store(host).ClaimAsync(default));
            await Store(host).FinishAsync(row, new(BrevoDeliveryStatus.Failed, HttpStatus: 429), DateTime.UtcNow.AddHours(1), default);
        }
        using var restarted = BrevoTests.CreateHost(handler, databasePath: database.Path);
        Assert.Null(await Store(restarted).ClaimAsync(default));
        await using (var db = database.Factory.GetDatabase())
            await db.GetTable<BrevoEmailRow>().Where(x => x.Id == id).Set(x => x.DueUtc, DateTime.UtcNow.AddSeconds(-1)).UpdateAsync();
        Assert.Equal(2, (await Store(restarted).ClaimAsync(default))!.Attempts);
    }

    [Fact]
    public async Task DefinitiveFailureCanBeRetriedButAcceptedCannot()
    {
        bool reject = true;
        using var handler = new RecordingHandler(_ => new HttpResponseMessage(reject ? HttpStatusCode.BadRequest : HttpStatusCode.Created)
        {
            Content = new StringContent("{\"messageId\":\"message-retry\"}"),
        });
        using var host = BrevoTests.CreateHost(handler);
        await host.StartAsync();
        var submission = await Emails(host).SendAsync(Email);
        Assert.Equal(BrevoDeliveryStatus.Failed, (await BrevoTests.Complete(submission)).Status);
        Assert.False(await Store(host).RetryAsync("other", submission.CorrelationId));
        reject = false;
        Assert.True(await Store(host).RetryAsync("shop", submission.CorrelationId));
        await WaitForAsync(host, submission.CorrelationId, BrevoQueueState.Accepted);
        Assert.False(await Store(host).RetryAsync("shop", submission.CorrelationId));
        Assert.Equal(2, handler.Requests.Count);
        await host.StopAsync();
    }

    [Fact]
    public async Task WebhookBeforeResponseCorrelatesAndDeduplicatesPerRecipientInEventOrder()
    {
        using var handler = new RecordingHandler();
        using var host = BrevoTests.CreateHost(handler, ConfigureWebhooks);
        var submission = await Emails(host).SendAsync(Email);
        string id = submission.CorrelationId;
        string later = Webhook(id, "delivered", 1700000002);
        Assert.Equal(200, await PostAsync(host, later));
        Assert.Equal(200, await PostAsync(host, later));
        Assert.Equal(200, await PostAsync(host, Webhook(id, "request", 1700000001)));
        Assert.Equal(200, await PostAsync(host, Webhook(id, "hard_bounce", 1700000003, "second@example.com")));
        var result = (await Store(host).GetAsync("shop", id))!;
        Assert.Equal("msg@example.com", result.MessageId);
        Assert.Equal(3, result.Events.Count);
        Assert.Equal(new[] { "request", "delivered", "hard_bounce" }, result.Events.Select(x => x.Event));
        Assert.Equal(2, result.Events.Select(x => x.Recipient).Distinct().Count());
        Assert.Null(await Store(host).GetAsync("other", id));
        Assert.Single(await Store(host).GetByOrderAsync("SHOP", "order-42"));
        Assert.Empty(await Store(host).GetByOrderAsync("other", "order-42"));
        Assert.Equal(200, await PostAsync(host, Webhook(id, "spam", 1700000004), storeAlias: "other"));
        Assert.Equal(3, (await Store(host).GetAsync("shop", id))!.Events.Count);
    }

    [Fact]
    public async Task MessageIdCorrelationWorksWithoutCustomHeaderAndIgnoresWebhookIdForDeduplication()
    {
        using var handler = new RecordingHandler();
        using var host = BrevoTests.CreateHost(handler, ConfigureWebhooks);
        string id = (await Emails(host).SendAsync(Email)).CorrelationId;
        var row = (await Store(host).ClaimAsync(default))!;
        await Store(host).FinishAsync(row, new(BrevoDeliveryStatus.Accepted, "<msg@example.com>"), null, default);
        Assert.Equal(200, await PostAsync(host, Webhook(null, "delivered", 1700000001)));
        Assert.Equal(200, await PostAsync(host, Webhook(null, "spam", 1700000002)));
        Assert.Equal(2, (await Store(host).GetAsync("shop", id))!.Events.Count);
    }

    [Fact]
    public async Task WebhooksAuthenticateBeforeReadingAndRejectMalformedOrOversizedBatches()
    {
        using var handler = new RecordingHandler();
        using var host = BrevoTests.CreateHost(handler, ConfigureWebhooks);
        string id = (await Emails(host).SendAsync(Email)).CorrelationId;
        Assert.Equal(401, await PostAsync(host, "invalid", "wrong"));
        Assert.Equal(400, await PostAsync(host, "invalid"));
        Assert.Equal(413, await PostAsync(host, new string('x', 65537)));
        Assert.Equal(400, await PostAsync(host, "[" + Webhook(id, "delivered", 1700000001) + ",{}]"));
        Assert.Empty((await Store(host).GetAsync("shop", id))!.Events);
        Assert.Equal(200, await PostAsync(host, "[" + Webhook(id, "delivered", 1700000001) + "," + Webhook(id, "spam", 1700000002) + "]"));
        Assert.Equal(2, (await Store(host).GetAsync("shop", id))!.Events.Count);
    }

    [Fact]
    public async Task StoreWebhookTokenOverridesGlobalAndWebhooksDefaultToDisabled()
    {
        using var handler = new RecordingHandler();
        using var disabled = BrevoTests.CreateHost(handler);
        Assert.Equal(404, await PostAsync(disabled, "{}"));
        using var host = BrevoTests.CreateHost(handler, o =>
        {
            ConfigureWebhooks(o);
            o.Stores.Add(new() { Alias = "shop", Transactional = new() { WebhookToken = Token + "store" } });
        });
        Assert.Equal(401, await PostAsync(host, "[]"));
        Assert.Equal(200, await PostAsync(host, "[]", Token + "store"));
    }

    [Fact]
    public async Task RetentionRemovesTerminalHistoryButKeepsPendingWork()
    {
        using var handler = new RecordingHandler();
        using var host = BrevoTests.CreateHost(handler, ConfigureWebhooks);
        string terminal = (await Emails(host).SendAsync(Email)).CorrelationId;
        var row = (await Store(host).ClaimAsync(default))!;
        await Store(host).FinishAsync(row, new(BrevoDeliveryStatus.Failed), null, default);
        await PostAsync(host, Webhook(terminal, "blocked", 1700000000));
        string pending = (await Emails(host).SendAsync(Email)).CorrelationId;
        await using var db = host.Services.GetRequiredService<BrevoTestDatabase>().Factory.GetDatabase();
        await db.GetTable<BrevoEmailRow>().Set(x => x.UpdatedUtc, DateTime.UtcNow.AddDays(-31)).UpdateAsync();
        await Store(host).PurgeAsync(default);
        Assert.Null(await Store(host).GetAsync("shop", terminal));
        Assert.NotNull(await Store(host).GetAsync("shop", pending));
        Assert.Empty(await db.GetTable<BrevoEventRow>().ToListAsync());
    }

    private static async Task ExpireLeaseAsync(IHost host, string id)
    {
        await using var db = host.Services.GetRequiredService<BrevoTestDatabase>().Factory.GetDatabase();
        await db.GetTable<BrevoEmailRow>().Where(x => x.Id == id)
            .Set(x => x.LeaseUntilUtc, DateTime.UtcNow.AddMinutes(-1)).UpdateAsync();
    }

    [Fact]
    public async Task WebhookStorageFailureIsNotAcknowledged()
    {
        using var handler = new RecordingHandler();
        using var host = BrevoTests.CreateHost(handler, ConfigureWebhooks);
        string id = (await Emails(host).SendAsync(Email)).CorrelationId;
        await using var db = host.Services.GetRequiredService<BrevoTestDatabase>().Factory.GetDatabase();
        await db.DropTableAsync<BrevoEventRow>();
        await Assert.ThrowsAnyAsync<Exception>(() => PostAsync(host, Webhook(id, "delivered", 1700000001)));
    }

    [Theory]
    [InlineData("short", 120, 30)]
    [InlineData(Token, 10, 30)]
    [InlineData(Token, 120, 0)]
    public async Task InvalidWebhookAndPersistenceSettingsFailStartup(string token, int lease, int retention)
    {
        using var handler = new RecordingHandler();
        using var host = BrevoTests.CreateHost(handler, o =>
        {
            ConfigureWebhooks(o);
            o.Transactional.Webhooks.Token = token;
            o.Transactional.Persistence.LeaseSeconds = lease;
            o.Transactional.Persistence.RetentionDays = retention;
        });
        await Assert.ThrowsAsync<Microsoft.Extensions.Options.OptionsValidationException>(() => host.StartAsync());
    }

    private static void ConfigureWebhooks(BrevoOptions options)
    {
        options.Transactional.Webhooks.Enabled = true;
        options.Transactional.Webhooks.Token = Token;
    }

    private static string Webhook(string? id, string type, long timestamp, string recipient = "customer@example.com")
        => JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["event"] = type, ["email"] = recipient, ["message-id"] = "<msg@example.com>", ["ts_event"] = timestamp,
            ["id"] = 1, ["X-Mailin-custom"] = id == null ? null : "ekom-brevo:" + id,
        });

    private static async Task<int?> PostAsync(IHost host, string json, string token = Token, string storeAlias = "shop")
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer " + token;
        using var body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        context.Request.Body = body;
        var result = await BrevoWebhookEndpoints.HandleAsync(context, storeAlias,
            host.Services.GetRequiredService<BrevoConfiguration>(), Store(host));
        return Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode;
    }

    private static async Task<BrevoEmailStatus> WaitForAsync(IHost host, string id, BrevoQueueState state)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (true)
        {
            var status = await Store(host).GetAsync("shop", id, timeout.Token);
            if (status?.State == state) return status;
            await Task.Delay(50, timeout.Token);
        }
    }
}

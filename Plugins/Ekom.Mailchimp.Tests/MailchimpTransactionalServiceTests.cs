using Ekom.Mailchimp.Dispatching;
using Ekom.Mailchimp.Models;
using Ekom.Mailchimp.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Ekom.Mailchimp.Tests;

public sealed class MailchimpTransactionalServiceTests
{
    [Fact]
    public async Task QueueMessageAsync_ValidRequestQueuesSnapshotWithDefaults()
    {
        MailchimpOptions options = CreateOptions();
        var dispatcher = new RecordingDispatcher();
        MailchimpTransactionalService service = CreateService(options, dispatcher);
        byte[] attachment = [1, 2, 3];
        var request = new MailchimpTransactionalMessage
        {
            StoreAlias = "default",
            Subject = "Receipt",
            Html = "<p>Thanks</p>",
            Recipients = [new MailchimpTransactionalRecipient { Email = "person@example.com" }],
            Attachments =
            [
                new MailchimpTransactionalContent
                {
                    Name = "receipt.pdf",
                    ContentType = "application/pdf",
                    Content = attachment,
                },
            ],
        };

        MailchimpTransactionalEnqueueResult result = await service.QueueMessageAsync(request);
        attachment[0] = 9;

        Assert.Equal(MailchimpTransactionalEnqueueStatus.Queued, result.Status);
        TransactionalMessageWorkItem workItem = Assert.IsType<TransactionalMessageWorkItem>(
            Assert.Single(dispatcher.Items));
        Assert.Equal("orders@example.com", workItem.Message.FromEmail);
        Assert.Equal("Example Store", workItem.Message.FromName);
        Assert.False(string.IsNullOrWhiteSpace(workItem.Message.CorrelationId));
        Assert.Equal(1, workItem.Message.Attachments[0].Content[0]);
    }

    [Fact]
    public async Task QueueTemplateAsync_ValidRequestQueuesTemplate()
    {
        MailchimpOptions options = CreateOptions();
        var dispatcher = new RecordingDispatcher();
        MailchimpTransactionalService service = CreateService(options, dispatcher);

        MailchimpTransactionalEnqueueResult result = await service.QueueTemplateAsync(
            new MailchimpTransactionalTemplateMessage
            {
                StoreAlias = "default",
                TemplateName = "order-confirmation",
                Recipients = [new MailchimpTransactionalRecipient { Email = "person@example.com" }],
                TemplateContent = new Dictionary<string, string> { ["heading"] = "Thanks" },
            });

        Assert.Equal(MailchimpTransactionalEnqueueStatus.Queued, result.Status);
        Assert.IsType<TransactionalTemplateWorkItem>(Assert.Single(dispatcher.Items));
    }

    [Theory]
    [InlineData(false, true, MailchimpTransactionalEnqueueStatus.Disabled)]
    [InlineData(true, false, MailchimpTransactionalEnqueueStatus.ConfigurationMissing)]
    public async Task QueueMessageAsync_UnavailableConfigurationDoesNotQueue(
        bool enabled,
        bool hasApiKey,
        MailchimpTransactionalEnqueueStatus expected)
    {
        MailchimpOptions options = CreateOptions();
        options.Transactional.Enabled = enabled;
        options.Transactional.ApiKey = hasApiKey ? "transactional-key" : null;
        var dispatcher = new RecordingDispatcher();
        MailchimpTransactionalService service = CreateService(options, dispatcher);

        MailchimpTransactionalEnqueueResult result = await service.QueueMessageAsync(CreateMessage());

        Assert.Equal(expected, result.Status);
        Assert.Empty(dispatcher.Items);
    }

    [Fact]
    public async Task QueueMessageAsync_InvalidRequestReturnsErrorsWithoutQueueing()
    {
        var dispatcher = new RecordingDispatcher();
        MailchimpTransactionalService service = CreateService(CreateOptions(), dispatcher);

        MailchimpTransactionalEnqueueResult result = await service.QueueMessageAsync(new MailchimpTransactionalMessage
        {
            StoreAlias = "default",
            Recipients = [new MailchimpTransactionalRecipient { Email = "invalid" }],
        });

        Assert.Equal(MailchimpTransactionalEnqueueStatus.InvalidRequest, result.Status);
        Assert.NotEmpty(result.Errors);
        Assert.Empty(dispatcher.Items);
    }

    [Fact]
    public async Task QueueMessageAsync_FullQueueReturnsQueueFull()
    {
        var dispatcher = new RecordingDispatcher { AcceptItems = false };
        MailchimpTransactionalService service = CreateService(CreateOptions(), dispatcher);

        MailchimpTransactionalEnqueueResult result = await service.QueueMessageAsync(CreateMessage());

        Assert.Equal(MailchimpTransactionalEnqueueStatus.QueueFull, result.Status);
    }

    [Fact]
    public async Task QueueMessageAsync_CancelledTokenReturnsCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var dispatcher = new RecordingDispatcher();
        MailchimpTransactionalService service = CreateService(CreateOptions(), dispatcher);

        MailchimpTransactionalEnqueueResult result = await service.QueueMessageAsync(
            CreateMessage(),
            cancellation.Token);

        Assert.Equal(MailchimpTransactionalEnqueueStatus.Cancelled, result.Status);
        Assert.Empty(dispatcher.Items);
    }

    [Fact]
    public async Task QueueMessageAsync_MutableMergeValueIsSnapshotted()
    {
        var values = new List<string> { "original" };
        var dispatcher = new RecordingDispatcher();
        MailchimpTransactionalService service = CreateService(CreateOptions(), dispatcher);
        MailchimpTransactionalMessage message = CreateMessage() with
        {
            GlobalMergeVariables = new Dictionary<string, object?> { ["VALUES"] = values },
        };

        MailchimpTransactionalEnqueueResult result = await service.QueueMessageAsync(message);
        values[0] = "changed";

        Assert.Equal(MailchimpTransactionalEnqueueStatus.Queued, result.Status);
        TransactionalMessageWorkItem workItem = Assert.IsType<TransactionalMessageWorkItem>(
            Assert.Single(dispatcher.Items));
        JsonElement snapshot = Assert.IsType<JsonElement>(workItem.Message.GlobalMergeVariables["VALUES"]);
        Assert.Equal("original", snapshot[0].GetString());
    }

    [Fact]
    public async Task QueueMessageAsync_InvalidMetadataReturnsInvalidRequest()
    {
        var dispatcher = new RecordingDispatcher();
        MailchimpTransactionalService service = CreateService(CreateOptions(), dispatcher);
        MailchimpTransactionalMessage message = CreateMessage() with
        {
            Metadata = new Dictionary<string, object?> { ["nested"] = new { Value = "unsupported" } },
        };

        MailchimpTransactionalEnqueueResult result = await service.QueueMessageAsync(message);

        Assert.Equal(MailchimpTransactionalEnqueueStatus.InvalidRequest, result.Status);
        Assert.Empty(dispatcher.Items);
    }

    [Fact]
    public async Task QueueMessageAsync_SerializedPayloadOverLimitReturnsInvalidRequest()
    {
        MailchimpOptions options = CreateOptions();
        options.Transactional.Dispatching.MaxMessageBytes = 100;
        var dispatcher = new RecordingDispatcher();
        MailchimpTransactionalService service = CreateService(options, dispatcher);

        MailchimpTransactionalEnqueueResult result = await service.QueueMessageAsync(CreateMessage());

        Assert.Equal(MailchimpTransactionalEnqueueStatus.InvalidRequest, result.Status);
        Assert.Empty(dispatcher.Items);
    }

    [Fact]
    public async Task QueueMessageAsync_NullCollectionReturnsInvalidRequest()
    {
        var dispatcher = new RecordingDispatcher();
        MailchimpTransactionalService service = CreateService(CreateOptions(), dispatcher);
        MailchimpTransactionalMessage message = CreateMessage() with { Tags = null! };

        MailchimpTransactionalEnqueueResult result = await service.QueueMessageAsync(message);

        Assert.Equal(MailchimpTransactionalEnqueueStatus.InvalidRequest, result.Status);
        Assert.Empty(dispatcher.Items);
    }

    private static MailchimpTransactionalService CreateService(
        MailchimpOptions options,
        IMailchimpTransactionalDispatcher dispatcher)
    {
        IOptions<MailchimpOptions> wrappedOptions = Options.Create(options);
        var resolver = new MailchimpTransactionalConfigurationResolver(
            wrappedOptions,
            NullLogger<MailchimpTransactionalConfigurationResolver>.Instance);
        return new MailchimpTransactionalService(wrappedOptions, resolver, dispatcher);
    }

    private static MailchimpOptions CreateOptions() => new()
    {
        Enabled = true,
        Transactional = new MailchimpTransactionalOptions
        {
            Enabled = true,
            ApiKey = "transactional-key",
            DefaultFromEmail = "orders@example.com",
            DefaultFromName = "Example Store",
        },
    };

    private static MailchimpTransactionalMessage CreateMessage() => new()
    {
        StoreAlias = "default",
        Subject = "Receipt",
        Text = "Thanks",
        Recipients = [new MailchimpTransactionalRecipient { Email = "person@example.com" }],
    };

    private sealed class RecordingDispatcher : IMailchimpTransactionalDispatcher
    {
        public bool AcceptItems { get; init; } = true;
        public List<MailchimpTransactionalWorkItem> Items { get; } = [];

        public bool TryEnqueue(MailchimpTransactionalWorkItem item)
        {
            if (AcceptItems)
            {
                Items.Add(item);
            }
            return AcceptItems;
        }
    }
}

using Ekom.Mailchimp.Clients;
using Ekom.Mailchimp.Exceptions;
using Ekom.Mailchimp.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json.Nodes;

namespace Ekom.Mailchimp.Tests;

public sealed class MailchimpTransactionalClientTests
{
    [Fact]
    public async Task SendAsync_SerializesRawMessageAttachmentsAndImages()
    {
        using var handler = new RecordingHandler();
        MailchimpTransactionalClient client = CreateClient(handler);

        IReadOnlyList<MailchimpTransactionalSendResult> results = await client.SendAsync(
            new MailchimpTransactionalMessage
            {
                StoreAlias = "default",
                CorrelationId = "order-123",
                FromEmail = "orders@example.com",
                FromName = "Example Store",
                ReplyTo = "support@example.com",
                Subject = "Receipt",
                Html = "<p>Thanks</p>",
                Text = "Thanks",
                Recipients =
                [
                    new MailchimpTransactionalRecipient
                    {
                        Email = "person@example.com",
                        Name = "Person",
                        Type = MailchimpTransactionalRecipientType.To,
                        MergeVariables = new Dictionary<string, object?> { ["ORDER"] = "123" },
                    },
                ],
                GlobalMergeVariables = new Dictionary<string, object?> { ["STORE"] = "Example" },
                Metadata = new Dictionary<string, object?> { ["order_id"] = "123" },
                Tags = ["receipt"],
                Attachments =
                [
                    new MailchimpTransactionalContent
                    {
                        Name = "receipt.txt",
                        ContentType = "text/plain",
                        Content = [1, 2, 3],
                    },
                ],
                InlineImages =
                [
                    new MailchimpTransactionalContent
                    {
                        Name = "logo",
                        ContentType = "image/png",
                        Content = [4, 5, 6],
                    },
                ],
            },
            CancellationToken.None);

        Assert.Equal("sent", Assert.Single(results).Status);
        Assert.EndsWith("/messages/send.json", handler.RequestUri?.AbsolutePath, StringComparison.Ordinal);
        JsonNode payload = JsonNode.Parse(handler.RequestBody!)!;
        Assert.Equal("transactional-key", payload["key"]!.GetValue<string>());
        Assert.Equal("person@example.com", payload["message"]!["to"]![0]!["email"]!.GetValue<string>());
        Assert.Equal("to", payload["message"]!["to"]![0]!["type"]!.GetValue<string>());
        Assert.Equal("support@example.com", payload["message"]!["headers"]!["Reply-To"]!.GetValue<string>());
        Assert.Equal("AQID", payload["message"]!["attachments"]![0]!["content"]!.GetValue<string>());
        Assert.Equal("BAUG", payload["message"]!["images"]![0]!["content"]!.GetValue<string>());
        Assert.Equal("ORDER", payload["message"]!["merge_vars"]![0]!["vars"]![0]!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task SendTemplateAsync_UsesTemplateEndpointAndContent()
    {
        using var handler = new RecordingHandler();
        MailchimpTransactionalClient client = CreateClient(handler);

        await client.SendTemplateAsync(new MailchimpTransactionalTemplateMessage
        {
            StoreAlias = "default",
            TemplateName = "order-confirmation",
            FromEmail = "orders@example.com",
            Recipients = [new MailchimpTransactionalRecipient { Email = "person@example.com" }],
            TemplateContent = new Dictionary<string, string> { ["heading"] = "Thanks" },
        }, CancellationToken.None);

        Assert.EndsWith("/messages/send-template.json", handler.RequestUri?.AbsolutePath, StringComparison.Ordinal);
        JsonNode payload = JsonNode.Parse(handler.RequestBody!)!;
        Assert.Equal("order-confirmation", payload["template_name"]!.GetValue<string>());
        Assert.Equal("heading", payload["template_content"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("Thanks", payload["template_content"]![0]!["content"]!.GetValue<string>());
    }

    [Fact]
    public async Task SendAsync_ApiErrorThrowsTypedException()
    {
        using var handler = new RecordingHandler
        {
            StatusCode = HttpStatusCode.Unauthorized,
            ResponseBody = """
                { "status": "error", "name": "Invalid_Key", "message": "Invalid API key" }
                """,
        };
        MailchimpTransactionalClient client = CreateClient(handler);

        MailchimpTransactionalApiException exception = await Assert.ThrowsAsync<MailchimpTransactionalApiException>(
            () => client.SendAsync(CreateMessage(), CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal("Invalid_Key", exception.ErrorName);
        Assert.False(exception.IsTransient);
    }

    [Fact]
    public async Task SendAsync_ParsesRecipientRejectionWithoutThrowing()
    {
        using var handler = new RecordingHandler
        {
            ResponseBody = """
                [{ "email": "person@example.com", "status": "rejected", "reject_reason": "hard-bounce", "_id": "id-1" }]
                """,
        };
        MailchimpTransactionalClient client = CreateClient(handler);

        IReadOnlyList<MailchimpTransactionalSendResult> results = await client.SendAsync(
            CreateMessage(),
            CancellationToken.None);

        MailchimpTransactionalSendResult result = Assert.Single(results);
        Assert.Equal("rejected", result.Status);
        Assert.Equal("hard-bounce", result.RejectReason);
    }

    private static MailchimpTransactionalClient CreateClient(HttpMessageHandler handler)
    {
        var options = Options.Create(new MailchimpOptions
        {
            Enabled = true,
            Transactional = new MailchimpTransactionalOptions
            {
                Enabled = true,
                ApiKey = "transactional-key",
            },
        });
        var resolver = new MailchimpTransactionalConfigurationResolver(
            options,
            NullLogger<MailchimpTransactionalConfigurationResolver>.Instance);
        return new MailchimpTransactionalClient(
            resolver,
            new TestHttpClientFactory(handler),
            NullLogger<MailchimpTransactionalClient>.Instance);
    }

    private static MailchimpTransactionalMessage CreateMessage() => new()
    {
        StoreAlias = "default",
        FromEmail = "orders@example.com",
        Subject = "Receipt",
        Text = "Thanks",
        Recipients = [new MailchimpTransactionalRecipient { Email = "person@example.com" }],
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }
        public HttpStatusCode StatusCode { get; init; } = HttpStatusCode.OK;
        public string ResponseBody { get; init; }
            = "[{\"email\":\"person@example.com\",\"status\":\"sent\",\"_id\":\"id-1\"}]";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestBody = request.Content == null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(StatusCode)
            {
                Content = new StringContent(ResponseBody),
            };
        }
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public TestHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://mandrillapp.com/api/1.0/"),
        };
    }
}

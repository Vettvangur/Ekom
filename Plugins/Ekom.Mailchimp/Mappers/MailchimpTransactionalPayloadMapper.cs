using Ekom.Mailchimp.Models;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ekom.Mailchimp.Mappers;

internal static class MailchimpTransactionalPayloadMapper
{
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static object CreatePayload(
        MailchimpTransactionalMessage message,
        MailchimpTransactionalStoreConfiguration configuration)
        => new
        {
            key = configuration.ApiKey,
            message = BuildMessage(message, message.Html, message.Text),
        };

    public static object CreatePayload(
        MailchimpTransactionalTemplateMessage message,
        MailchimpTransactionalStoreConfiguration configuration)
        => new
        {
            key = configuration.ApiKey,
            template_name = message.TemplateName,
            template_content = message.TemplateContent.Select(x => new { name = x.Key, content = x.Value }),
            message = BuildMessage(message, null, null),
        };

    public static int GetSerializedSize(object payload)
        => JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions).Length;

    [SuppressMessage("Globalization", "CA1308", Justification = "Mandrill requires lowercase recipient type values.")]
    private static object BuildMessage(
        MailchimpTransactionalMessageBase message,
        string? html,
        string? text)
        => new
        {
            html,
            text,
            subject = message.Subject,
            from_email = message.FromEmail,
            from_name = message.FromName,
            to = message.Recipients.Select(x => new
            {
                email = x.Email,
                name = x.Name,
                type = x.Type.ToString().ToLowerInvariant(),
            }),
            headers = string.IsNullOrWhiteSpace(message.ReplyTo)
                ? null
                : new Dictionary<string, string> { ["Reply-To"] = message.ReplyTo },
            global_merge_vars = message.GlobalMergeVariables.Select(x => new { name = x.Key, content = x.Value }),
            merge_vars = message.Recipients
                .Where(x => x.MergeVariables.Count > 0)
                .Select(x => new
                {
                    rcpt = x.Email,
                    vars = x.MergeVariables.Select(variable => new
                    {
                        name = variable.Key,
                        content = variable.Value,
                    }),
                }),
            tags = message.Tags,
            metadata = message.Metadata,
            attachments = BuildContent(message.Attachments),
            images = BuildContent(message.InlineImages),
        };

    private static object[]? BuildContent(IReadOnlyList<MailchimpTransactionalContent> content)
        => content.Count == 0
            ? null
            : content.Select(x => (object)new
            {
                type = x.ContentType,
                name = x.Name,
                content = Convert.ToBase64String(x.Content),
            }).ToArray();
}

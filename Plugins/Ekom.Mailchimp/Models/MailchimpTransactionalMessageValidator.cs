using Ekom.Mailchimp.Mappers;
using System.Net.Mail;
using System.Text.Json;

namespace Ekom.Mailchimp.Models;

internal static class MailchimpTransactionalMessageValidator
{
    private const int MaxMetadataBytes = 250;

    public static bool TryValidateAndSnapshot(
        MailchimpTransactionalMessage message,
        MailchimpTransactionalStoreConfiguration configuration,
        int maxMessageBytes,
        out MailchimpTransactionalMessage? snapshot,
        out long payloadBytes,
        out IReadOnlyList<string> errors)
    {
        var failures = new List<string>();
        ValidateCommon(message, configuration, failures);
        if (string.IsNullOrWhiteSpace(message.Subject))
        {
            failures.Add("Subject is required for raw messages.");
        }
        if (string.IsNullOrWhiteSpace(message.Html) && string.IsNullOrWhiteSpace(message.Text))
        {
            failures.Add("Raw messages require HTML or text content.");
        }
        if (failures.Count > 0)
        {
            snapshot = null;
            payloadBytes = 0;
            errors = failures;
            return false;
        }

        try
        {
            snapshot = message with
            {
                CorrelationId = GetCorrelationId(message.CorrelationId),
                FromEmail = message.FromEmail ?? configuration.DefaultFromEmail,
                FromName = message.FromName ?? configuration.DefaultFromName,
                ReplyTo = message.ReplyTo ?? configuration.DefaultReplyTo,
                Recipients = SnapshotRecipients(message.Recipients),
                GlobalMergeVariables = SnapshotValues(message.GlobalMergeVariables),
                Metadata = SnapshotValues(message.Metadata),
                Tags = message.Tags.ToArray(),
                Attachments = SnapshotContent(message.Attachments),
                InlineImages = SnapshotContent(message.InlineImages),
            };
            payloadBytes = MailchimpTransactionalPayloadMapper.GetSerializedSize(
                MailchimpTransactionalPayloadMapper.CreatePayload(snapshot, configuration));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ObjectDisposedException)
        {
            snapshot = null;
            payloadBytes = 0;
            failures.Add("Merge variables and metadata must contain JSON-serializable values.");
            errors = failures;
            return false;
        }

        if (payloadBytes > maxMessageBytes)
        {
            snapshot = null;
            failures.Add($"Serialized message exceeds the configured {maxMessageBytes} byte limit.");
        }
        errors = failures;
        return failures.Count == 0;
    }

    public static bool TryValidateAndSnapshot(
        MailchimpTransactionalTemplateMessage message,
        MailchimpTransactionalStoreConfiguration configuration,
        int maxMessageBytes,
        out MailchimpTransactionalTemplateMessage? snapshot,
        out long payloadBytes,
        out IReadOnlyList<string> errors)
    {
        var failures = new List<string>();
        ValidateCommon(message, configuration, failures);
        if (string.IsNullOrWhiteSpace(message.TemplateName))
        {
            failures.Add("TemplateName is required.");
        }
        if (message.TemplateContent == null
            || message.TemplateContent.Keys.Any(string.IsNullOrWhiteSpace)
            || message.TemplateContent.Values.Any(x => x == null))
        {
            failures.Add("Template content names and values cannot be null or blank.");
        }
        if (failures.Count > 0)
        {
            snapshot = null;
            payloadBytes = 0;
            errors = failures;
            return false;
        }

        try
        {
            snapshot = message with
            {
                CorrelationId = GetCorrelationId(message.CorrelationId),
                FromEmail = message.FromEmail ?? configuration.DefaultFromEmail,
                FromName = message.FromName ?? configuration.DefaultFromName,
                ReplyTo = message.ReplyTo ?? configuration.DefaultReplyTo,
                Recipients = SnapshotRecipients(message.Recipients),
                GlobalMergeVariables = SnapshotValues(message.GlobalMergeVariables),
                Metadata = SnapshotValues(message.Metadata),
                Tags = message.Tags.ToArray(),
                Attachments = SnapshotContent(message.Attachments),
                InlineImages = SnapshotContent(message.InlineImages),
                TemplateContent = new Dictionary<string, string>(message.TemplateContent!),
            };
            payloadBytes = MailchimpTransactionalPayloadMapper.GetSerializedSize(
                MailchimpTransactionalPayloadMapper.CreatePayload(snapshot, configuration));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ObjectDisposedException)
        {
            snapshot = null;
            payloadBytes = 0;
            failures.Add("Merge variables and metadata must contain JSON-serializable values.");
            errors = failures;
            return false;
        }

        if (payloadBytes > maxMessageBytes)
        {
            snapshot = null;
            failures.Add($"Serialized message exceeds the configured {maxMessageBytes} byte limit.");
        }
        errors = failures;
        return failures.Count == 0;
    }

    private static void ValidateCommon(
        MailchimpTransactionalMessageBase message,
        MailchimpTransactionalStoreConfiguration configuration,
        List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(message.StoreAlias))
        {
            failures.Add("StoreAlias is required.");
        }
        if (!IsValidEmail(message.FromEmail ?? configuration.DefaultFromEmail))
        {
            failures.Add("A valid sender email is required.");
        }
        string? replyTo = message.ReplyTo ?? configuration.DefaultReplyTo;
        if (replyTo != null && !IsValidEmail(replyTo))
        {
            failures.Add("ReplyTo must be a valid email address.");
        }

        ValidateRecipients(message.Recipients, failures);
        ValidateTags(message.Tags, failures);
        ValidateNamedValues(message.GlobalMergeVariables, "Global merge variables", failures);
        ValidateMetadata(message.Metadata, failures);
        ValidateContent(message.Attachments, "attachment", failures);
        ValidateContent(message.InlineImages, "inline image", failures);
    }

    private static void ValidateRecipients(
        IReadOnlyList<MailchimpTransactionalRecipient>? recipients,
        List<string> failures)
    {
        if (recipients == null || recipients.Count == 0)
        {
            failures.Add("At least one recipient is required.");
            return;
        }

        for (int index = 0; index < recipients.Count; index++)
        {
            MailchimpTransactionalRecipient? recipient = recipients[index];
            if (recipient == null || !IsValidEmail(recipient.Email))
            {
                failures.Add($"Recipient at index {index} must have a valid email address.");
                continue;
            }
            if (!Enum.IsDefined(recipient.Type))
            {
                failures.Add($"Recipient at index {index} has an invalid type.");
            }
            ValidateNamedValues(recipient.MergeVariables, $"Recipient merge variables at index {index}", failures);
        }
    }

    private static void ValidateTags(IReadOnlyList<string>? tags, List<string> failures)
    {
        if (tags == null)
        {
            failures.Add("Tags cannot be null.");
            return;
        }
        if (tags.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 50 || x.StartsWith('_')))
        {
            failures.Add("Tags must be non-blank, at most 50 characters, and cannot start with an underscore.");
        }
    }

    private static void ValidateNamedValues(
        IReadOnlyDictionary<string, object?>? values,
        string description,
        List<string> failures)
    {
        if (values == null || values.Keys.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add($"{description} and their names cannot be null or blank.");
        }
    }

    private static void ValidateMetadata(
        IReadOnlyDictionary<string, object?>? metadata,
        List<string> failures)
    {
        if (metadata == null)
        {
            failures.Add("Metadata cannot be null.");
            return;
        }
        bool hasInvalidNames = metadata.Keys.Any(x => !IsValidMetadataName(x));
        if (hasInvalidNames)
        {
            failures.Add("Metadata names must contain only letters, numbers, and underscores and cannot start with an underscore.");
        }
        bool hasInvalidValues = metadata.Values.Any(x => !IsScalarValue(x));
        if (hasInvalidValues)
        {
            failures.Add("Metadata values must be strings, numbers, booleans, or null.");
        }
        if (hasInvalidNames || hasInvalidValues)
        {
            return;
        }

        try
        {
            if (JsonSerializer.SerializeToUtf8Bytes(
                metadata,
                MailchimpTransactionalPayloadMapper.JsonOptions).Length > MaxMetadataBytes)
            {
                failures.Add($"Metadata cannot exceed {MaxMetadataBytes} JSON-encoded bytes.");
            }
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ObjectDisposedException)
        {
            failures.Add("Metadata values must be valid JSON scalars.");
        }
    }

    private static void ValidateContent(
        IReadOnlyList<MailchimpTransactionalContent>? content,
        string description,
        List<string> failures)
    {
        if (content == null)
        {
            failures.Add($"{description} collection cannot be null.");
            return;
        }
        for (int index = 0; index < content.Count; index++)
        {
            MailchimpTransactionalContent? item = content[index];
            if (item == null
                || string.IsNullOrWhiteSpace(item.Name)
                || string.IsNullOrWhiteSpace(item.ContentType)
                || item.Content == null
                || item.Content.Length == 0)
            {
                failures.Add($"Each {description} requires a name, content type, and non-empty content (index {index}).");
            }
        }
    }

    private static bool IsValidEmail(string? value)
        => !string.IsNullOrWhiteSpace(value) && MailAddress.TryCreate(value, out _);

    private static bool IsValidMetadataName(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && value[0] != '_'
            && value.All(x => char.IsAsciiLetterOrDigit(x) || x == '_');

    private static bool IsScalarValue(object? value)
    {
        try
        {
            return value is null
                or string
                or bool
                or byte
                or sbyte
                or short
                or ushort
                or int
                or uint
                or long
                or ulong
                or float
                or double
                or decimal
                or JsonElement { ValueKind: JsonValueKind.Null
                    or JsonValueKind.String
                    or JsonValueKind.True
                    or JsonValueKind.False
                    or JsonValueKind.Number };
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private static string GetCorrelationId(string? correlationId)
        => string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId;

    private static IReadOnlyList<MailchimpTransactionalRecipient> SnapshotRecipients(
        IReadOnlyList<MailchimpTransactionalRecipient> recipients)
        => recipients.Select(x => x with
        {
            MergeVariables = SnapshotValues(x.MergeVariables),
        }).ToArray();

    private static IReadOnlyDictionary<string, object?> SnapshotValues(
        IReadOnlyDictionary<string, object?> values)
        => values.ToDictionary(
            x => x.Key,
            x => x.Value == null
                ? null
                : (object)JsonSerializer.SerializeToElement(
                    x.Value,
                    MailchimpTransactionalPayloadMapper.JsonOptions));

    private static IReadOnlyList<MailchimpTransactionalContent> SnapshotContent(
        IReadOnlyList<MailchimpTransactionalContent> content)
        => content.Select(x => x with { Content = x.Content.ToArray() }).ToArray();
}

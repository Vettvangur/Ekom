using Ekom.Brevo.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ekom.Brevo.Mappers;

internal static class BrevoTransactionalMapper
{
    public static string Map(BrevoTransactionalMessage message, BrevoConfiguration configuration)
    {
        var store = configuration.Store(message.StoreAlias)?.Transactional;
        var defaults = configuration.Options.Transactional;
        string? from = store?.DefaultFromEmail ?? defaults.DefaultFromEmail;
        var sender = message.Sender ?? (from == null ? null : new BrevoEmailAddress(from, store?.DefaultFromName ?? defaults.DefaultFromName));
        string? reply = store?.DefaultReplyTo ?? defaults.DefaultReplyTo;
        var replyTo = message.ReplyTo ?? (reply == null ? null : new BrevoEmailAddress(reply));
        Validate(message, sender);
        var payload = new JsonObject { ["to"] = Addresses(message.To) };
        if (message.Cc.Count > 0) payload["cc"] = Addresses(message.Cc);
        if (message.Bcc.Count > 0) payload["bcc"] = Addresses(message.Bcc);
        if (sender != null) payload["sender"] = Address(sender);
        if (replyTo != null) payload["replyTo"] = Address(replyTo);
        if (message.Subject != null) payload["subject"] = message.Subject;
        if (message.TemplateId != null)
        {
            payload["templateId"] = message.TemplateId.Value;
            payload["params"] = JsonSerializer.SerializeToNode(message.Parameters);
        }
        else
        {
            if (message.HtmlContent != null) payload["htmlContent"] = message.HtmlContent;
            if (message.TextContent != null) payload["textContent"] = message.TextContent;
        }
        AddAttachments(payload, message, configuration.Options.Transactional.Dispatching.MaxMessageBytes);
        return payload.ToJsonString();
    }

    private static void Validate(BrevoTransactionalMessage message, BrevoEmailAddress? sender)
    {
        if (message.To.Count == 0 || message.To.Count + message.Cc.Count + message.Bcc.Count > 99)
            throw new ArgumentException("Provide at least one To recipient and at most 99 total recipients.", nameof(message));
        if (message.TemplateId is <= 0)
            throw new ArgumentException("TemplateId must be positive.", nameof(message));
        if (message.TemplateId == null && (sender == null || string.IsNullOrWhiteSpace(message.Subject)
            || (string.IsNullOrWhiteSpace(message.HtmlContent) && string.IsNullOrWhiteSpace(message.TextContent))))
            throw new ArgumentException("Direct email requires sender, subject, and HTML or text content.", nameof(message));
        if (message.TemplateId != null && (message.HtmlContent != null || message.TextContent != null))
            throw new ArgumentException("Choose either a template or direct content.", nameof(message));
    }

    private static void AddAttachments(JsonObject payload, BrevoTransactionalMessage message, int maxMessageBytes)
    {
        if (message.Attachments.Count > 0)
        {
            long rawBytes = message.Attachments.Sum(x => (long)x.Content.Length);
            if (rawBytes > maxMessageBytes * 3L / 4)
                throw new ArgumentException("Attachments exceed the configured message size.", nameof(message));
            var attachments = new JsonArray();
            foreach (var attachment in message.Attachments)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(attachment.Name);
                if (attachment.Content.IsEmpty) throw new ArgumentException("Attachments must not be empty.", nameof(message));
                attachments.Add(new JsonObject
                {
                    ["name"] = attachment.Name,
                    ["content"] = Convert.ToBase64String(attachment.Content.Span),
                });
            }
            payload["attachment"] = attachments;
        }
    }

    private static JsonObject Address(BrevoEmailAddress address)
    {
        BrevoConfiguration.ValidateEmail(address.Email);
        var result = new JsonObject { ["email"] = address.Email };
        if (address.Name != null) result["name"] = address.Name;
        return result;
    }

    private static JsonArray Addresses(IReadOnlyList<BrevoEmailAddress> addresses)
        => new(addresses.Select(x => (JsonNode)Address(x)).ToArray());
}

using Ekom.Brevo.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ekom.Brevo.Webhooks;

internal sealed record BrevoWebhookEvent(string MessageId, string? CorrelationId, string Recipient,
    string Event, DateTime OccurredUtc, string? Reason);

public static class BrevoWebhookEndpoints
{
    private const int MaxBodyBytes = 64 * 1024;

    /// <summary>Maps POST /ekom/brevo/webhooks/{storeAlias}. Requires configured bearer authentication.</summary>
    public static IEndpointConventionBuilder MapBrevoWebhooks(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/ekom/brevo/webhooks/{storeAlias}", HandleAsync).AllowAnonymous();

    internal static async Task<IResult> HandleAsync(HttpContext context, string storeAlias,
        BrevoConfiguration configuration, BrevoEmailStore store)
    {
        var options = configuration.Options;
        if (!options.Enabled || !options.Transactional.Webhooks.Enabled) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(storeAlias) || storeAlias.Length > 255) return Results.BadRequest();
        string? expected = configuration.Store(storeAlias)?.Transactional.WebhookToken ?? options.Transactional.Webhooks.Token;
        string authorization = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(expected) || authorization.Length > 1024
            || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
                SHA256.HashData(Encoding.UTF8.GetBytes(authorization[7..])))) return Results.Unauthorized();
        if (context.Request.ContentLength > MaxBodyBytes) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        using var body = new MemoryStream();
        var buffer = new byte[4096];
        int length;
        while ((length = await context.Request.Body.ReadAsync(buffer, context.RequestAborted).ConfigureAwait(false)) > 0)
        {
            if (body.Length + length > MaxBodyBytes) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            await body.WriteAsync(buffer.AsMemory(0, length), context.RequestAborted).ConfigureAwait(false);
        }
        IReadOnlyList<BrevoWebhookEvent> events;
        try
        {
            using var json = JsonDocument.Parse(body.ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
            events = Parse(json.RootElement);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or FormatException or OverflowException)
        {
            return Results.BadRequest();
        }
        // Storage failures propagate as 5xx so Brevo can retry. Never acknowledge before commit.
        if (events.Count > 0) await store.RecordEventsAsync(storeAlias, events, context.RequestAborted).ConfigureAwait(false);
        return Results.Ok();
    }

    internal static IReadOnlyList<BrevoWebhookEvent> Parse(JsonElement root)
    {
        var elements = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().ToArray() : [root];
        if (elements.Length > 100) throw new ArgumentException("Too many webhook events.", nameof(root));
        var events = new List<BrevoWebhookEvent>();
        foreach (var item in elements)
        {
            if (item.ValueKind != JsonValueKind.Object) throw new ArgumentException("Expected a webhook object.", nameof(root));
            string type = Text(item, "event", 32) ?? throw new ArgumentException("Missing event.", nameof(root));
            // Engagement, marketing and SMS events are not delivery tracking events.
            if (type is not ("request" or "delivered" or "soft_bounce" or "hard_bounce" or "blocked"
                or "spam" or "invalid_email" or "error" or "deferred" or "unsubscribed")) continue;
            string messageId = Text(item, "message-id", 320) ?? throw new ArgumentException("Missing message ID.", nameof(root));
            string email = Text(item, "email", 320) ?? throw new ArgumentException("Missing email.", nameof(root));
            BrevoConfiguration.ValidateEmail(email);
            if (!item.TryGetProperty("ts_event", out var timestamp)
                || !long.TryParse(timestamp.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
                throw new ArgumentException("Missing UTC event timestamp.", nameof(root));
            DateTime occurred = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
            string? custom = Text(item, "X-Mailin-custom", 512);
            string? correlation = custom?.StartsWith("ekom-brevo:", StringComparison.Ordinal) == true ? custom[11..] : null;
            if (correlation != null && !Guid.TryParseExact(correlation, "N", out _)) correlation = null;
            events.Add(new(BrevoEmailStore.NormalizeMessageId(messageId), correlation, email.ToUpperInvariant(), type,
                occurred, Text(item, "reason", 1000)));
        }
        return events;
    }

    private static string? Text(JsonElement item, string name, int maxLength)
    {
        if (!item.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String) throw new ArgumentException("Webhook field must be a string.", nameof(item));
        string? result = value.GetString();
        if (string.IsNullOrWhiteSpace(result) || result.Length > maxLength) throw new ArgumentException("Invalid webhook field length.", nameof(item));
        return result;
    }
}

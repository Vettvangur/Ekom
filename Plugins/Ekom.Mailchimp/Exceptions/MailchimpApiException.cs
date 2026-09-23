using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ekom.Mailchimp.Exceptions;

[SuppressMessage("Design", "CA1032", Justification = "Mailchimp API context is required to construct this exception.")]
public sealed class MailchimpApiException : Exception
{
    private static readonly Regex EmailPattern = new(@"\b[^\s@]+@[^\s@]+\.[^\s@]+\b", RegexOptions.CultureInvariant);
    private static readonly Regex SubscriberHashPattern = new(@"\b[a-fA-F0-9]{32}\b", RegexOptions.CultureInvariant);

    public MailchimpApiException(
        HttpStatusCode statusCode,
        string path,
        string? responseBody,
        TimeSpan? retryAfter = null)
        : base($"Mailchimp request to '{path}' failed with status {(int)statusCode}.")
    {
        StatusCode = statusCode;
        Path = path;
        ResponseBody = responseBody;
        RetryAfter = retryAfter;
        Error = ParseError(responseBody);
    }

    public HttpStatusCode StatusCode { get; }
    public string Path { get; }
    public string? ResponseBody { get; }
    public TimeSpan? RetryAfter { get; }
    public bool IsTransient => StatusCode == HttpStatusCode.TooManyRequests || (int)StatusCode >= 500;

    internal MailchimpApiError? Error { get; }

    private static MailchimpApiError? ParseError(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var fields = new HashSet<string>(StringComparer.Ordinal);
            if (root.TryGetProperty("errors", out JsonElement errors) && errors.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement error in errors.EnumerateArray())
                {
                    if (error.ValueKind != JsonValueKind.Object
                        || !error.TryGetProperty("field", out JsonElement field)
                        || field.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    string? fieldName = field.GetString();
                    if (!string.IsNullOrWhiteSpace(fieldName))
                    {
                        fields.Add(fieldName);
                    }
                }
            }

            return new MailchimpApiError(
                GetString(root, "type"),
                GetString(root, "title"),
                Sanitize(GetString(root, "detail")),
                fields.ToArray());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static string? Sanitize(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return detail;
        }

        string sanitized = EmailPattern.Replace(detail, "[redacted-email]");
        sanitized = SubscriberHashPattern.Replace(sanitized, "[redacted-subscriber-hash]");
        return sanitized.Length <= 1_000 ? sanitized : string.Concat(sanitized.AsSpan(0, 1_000), "...");
    }
}

internal sealed record MailchimpApiError(
    string? Type,
    string? Title,
    string? Detail,
    IReadOnlyList<string> Fields);

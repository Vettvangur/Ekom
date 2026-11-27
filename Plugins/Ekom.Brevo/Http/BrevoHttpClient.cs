using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Ekom.Brevo.Http;

[SuppressMessage("Design", "CA1032", Justification = "HTTP status is required for safe retry decisions.")]
public sealed class BrevoApiException(HttpStatusCode statusCode, TimeSpan? retryAfter)
    : Exception($"Brevo API returned HTTP {(int)statusCode}.")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

internal sealed class BrevoHttpClient(IHttpClientFactory factory)
{
    public async Task<string?> SendAsync(string apiKey, HttpMethod method, string path, string? payload, CancellationToken ct)
    {
        using var client = factory.CreateClient("Ekom.Brevo");
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("api-key", apiKey);
        if (payload != null)
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var retryAfter = response.Headers.RetryAfter;
            throw new BrevoApiException(response.StatusCode,
                retryAfter?.Delta ?? (retryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : null));
        }
        return response.StatusCode == HttpStatusCode.NoContent
            ? null : await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    public static string? MessageId(string? response)
    {
        if (string.IsNullOrWhiteSpace(response)) return null;
        using var doc = JsonDocument.Parse(response);
        return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("messageId", out var id) && id.ValueKind == JsonValueKind.String
            ? id.GetString() : null;
    }
}

using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ekom.Klaviyo.Http;

internal sealed class KlaviyoHttpClient
{
    private readonly HttpClient _http;
    private readonly ILogger<KlaviyoHttpClient> _logger;

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public KlaviyoHttpClient(HttpClient http, ILogger<KlaviyoHttpClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<string> PostAsync(string path, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        _logger.LogDebug("Klaviyo POST {Path}", path);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(path, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Klaviyo API error {StatusCode} on {Path} | Body={Body} | Json={Json}",
                (int)response.StatusCode, path, body, json);

            // Special-case: catalog sync lock (non-transient)
            if (response.StatusCode == HttpStatusCode.Forbidden &&
                body.Contains("active Catalog Sync", StringComparison.OrdinalIgnoreCase))
            {
                throw new KlaviyoCatalogSyncLockException((int)response.StatusCode, path, body, json);
            }

            throw new KlaviyoApiException(
                $"Klaviyo API error ({(int)response.StatusCode})",
                (int)response.StatusCode,
                path,
                body,
                json);
        }

        _logger.LogDebug("Klaviyo API accepted ({Path}) | Response={Body}", path, body);
        return body;
    }

    public async Task<string> GetAsync(string path, CancellationToken ct)
    {
        _logger.LogDebug("Klaviyo GET {Path}", path);

        using var response = await _http.GetAsync(path, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Klaviyo API GET error {StatusCode} on {Path} | Body={Body}",
                (int)response.StatusCode, path, body);

            throw new KlaviyoApiException(
                $"Klaviyo API GET error ({(int)response.StatusCode})",
                (int)response.StatusCode,
                path,
                body,
                requestJson: "");
        }

        return body;
    }
}

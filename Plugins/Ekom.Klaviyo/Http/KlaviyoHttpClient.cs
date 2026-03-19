using Ekom.Klaviyo.Exceptions;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ekom.Klaviyo.Http;

internal sealed class KlaviyoHttpClient
{
    private readonly HttpClient _http;
    private readonly IKlaviyoApiKeyResolver _apiKeyResolver;
    private readonly ILogger<KlaviyoHttpClient> _logger;

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public KlaviyoHttpClient(
        HttpClient http,
        IKlaviyoApiKeyResolver apiKeyResolver,
        ILogger<KlaviyoHttpClient> logger)
    {
        _http = http;
        _apiKeyResolver = apiKeyResolver;
        _logger = logger;
    }

    public Task<string> PostAsync(string path, object payload, string? storeAlias, CancellationToken ct)
    {
        var apiKey = _apiKeyResolver.ResolveRequired(storeAlias);
        return PostAsyncInternal(path, payload, apiKey, ct);
    }

    public Task<string> GetAsync(string path, string? storeAlias, CancellationToken ct)
    {
        var apiKey = _apiKeyResolver.ResolveRequired(storeAlias);
        return GetAsyncInternal(path, apiKey, ct);
    }

    private async Task<string> PostAsyncInternal(string path, object payload, string apiKey, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        _logger.LogDebug("Klaviyo POST {Path}", path);

        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.TryAddWithoutValidation("Authorization", $"Klaviyo-API-Key {apiKey}");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Klaviyo API error {StatusCode} on {Path} | Body={Body} | Json={Json}",
                (int)response.StatusCode, path, body, json);

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

    private async Task<string> GetAsyncInternal(string path, string apiKey, CancellationToken ct)
    {
        _logger.LogDebug("Klaviyo GET {Path}", path);

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("Authorization", $"Klaviyo-API-Key {apiKey}");

        using var response = await _http.SendAsync(request, ct);
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

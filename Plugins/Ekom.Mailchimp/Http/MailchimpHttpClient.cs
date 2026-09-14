using Ekom.Mailchimp.Exceptions;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ekom.Mailchimp.Http;

internal sealed class MailchimpHttpClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MailchimpHttpClient> _logger;

    public MailchimpHttpClient(IHttpClientFactory httpClientFactory, ILogger<MailchimpHttpClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<JsonNode?> SendAsync(
        MailchimpStoreConfiguration configuration,
        HttpMethod method,
        string path,
        object? payload,
        CancellationToken ct)
    {
        using HttpClient httpClient = _httpClientFactory.CreateClient("Ekom.Mailchimp");
        using var request = new HttpRequestMessage(
            method,
            new Uri($"https://{configuration.ServerPrefix}.api.mailchimp.com/3.0/{path.TrimStart('/')}", UriKind.Absolute));
        string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"ekom:{configuration.ApiKey}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (payload != null)
        {
            request.Content = JsonContent.Create(payload, options: JsonOptions);
        }

        using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        string? responseBody = response.Content.Headers.ContentLength == 0
            ? null
            : await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta;
            if (retryAfter == null && response.Headers.RetryAfter?.Date is { } retryDate)
            {
                retryAfter = retryDate - DateTimeOffset.UtcNow;
                if (retryAfter < TimeSpan.Zero)
                {
                    retryAfter = TimeSpan.Zero;
                }
            }
            _logger.LogWarning(
                "Mailchimp request to {Path} failed with status {StatusCode}",
                path,
                (int)response.StatusCode);
            throw new MailchimpApiException(response.StatusCode, path, responseBody, retryAfter);
        }

        if (response.StatusCode == HttpStatusCode.NoContent || string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        return JsonNode.Parse(responseBody);
    }
}

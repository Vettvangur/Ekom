using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Algolia.Services;

internal interface IAlgoliaQuerySuggestionsConfigurator
{
    Task EnsureConfiguredAsync(AlgoliaResolvedStore store, string primaryIndexName, CancellationToken ct = default);
}

internal sealed class AlgoliaQuerySuggestionsConfigurator : IAlgoliaQuerySuggestionsConfigurator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly AlgoliaOptions _options;
    private readonly IndexNameBuilder _indexNameBuilder;
    private readonly ILogger<AlgoliaQuerySuggestionsConfigurator> _logger;

    public AlgoliaQuerySuggestionsConfigurator(
        HttpClient httpClient,
        IOptions<AlgoliaOptions> options,
        IndexNameBuilder indexNameBuilder,
        ILogger<AlgoliaQuerySuggestionsConfigurator> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _indexNameBuilder = indexNameBuilder;
        _logger = logger;
    }

    public async Task EnsureConfiguredAsync(AlgoliaResolvedStore store, string primaryIndexName, CancellationToken ct = default)
    {
        if (!_options.Enabled || !_options.Search.Enabled || !_options.Search.QuerySuggestions)
            return;

        var provisioning = _options.Search.QuerySuggestionsProvisioning;
        if (!provisioning.Enabled)
            return;

        var indexName = _indexNameBuilder.BuildQuerySuggestions("products", store);
        var payload = BuildConfiguration(indexName, primaryIndexName, store);

        try
        {
            await CreateOrUpdateAsync(indexName, payload, ct).ConfigureAwait(false);

            _logger.LogDebug(
                "Ensured Algolia query suggestions config for store {Store}, source {SourceIndex}, target {TargetIndex}.",
                store.Alias,
                primaryIndexName,
                indexName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to ensure Algolia query suggestions config for store {Store}, source {SourceIndex}, target {TargetIndex}.",
                store.Alias,
                primaryIndexName,
                indexName);
        }
    }

    private async Task CreateOrUpdateAsync(string indexName, QuerySuggestionsConfigWithIndex payload, CancellationToken ct)
    {
        var createResponse = await SendAsync(
            HttpMethod.Post,
            "/1/configs",
            payload,
            ct).ConfigureAwait(false);

        if (createResponse.StatusCode == HttpStatusCode.OK)
            return;

        if (createResponse.StatusCode != HttpStatusCode.UnprocessableEntity)
            throw await CreateExceptionAsync(createResponse).ConfigureAwait(false);

        var updatePayload = new QuerySuggestionsConfig
        {
            SourceIndices = payload.SourceIndices,
            Languages = payload.Languages,
            Exclude = payload.Exclude,
            EnablePersonalization = payload.EnablePersonalization,
            AllowSpecialCharacters = payload.AllowSpecialCharacters
        };

        var updateResponse = await SendAsync(
            HttpMethod.Put,
            $"/1/configs/{Uri.EscapeDataString(indexName)}",
            updatePayload,
            ct).ConfigureAwait(false);

        if (updateResponse.StatusCode == HttpStatusCode.OK)
            return;

        throw await CreateExceptionAsync(updateResponse).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object payload, CancellationToken ct)
    {
        Exception? lastException = null;

        foreach (var region in GetRegionCandidates())
        {
            using var request = new HttpRequestMessage(method, BuildUri(region, path))
            {
                Content = JsonContent.Create(payload, options: JsonOptions)
            };

            request.Headers.TryAddWithoutValidation("x-algolia-application-id", _options.ApplicationId);
            request.Headers.TryAddWithoutValidation("x-algolia-api-key", _options.AdminApiKey);

            var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!IsRegionMismatch(response.StatusCode, body))
                return response;

            lastException = CreateException(response.StatusCode, body);
        }

        throw lastException ?? new InvalidOperationException("Failed to determine the Algolia analytics region for query suggestions.");
    }

    private QuerySuggestionsConfigWithIndex BuildConfiguration(string indexName, string primaryIndexName, AlgoliaResolvedStore store)
    {
        var provisioning = _options.Search.QuerySuggestionsProvisioning;

        return new QuerySuggestionsConfigWithIndex
        {
            IndexName = indexName,
            SourceIndices =
            [
                new QuerySuggestionsSourceIndex
                {
                    IndexName = primaryIndexName,
                    Replicas = provisioning.UseReplicas,
                    MinHits = Math.Max(0, provisioning.MinimumHits),
                    MinLetters = Math.Max(0, provisioning.MinimumLetters),
                }
            ],
            Languages = BuildLanguages(store.Locale),
            Exclude = provisioning.Exclude.Count > 0 ? provisioning.Exclude.ToList() : null,
            EnablePersonalization = provisioning.EnablePersonalization,
            AllowSpecialCharacters = provisioning.AllowSpecialCharacters
        };
    }

    private static List<string>? BuildLanguages(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            return null;

        try
        {
            var culture = CultureInfo.GetCultureInfo(locale);
            return [culture.TwoLetterISOLanguageName.ToLowerInvariant()];
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    private IEnumerable<string> GetRegionCandidates()
    {
        var configured = NormalizeRegion(_options.AnalyticsRegion);
        if (configured is not null)
        {
            yield return configured;
            yield break;
        }

        yield return "us";
        yield return "eu";
    }

    private static string? NormalizeRegion(string? region)
    {
        if (string.IsNullOrWhiteSpace(region))
            return null;

        var normalized = region.Trim().ToLowerInvariant();
        return normalized is "us" or "eu"
            ? normalized
            : null;
    }

    private static Uri BuildUri(string region, string path)
        => new($"https://query-suggestions.{region}.algolia.com{path}", UriKind.Absolute);

    private static bool IsRegionMismatch(HttpStatusCode statusCode, string body)
    {
        if (statusCode != HttpStatusCode.Unauthorized)
            return false;

        var error = DeserializeError(body);
        return error?.Message?.Contains("region does not match", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static async Task<InvalidOperationException> CreateExceptionAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return CreateException(response.StatusCode, body);
    }

    private static InvalidOperationException CreateException(HttpStatusCode statusCode, string body)
    {
        var error = DeserializeError(body);
        var message = error?.Message ?? $"Algolia query suggestions API returned {(int)statusCode}.";
        return new InvalidOperationException(message);
    }

    private static AlgoliaQuerySuggestionsError? DeserializeError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            return JsonSerializer.Deserialize<AlgoliaQuerySuggestionsError>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class QuerySuggestionsConfigWithIndex : QuerySuggestionsConfig
    {
        public required string IndexName { get; init; }
    }

    private class QuerySuggestionsConfig
    {
        public required List<QuerySuggestionsSourceIndex> SourceIndices { get; init; }
        public List<string>? Languages { get; init; }
        public List<string>? Exclude { get; init; }
        public bool EnablePersonalization { get; init; }
        public bool AllowSpecialCharacters { get; init; }
    }

    private sealed class QuerySuggestionsSourceIndex
    {
        public required string IndexName { get; init; }
        public bool Replicas { get; init; }
        public int MinHits { get; init; }
        public int MinLetters { get; init; }
    }

    private sealed class AlgoliaQuerySuggestionsError
    {
        public string? Message { get; init; }
    }
}

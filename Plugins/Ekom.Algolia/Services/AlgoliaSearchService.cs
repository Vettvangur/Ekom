using Algolia.Search.Models.Search;
using Algolia.Search.Exceptions;
using Ekom.Algolia.Models.Indexing;
using Ekom.Algolia.Models.Search;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Ekom.Algolia.Services;

public interface IAlgoliaSearchService
{
    Task<AlgoliaSearchResponse<AlgoliaProductRecord>> SearchProductsAsync(AlgoliaSearchRequest request, CancellationToken ct = default);
    Task<AlgoliaSearchResponse<AlgoliaQuerySuggestionRecord>> SearchQuerySuggestionsAsync(AlgoliaSearchRequest request, CancellationToken ct = default);
}

internal sealed class AlgoliaSearchService : IAlgoliaSearchService
{
    private const string ProductsEntity = "products";
    private const string QuerySuggestionsEntity = "products";

    private readonly IAlgoliaQueryClient _queryClient;
    private readonly IMemoryCache _cache;
    private readonly AlgoliaOptions _options;
    private readonly AlgoliaStoreResolver _storeResolver;
    private readonly IndexNameBuilder _indexNameBuilder;
    private readonly AlgoliaSearchCacheKeyBuilder _cacheKeyBuilder;
    private readonly ILogger<AlgoliaSearchService> _logger;

    public AlgoliaSearchService(
        IAlgoliaQueryClient queryClient,
        IMemoryCache cache,
        IOptions<AlgoliaOptions> options,
        AlgoliaStoreResolver storeResolver,
        IndexNameBuilder indexNameBuilder,
        AlgoliaSearchCacheKeyBuilder cacheKeyBuilder,
        ILogger<AlgoliaSearchService> logger)
    {
        _queryClient = queryClient;
        _cache = cache;
        _options = options.Value;
        _storeResolver = storeResolver;
        _indexNameBuilder = indexNameBuilder;
        _cacheKeyBuilder = cacheKeyBuilder;
        _logger = logger;
    }

    public async Task<AlgoliaSearchResponse<AlgoliaProductRecord>> SearchProductsAsync(AlgoliaSearchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.StoreAlias);
        ArgumentNullException.ThrowIfNull(request.Query);

        if (!_options.Enabled || !_options.Search.Enabled || !_options.Search.Products)
        {
            _logger.LogDebug("Algolia product search skipped because search is disabled. Store={Store}", request.StoreAlias);
            return EmptyResponse<AlgoliaProductRecord>(request.Query.Query, request.Query.Page, request.Query.HitsPerPage);
        }

        var query = CloneQuery(request.Query);
        var trimmedQuery = query.Query?.Trim();

        if (_options.Search.MinimumQueryLength > 0 && string.IsNullOrWhiteSpace(trimmedQuery))
            return EmptyResponse<AlgoliaProductRecord>(trimmedQuery, query.Page, query.HitsPerPage);

        if (_options.Search.MinimumQueryLength > 0 && trimmedQuery!.Length < _options.Search.MinimumQueryLength)
            return EmptyResponse<AlgoliaProductRecord>(trimmedQuery, query.Page, query.HitsPerPage);

        var store = _storeResolver.Resolve(request.StoreAlias);
        var target = store.WithSelection(request.Locale ?? store.Locale, request.Currency ?? store.Currency);
        var indexName = _indexNameBuilder.BuildPrimary(ProductsEntity, target);

        query.IndexName = indexName;

        if (_options.Search.MaxHitsPerPage > 0 && query.HitsPerPage > _options.Search.MaxHitsPerPage)
            query.HitsPerPage = _options.Search.MaxHitsPerPage;

        var useCache = _options.Search.Cache.Enabled && !request.BypassCache;
        var cacheKey = _cacheKeyBuilder.BuildProductsKey(request, query, indexName);

        if (useCache && _cache.TryGetValue(cacheKey, out AlgoliaSearchResponse<AlgoliaProductRecord>? cached) && cached is not null)
        {
            _logger.LogDebug("Algolia product search cache hit for store {Store} and index {IndexName}.", request.StoreAlias, indexName);
            return cached;
        }

        var response = await QueryProductsAsync(query, ct).ConfigureAwait(false);

        if (!useCache)
            return response;

        if (response.Hits.Count == 0 && !_options.Search.Cache.CacheEmptyResults)
            return response;

        var ttlMinutes = _options.Search.Cache.DurationMinutes <= 0 ? 60 : _options.Search.Cache.DurationMinutes;
        _cache.Set(cacheKey, response, TimeSpan.FromMinutes(ttlMinutes));

        return response;
    }

    public async Task<AlgoliaSearchResponse<AlgoliaQuerySuggestionRecord>> SearchQuerySuggestionsAsync(AlgoliaSearchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.StoreAlias);
        ArgumentNullException.ThrowIfNull(request.Query);

        if (!_options.Enabled || !_options.Search.Enabled || !_options.Search.QuerySuggestions)
        {
            _logger.LogDebug("Algolia query suggestions search skipped because query suggestions are disabled. Store={Store}", request.StoreAlias);
            return EmptyResponse<AlgoliaQuerySuggestionRecord>(request.Query.Query, request.Query.Page, request.Query.HitsPerPage);
        }

        var query = CloneQuery(request.Query);
        var trimmedQuery = query.Query?.Trim();

        if (_options.Search.MinimumQueryLength > 0 && string.IsNullOrWhiteSpace(trimmedQuery))
            return EmptyResponse<AlgoliaQuerySuggestionRecord>(trimmedQuery, query.Page, query.HitsPerPage);

        if (_options.Search.MinimumQueryLength > 0 && trimmedQuery!.Length < _options.Search.MinimumQueryLength)
            return EmptyResponse<AlgoliaQuerySuggestionRecord>(trimmedQuery, query.Page, query.HitsPerPage);

        var store = _storeResolver.Resolve(request.StoreAlias);
        var target = store.WithSelection(request.Locale ?? store.Locale, request.Currency ?? store.Currency);
        var indexName = _indexNameBuilder.BuildQuerySuggestions(QuerySuggestionsEntity, target);

        query.IndexName = indexName;

        if (_options.Search.MaxHitsPerPage > 0 && query.HitsPerPage > _options.Search.MaxHitsPerPage)
            query.HitsPerPage = _options.Search.MaxHitsPerPage;

        var useCache = _options.Search.Cache.Enabled && !request.BypassCache;
        var cacheKey = _cacheKeyBuilder.BuildQuerySuggestionsKey(request, query, indexName);

        if (useCache && _cache.TryGetValue(cacheKey, out AlgoliaSearchResponse<AlgoliaQuerySuggestionRecord>? cached) && cached is not null)
        {
            _logger.LogDebug("Algolia query suggestions cache hit for store {Store} and index {IndexName}.", request.StoreAlias, indexName);
            return cached;
        }

        AlgoliaSearchResponse<AlgoliaQuerySuggestionRecord> response;

        try
        {
            response = await QueryAsync<AlgoliaQuerySuggestionRecord>(query, ct).ConfigureAwait(false);
        }
        catch (AlgoliaApiException ex) when (ex.HttpErrorCode == 404)
        {
            _logger.LogWarning(
                ex,
                "Algolia query suggestions index was not found for store {Store} and index {IndexName}. Returning no suggestions.",
                request.StoreAlias,
                indexName);

            return EmptyResponse<AlgoliaQuerySuggestionRecord>(trimmedQuery, query.Page, query.HitsPerPage);
        }

        if (!useCache)
            return response;

        if (response.Hits.Count == 0 && !_options.Search.Cache.CacheEmptyResults)
            return response;

        var ttlMinutes = _options.Search.Cache.DurationMinutes <= 0 ? 60 : _options.Search.Cache.DurationMinutes;
        _cache.Set(cacheKey, response, TimeSpan.FromMinutes(ttlMinutes));

        return response;
    }

    private Task<AlgoliaSearchResponse<AlgoliaProductRecord>> QueryProductsAsync(SearchForHits query, CancellationToken ct)
        => QueryAsync<AlgoliaProductRecord>(query, ct);

    private async Task<AlgoliaSearchResponse<THit>> QueryAsync<THit>(SearchForHits query, CancellationToken ct)
        where THit : class
    {
        var response = await _queryClient.SearchAsync<THit>(
            new SearchMethodParams
            {
                Requests =
                [
                    new SearchQuery(query)
                ]
            },
            ct).ConfigureAwait(false);

        var result = response.Results
            .Select(x => x.AsSearchResponse())
            .FirstOrDefault(x => x is not null);

        if (result is null)
            return EmptyResponse<THit>(query.Query, query.Page, query.HitsPerPage);

        return new AlgoliaSearchResponse<THit>
        {
            Hits = result.Hits?.ToList() ?? [],
            Page = result.Page ?? 0,
            HitsPerPage = result.HitsPerPage ?? 0,
            TotalHits = result.NbHits ?? 0,
            TotalPages = result.NbPages ?? 0,
            ProcessingTimeMs = result.ProcessingTimeMS ?? 0,
            Query = result.Query,
            Facets = ToFacets(result.Facets)
        };
    }

    private static AlgoliaSearchResponse<THit> EmptyResponse<THit>(string? query, int? page, int? hitsPerPage)
        where THit : class
        => new()
        {
            Query = query,
            Page = page ?? 0,
            HitsPerPage = hitsPerPage ?? 0,
            Hits = []
        };

    private static SearchForHits CloneQuery(SearchForHits query)
    {
        var json = JsonSerializer.Serialize(query);
        return JsonSerializer.Deserialize<SearchForHits>(json)
            ?? throw new InvalidOperationException("Failed to clone Algolia search query.");
    }

    private static IReadOnlyDictionary<string, JsonElement>? ToFacets(object? facets)
    {
        if (facets is null)
            return null;

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(facets));
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            return null;

        var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.RootElement.EnumerateObject())
            result[property.Name] = property.Value.Clone();

        return result;
    }
}

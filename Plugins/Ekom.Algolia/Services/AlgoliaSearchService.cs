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
    Task<AlgoliaSearchResponse<AlgoliaCategoryRecord>> SearchCategoriesAsync(AlgoliaSearchRequest request, CancellationToken ct = default);
    Task<AlgoliaSearchResponse<AlgoliaQuerySuggestionRecord>> SearchQuerySuggestionsAsync(AlgoliaSearchRequest request, CancellationToken ct = default);
    Task<AlgoliaSearchResponse<AlgoliaContentRecord>> SearchContentAsync(AlgoliaContentSearchRequest request, CancellationToken ct = default);
    Task<AlgoliaFederatedSearchResponse> FederatedSearchAsync(AlgoliaFederatedSearchRequest request, CancellationToken ct = default);
}

internal sealed class AlgoliaSearchService : IAlgoliaSearchService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string ProductsEntity = "products";
    private const string CategoriesEntity = "categories";
    private const string QuerySuggestionsEntity = "products";

    private readonly IAlgoliaQueryClient _queryClient;
    private readonly IMemoryCache _cache;
    private readonly AlgoliaOptions _options;
    private readonly AlgoliaStoreResolver _storeResolver;
    private readonly IndexNameBuilder _indexNameBuilder;
    private readonly ContentIndexNameResolver _contentIndexNameResolver;
    private readonly AlgoliaSearchCacheKeyBuilder _cacheKeyBuilder;
    private readonly IAlgoliaUserTokenProvider _userTokenProvider;
    private readonly ILogger<AlgoliaSearchService> _logger;

    public AlgoliaSearchService(
        IAlgoliaQueryClient queryClient,
        IMemoryCache cache,
        IOptions<AlgoliaOptions> options,
        AlgoliaStoreResolver storeResolver,
        IndexNameBuilder indexNameBuilder,
        ContentIndexNameResolver contentIndexNameResolver,
        AlgoliaSearchCacheKeyBuilder cacheKeyBuilder,
        IAlgoliaUserTokenProvider userTokenProvider,
        ILogger<AlgoliaSearchService> logger)
    {
        _queryClient = queryClient;
        _cache = cache;
        _options = options.Value;
        _storeResolver = storeResolver;
        _indexNameBuilder = indexNameBuilder;
        _contentIndexNameResolver = contentIndexNameResolver;
        _cacheKeyBuilder = cacheKeyBuilder;
        _userTokenProvider = userTokenProvider;
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
        ApplyVariantGrouping(query);

        if (_options.Search.MaxHitsPerPage > 0 && query.HitsPerPage > _options.Search.MaxHitsPerPage)
            query.HitsPerPage = _options.Search.MaxHitsPerPage;

        var userToken = PrepareUserTokenForCache(query);

        var useCache = _options.Search.Cache.Enabled && !request.BypassCache;
        var cacheKey = _cacheKeyBuilder.BuildProductsKey(request, query, indexName);

        if (useCache && _cache.TryGetValue(cacheKey, out AlgoliaSearchResponse<AlgoliaProductRecord>? cached) && cached is not null)
        {
            _logger.LogDebug("Algolia product search cache hit for store {Store} and index {IndexName}.", request.StoreAlias, indexName);
            return cached;
        }

        ApplyUserToken(query, userToken);

        var response = await QueryProductsAsync(query, ct).ConfigureAwait(false);

        if (!useCache)
            return response;

        if (response.Hits.Count == 0 && !_options.Search.Cache.CacheEmptyResults)
            return response;

        var ttlMinutes = _options.Search.Cache.DurationMinutes <= 0 ? 60 : _options.Search.Cache.DurationMinutes;
        _cache.Set(cacheKey, response, TimeSpan.FromMinutes(ttlMinutes));

        return response;
    }

    public async Task<AlgoliaSearchResponse<AlgoliaCategoryRecord>> SearchCategoriesAsync(AlgoliaSearchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.StoreAlias);
        ArgumentNullException.ThrowIfNull(request.Query);

        if (!_options.Enabled || !_options.Search.Enabled || !_options.Search.Categories)
        {
            _logger.LogDebug("Algolia category search skipped because search is disabled. Store={Store}", request.StoreAlias);
            return EmptyResponse<AlgoliaCategoryRecord>(request.Query.Query, request.Query.Page, request.Query.HitsPerPage);
        }

        var query = CloneQuery(request.Query);
        var trimmedQuery = query.Query?.Trim();

        if (_options.Search.MinimumQueryLength > 0 && string.IsNullOrWhiteSpace(trimmedQuery))
            return EmptyResponse<AlgoliaCategoryRecord>(trimmedQuery, query.Page, query.HitsPerPage);

        if (_options.Search.MinimumQueryLength > 0 && trimmedQuery!.Length < _options.Search.MinimumQueryLength)
            return EmptyResponse<AlgoliaCategoryRecord>(trimmedQuery, query.Page, query.HitsPerPage);

        var store = _storeResolver.Resolve(request.StoreAlias);
        var target = store.WithSelection(request.Locale ?? store.Locale, currency: null);
        var indexName = _indexNameBuilder.BuildPrimary(CategoriesEntity, target, currencyOverride: string.Empty);

        query.IndexName = indexName;

        if (_options.Search.MaxHitsPerPage > 0 && query.HitsPerPage > _options.Search.MaxHitsPerPage)
            query.HitsPerPage = _options.Search.MaxHitsPerPage;

        var userToken = PrepareUserTokenForCache(query);

        var useCache = _options.Search.Cache.Enabled && !request.BypassCache;
        var cacheKey = _cacheKeyBuilder.BuildCategoriesKey(request, query, indexName);

        if (useCache && _cache.TryGetValue(cacheKey, out AlgoliaSearchResponse<AlgoliaCategoryRecord>? cached) && cached is not null)
        {
            _logger.LogDebug("Algolia category search cache hit for store {Store} and index {IndexName}.", request.StoreAlias, indexName);
            return cached;
        }

        ApplyUserToken(query, userToken);

        var response = await QueryAsync<AlgoliaCategoryRecord>(query, ct).ConfigureAwait(false);

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

        var userToken = PrepareUserTokenForCache(query);

        var useCache = _options.Search.Cache.Enabled && !request.BypassCache;
        var cacheKey = _cacheKeyBuilder.BuildQuerySuggestionsKey(request, query, indexName);

        if (useCache && _cache.TryGetValue(cacheKey, out AlgoliaSearchResponse<AlgoliaQuerySuggestionRecord>? cached) && cached is not null)
        {
            _logger.LogDebug("Algolia query suggestions cache hit for store {Store} and index {IndexName}.", request.StoreAlias, indexName);
            return cached;
        }

        ApplyUserToken(query, userToken);

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

    public async Task<AlgoliaSearchResponse<AlgoliaContentRecord>> SearchContentAsync(AlgoliaContentSearchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.IndexName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Culture);
        ArgumentNullException.ThrowIfNull(request.Query);

        if (!_options.Enabled || !_options.Search.Enabled || !_options.ContentIndexing.Enabled)
        {
            _logger.LogDebug("Algolia content search skipped because search or content indexing is disabled. Index={IndexName}", request.IndexName);
            return EmptyResponse<AlgoliaContentRecord>(request.Query.Query, request.Query.Page, request.Query.HitsPerPage);
        }

        var query = CloneQuery(request.Query);
        var trimmedQuery = query.Query?.Trim();

        if (_options.Search.MinimumQueryLength > 0 && string.IsNullOrWhiteSpace(trimmedQuery))
            return EmptyResponse<AlgoliaContentRecord>(trimmedQuery, query.Page, query.HitsPerPage);

        if (_options.Search.MinimumQueryLength > 0 && trimmedQuery!.Length < _options.Search.MinimumQueryLength)
            return EmptyResponse<AlgoliaContentRecord>(trimmedQuery, query.Page, query.HitsPerPage);

        var indexName = _contentIndexNameResolver.Resolve(request.IndexName, request.Culture);
        query.IndexName = indexName;

        if (_options.Search.MaxHitsPerPage > 0 && query.HitsPerPage > _options.Search.MaxHitsPerPage)
            query.HitsPerPage = _options.Search.MaxHitsPerPage;

        var userToken = PrepareUserTokenForCache(query);
        var useCache = _options.Search.Cache.Enabled && !request.BypassCache;
        var cacheKey = _cacheKeyBuilder.BuildContentKey(request, query, indexName);

        if (useCache && _cache.TryGetValue(cacheKey, out AlgoliaSearchResponse<AlgoliaContentRecord>? cached) && cached is not null)
        {
            _logger.LogDebug("Algolia content search cache hit for index {IndexName}.", indexName);
            return cached;
        }

        ApplyUserToken(query, userToken);

        var response = await QueryAsync<AlgoliaContentRecord>(query, ct).ConfigureAwait(false);

        if (!useCache)
            return response;

        if (response.Hits.Count == 0 && !_options.Search.Cache.CacheEmptyResults)
            return response;

        var ttlMinutes = _options.Search.Cache.DurationMinutes <= 0 ? 60 : _options.Search.Cache.DurationMinutes;
        _cache.Set(cacheKey, response, TimeSpan.FromMinutes(ttlMinutes));

        return response;
    }

    public async Task<AlgoliaFederatedSearchResponse> FederatedSearchAsync(AlgoliaFederatedSearchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.StoreAlias);

        if (request.Targets.Count == 0 || !_options.Enabled || !_options.Search.Enabled)
            return new AlgoliaFederatedSearchResponse();

        var store = _storeResolver.Resolve(request.StoreAlias);
        var preparedTargets = new List<(AlgoliaFederatedSearchTarget Target, SearchForHits Query, string IndexName)>();

        foreach (var target in request.Targets)
        {
            ct.ThrowIfCancellationRequested();
            ArgumentException.ThrowIfNullOrWhiteSpace(target.Key);
            ArgumentNullException.ThrowIfNull(target.Query);

            if (!TryPrepareFederatedTarget(request, store, target, out var query, out var indexName))
                continue;

            preparedTargets.Add((target, query, indexName));
        }

        if (preparedTargets.Count == 0)
            return new AlgoliaFederatedSearchResponse();

        var response = await _queryClient.SearchAsync<Dictionary<string, object?>>(
            new SearchMethodParams
            {
                Requests = preparedTargets.Select(x => new SearchQuery(x.Query)).ToList()
            },
            ct).ConfigureAwait(false);

        AlgoliaSearchResponse<AlgoliaQuerySuggestionRecord>? suggestions = null;
        AlgoliaSearchResponse<AlgoliaProductRecord>? products = null;
        AlgoliaSearchResponse<AlgoliaCategoryRecord>? categories = null;
        var content = new Dictionary<string, AlgoliaSearchResponse<AlgoliaContentRecord>>(StringComparer.OrdinalIgnoreCase);
        var resultList = response.Results.Select(x => x.AsSearchResponse()).ToList();

        for (var i = 0; i < preparedTargets.Count; i++)
        {
            var (target, query, _) = preparedTargets[i];
            var result = i < resultList.Count ? resultList[i] : null;

            switch (target.Kind)
            {
                case AlgoliaFederatedSearchTargetKind.QuerySuggestions:
                    suggestions = result is null
                        ? EmptyResponse<AlgoliaQuerySuggestionRecord>(query.Query, query.Page, query.HitsPerPage)
                        : ToSearchResponse<AlgoliaQuerySuggestionRecord>(result);
                    break;
                case AlgoliaFederatedSearchTargetKind.Products:
                    products = result is null
                        ? EmptyResponse<AlgoliaProductRecord>(query.Query, query.Page, query.HitsPerPage)
                        : ToSearchResponse<AlgoliaProductRecord>(result);
                    break;
                case AlgoliaFederatedSearchTargetKind.Categories:
                    categories = result is null
                        ? EmptyResponse<AlgoliaCategoryRecord>(query.Query, query.Page, query.HitsPerPage)
                        : ToSearchResponse<AlgoliaCategoryRecord>(result);
                    break;
                case AlgoliaFederatedSearchTargetKind.Content:
                    content[target.Key] = result is null
                        ? EmptyResponse<AlgoliaContentRecord>(query.Query, query.Page, query.HitsPerPage)
                        : ToSearchResponse<AlgoliaContentRecord>(result);
                    break;
            }
        }

        return new AlgoliaFederatedSearchResponse
        {
            Suggestions = suggestions,
            Products = products,
            Categories = categories,
            Content = content
        };
    }

    private Task<AlgoliaSearchResponse<AlgoliaProductRecord>> QueryProductsAsync(SearchForHits query, CancellationToken ct)
        => QueryAsync<AlgoliaProductRecord>(query, ct);

    private bool TryPrepareFederatedTarget(
        AlgoliaFederatedSearchRequest request,
        AlgoliaResolvedStore store,
        AlgoliaFederatedSearchTarget target,
        out SearchForHits query,
        out string indexName)
    {
        query = CloneQuery(target.Query);
        indexName = string.Empty;
        var trimmedQuery = query.Query?.Trim();

        if (_options.Search.MinimumQueryLength > 0 && string.IsNullOrWhiteSpace(trimmedQuery))
            return false;

        if (_options.Search.MinimumQueryLength > 0 && trimmedQuery!.Length < _options.Search.MinimumQueryLength)
            return false;

        if (_options.Search.MaxHitsPerPage > 0 && query.HitsPerPage > _options.Search.MaxHitsPerPage)
            query.HitsPerPage = _options.Search.MaxHitsPerPage;

        var contentCulture = target.Culture ?? request.Locale ?? store.Locale;
        indexName = target.Kind switch
        {
            AlgoliaFederatedSearchTargetKind.Products when _options.Search.Products => _indexNameBuilder.BuildPrimary(ProductsEntity, store.WithSelection(request.Locale ?? store.Locale, request.Currency ?? store.Currency)),
            AlgoliaFederatedSearchTargetKind.Categories when _options.Search.Categories => _indexNameBuilder.BuildPrimary(CategoriesEntity, store.WithSelection(request.Locale ?? store.Locale, currency: null), currencyOverride: string.Empty),
            AlgoliaFederatedSearchTargetKind.QuerySuggestions when _options.Search.QuerySuggestions => _indexNameBuilder.BuildQuerySuggestions(QuerySuggestionsEntity, store.WithSelection(request.Locale ?? store.Locale, request.Currency ?? store.Currency)),
            AlgoliaFederatedSearchTargetKind.Content when _options.ContentIndexing.Enabled && !string.IsNullOrWhiteSpace(target.ContentIndexName) && !string.IsNullOrWhiteSpace(contentCulture) => _contentIndexNameResolver.Resolve(target.ContentIndexName, contentCulture),
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(indexName))
            return false;

        query.IndexName = indexName;
        if (target.Kind == AlgoliaFederatedSearchTargetKind.Products)
            ApplyVariantGrouping(query);

        var userToken = PrepareUserTokenForCache(query);
        ApplyUserToken(query, userToken);
        return true;
    }

    private void ApplyVariantGrouping(SearchForHits query)
    {
        if (!_options.Indexing.Variants || !_options.Search.GroupVariantsByProduct)
            return;

        query.Distinct ??= new Distinct(true);
    }

    private string? PrepareUserTokenForCache(SearchForHits query)
    {
        var userToken = query.UserToken;

        if (_options.Search.VaryCacheByUserToken)
        {
            ApplyUserToken(query, userToken);
            return query.UserToken;
        }

        query.UserToken = null;
        return userToken;
    }

    private void ApplyUserToken(SearchForHits query, string? userToken)
    {
        if (!string.IsNullOrWhiteSpace(query.UserToken))
            return;

        if (!string.IsNullOrWhiteSpace(userToken))
        {
            query.UserToken = userToken;
            return;
        }

        if (!_options.Search.IncludeUserToken)
            return;

        var resolvedUserToken = _userTokenProvider.GetUserToken();
        if (!string.IsNullOrWhiteSpace(resolvedUserToken))
            query.UserToken = resolvedUserToken;
    }

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

    private static AlgoliaSearchResponse<THit> ToSearchResponse<THit>(dynamic result)
        where THit : class
    {
        var hits = new List<THit>();
        if (result.Hits is not null)
        {
            foreach (var hit in result.Hits)
            {
                var mapped = MapHit<THit>(hit);
                if (mapped is not null)
                    hits.Add(mapped);
            }
        }

        return new AlgoliaSearchResponse<THit>
        {
            Hits = hits,
            Page = result.Page ?? 0,
            HitsPerPage = result.HitsPerPage ?? 0,
            TotalHits = result.NbHits ?? 0,
            TotalPages = result.NbPages ?? 0,
            ProcessingTimeMs = result.ProcessingTimeMS ?? 0,
            Query = result.Query,
            Facets = ToFacets(result.Facets)
        };
    }

    private static THit? MapHit<THit>(object hit)
        where THit : class
    {
        if (hit is THit typedHit)
            return typedHit;

        if (hit is JsonElement element)
            return element.Deserialize<THit>(JsonOptions);

        var json = JsonSerializer.Serialize(hit, JsonOptions);
        return JsonSerializer.Deserialize<THit>(json, JsonOptions);
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

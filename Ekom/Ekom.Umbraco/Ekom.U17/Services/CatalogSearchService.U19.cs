#if UMBRACO_19
using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Search.Querying;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;
using UmbracoSearchConstants = Umbraco.Cms.Core.Constants.Search;

namespace Ekom.Umb.Services;

/// <summary>
/// Catalog search for Umbraco 19, where the legacy Examine indexes were replaced by the
/// provider based search abstraction. Relevance, analysis and matching are decided by the
/// registered search provider, so per-field boosts, fuzzy and wildcard settings on
/// <see cref="ProductQueryBase.SearchFields"/> do not apply here.
/// </summary>
internal sealed class CatalogSearchService : ICatalogSearchService
{
    // Mirrors the legacy Examine default, used when a request carries no page size.
    private const int DefaultMaxResults = 500;

    private readonly ILogger<CatalogSearchService> _logger;
    private readonly ISearcherResolver _searcherResolver;
    private readonly IPublishedContentCache _publishedContentCache;
    private readonly IVariationContextAccessor _variationContextAccessor;
    private readonly IContentTypeService _contentTypeService;
    private readonly IIdKeyMap _idKeyMap;
    private readonly Configuration _config;

    public CatalogSearchService(
        ILogger<CatalogSearchService> logger,
        ISearcherResolver searcherResolver,
        IPublishedContentCache publishedContentCache,
        IVariationContextAccessor variationContextAccessor,
        IContentTypeService contentTypeService,
        IIdKeyMap idKeyMap,
        Configuration config)
    {
        _logger = logger;
        _searcherResolver = searcherResolver;
        _publishedContentCache = publishedContentCache;
        _variationContextAccessor = variationContextAccessor;
        _contentTypeService = contentTypeService;
        _idKeyMap = idKeyMap;
        _config = config;
    }

    public async Task<(IEnumerable<SearchResultEntity> Results, long Total)> PublicQueryAsync(
        SearchRequest req,
        CancellationToken ct = default)
    {
        var (content, total) = await SearchAsync(req, ct).ConfigureAwait(false);
        var results = new List<SearchResultEntity>(content.Count);

        for (var i = 0; i < content.Count; i++)
        {
            var item = content[i];
            results.Add(new SearchResultEntity
            {
                Name = item.Name,
                Id = item.Id,
                Key = item.Key,
                Score = total - i,
                Path = item.Path,
                DocType = item.ContentType.Alias,
                ParentName = item.Parent()?.Name ?? string.Empty,
                ParentId = item.IsDocumentType("ekmVariant")
                    ? item.Parent()?.Parent()?.Id ?? item.Id
                    : item.Id,
                SKU = item.HasProperty("sku") ? item.Value<string>("sku") ?? string.Empty : string.Empty,
                Url = item.Url(),
            });
        }

        return (results, total);
    }

    public async Task<(IEnumerable<SearchResultEntity> Results, long Total)> InternalQueryAsync(
        SearchRequest req,
        CancellationToken ct = default)
    {
        var (content, total) = await SearchAsync(req, ct).ConfigureAwait(false);
        var results = new List<SearchResultEntity>(content.Count);

        for (var i = 0; i < content.Count; i++)
        {
            var item = content[i];
            var parentId = item.Parent()?.Id ?? -1;

            // Root level nodes are never catalog items, matching the legacy behaviour.
            if (parentId == -1)
            {
                continue;
            }

            results.Add(new SearchResultEntity
            {
                DocType = item.ContentType.Alias,
                Name = item.Name,
                Id = item.Id,
                Key = item.Key,
                Score = total - i,
                Path = item.Path,
                SKU = item.HasProperty("sku") ? item.Value<string>("sku") ?? string.Empty : string.Empty,
                ParentId = parentId,
            });
        }

        return (results, total);
    }

    public async Task<(IEnumerable<int> Ids, long Total)> ProductQueryAsync(
        SearchRequest req,
        CancellationToken ct = default)
    {
        var (content, total) = await SearchAsync(req, ct).ConfigureAwait(false);
        var ids = new List<int>(content.Count);

        foreach (var item in content)
        {
            ids.Add(item.Id);
        }

        return (ids, total);
    }

    private async Task<(IReadOnlyList<IPublishedContent> Content, long Total)> SearchAsync(
        SearchRequest? req,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (req == null || string.IsNullOrWhiteSpace(req.SearchQuery))
        {
            return (Array.Empty<IPublishedContent>(), 0);
        }

        var indexAlias = ResolveIndexAlias(req.ExamineIndex);

        try
        {
            var searcher = _searcherResolver.GetSearcher(indexAlias);
            if (searcher == null)
            {
                _logger.LogWarning("No searcher is registered for the search index. Index: {Index}", indexAlias);
                return (Array.Empty<IPublishedContent>(), 0);
            }

            var query = BuildQuery(req.SearchQuery);
            if (string.IsNullOrWhiteSpace(query))
            {
                return (Array.Empty<IPublishedContent>(), 0);
            }

            if (!TryBuildFilters(req, out var filters))
            {
                return (Array.Empty<IPublishedContent>(), 0);
            }

            var take = req.PageSize is > 0 ? req.PageSize.Value : DefaultMaxResults;
            var skip = req.Page is > 0 ? req.Page.Value : 0;
            var culture = _variationContextAccessor.VariationContext?.Culture;

            var result = await searcher.SearchAsync(
                    indexAlias,
                    query: query,
                    filters: filters,
                    culture: string.IsNullOrWhiteSpace(culture) ? null : culture,
                    accessContext: AccessContext.BypassProtection(),
                    skip: skip,
                    take: take)
                .ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            var content = new List<IPublishedContent>();
            foreach (var document in result.Documents)
            {
                if (document.ObjectType != UmbracoObjectTypes.Document)
                {
                    continue;
                }

                var item = _publishedContentCache.GetById(document.Id);
                if (item != null)
                {
                    content.Add(item);
                }
            }

            return (content, result.Total);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query search service. Query: {Query}. Index: {Index}", req.SearchQuery, indexAlias);
            return (Array.Empty<IPublishedContent>(), 0);
        }
    }

    private string ResolveIndexAlias(string? requestedIndex)
    {
        var alias = string.IsNullOrWhiteSpace(requestedIndex) ? _config.ExamineSearchIndex : requestedIndex;

        // "ExternalIndex" is the Ekom default from the legacy Examine setup and no longer exists.
        return string.IsNullOrWhiteSpace(alias) || alias.Equals("ExternalIndex", StringComparison.OrdinalIgnoreCase)
            ? UmbracoSearchConstants.IndexAliases.PublishedContent
            : alias;
    }

    private static string BuildQuery(string searchQuery)
    {
        // Stop words are left to the search provider's own text analysis.
        return SearchHelper.RemoveDiacritics(searchQuery).Trim();
    }

    private bool TryBuildFilters(SearchRequest req, out List<Filter> filters)
    {
        filters = [];

        if (req.NodeTypeAlias?.Length > 0)
        {
            var contentTypeKeys = new List<string>(req.NodeTypeAlias.Length);
            foreach (var alias in req.NodeTypeAlias)
            {
                var contentType = string.IsNullOrWhiteSpace(alias) ? null : _contentTypeService.Get(alias);
                if (contentType != null)
                {
                    contentTypeKeys.Add(contentType.Key.ToString("D"));
                }
            }

            // None of the requested document types exist, so nothing can match.
            if (contentTypeKeys.Count == 0)
            {
                return false;
            }

            filters.Add(new KeywordFilter(UmbracoSearchConstants.FieldNames.ContentTypeId, contentTypeKeys.ToArray(), false));
        }

        if (!string.IsNullOrWhiteSpace(req.SearchNodeById))
        {
            if (!TryResolveDocumentKey(req.SearchNodeById, out var ancestorKey))
            {
                return false;
            }

            filters.Add(new KeywordFilter(UmbracoSearchConstants.FieldNames.PathIds, [ancestorKey.ToString("D")], false));
        }

        return true;
    }

    private bool TryResolveDocumentKey(string value, out Guid key)
    {
        if (Guid.TryParse(value, out key))
        {
            return true;
        }

        if (int.TryParse(value, out var id))
        {
            var attempt = _idKeyMap.GetKeyForId(id, UmbracoObjectTypes.Document);
            if (attempt.Success)
            {
                key = attempt.Result;
                return true;
            }
        }

        key = Guid.Empty;
        return false;
    }
}
#endif

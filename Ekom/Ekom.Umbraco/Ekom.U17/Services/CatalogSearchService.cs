using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using Examine;
using Examine.Lucene.Search;
using Examine.Search;
using Lucene.Net.QueryParsers.Classic;
using Microsoft.Extensions.Logging;
using System.Text;
using Umbraco.Cms.Core;
using Umbraco.Cms.Infrastructure.Examine;
using Umbraco.Extensions;

namespace Ekom.Umb.Services;

internal sealed class CatalogSearchService : ICatalogSearchService
{
    private readonly ILogger<CatalogSearchService> _logger;
    private readonly IPublishedContentQuery _query;
    private readonly IExamineManager _examineManager;
    private readonly Configuration _config;

    public CatalogSearchService(
        IPublishedContentQuery query,
        ILogger<CatalogSearchService> logger,
        IExamineManager examineManager,
        Configuration config)
    {
        _logger = logger;
        _query = query;
        _examineManager = examineManager;
        _config = config;
    }

    public Task<(IEnumerable<SearchResultEntity> Results, long Total)> PublicQueryAsync(
        SearchRequest req,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var results = PublicQueryCore(req, out var total, ct);
        return Task.FromResult((results, total));
    }

    public Task<(IEnumerable<SearchResultEntity> Results, long Total)> InternalQueryAsync(
        SearchRequest req,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var results = InternalQueryCore(req, out var total, ct);
        return Task.FromResult((results, total));
    }

    public Task<(IEnumerable<int> Ids, long Total)> ProductQueryAsync(
        SearchRequest req,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var results = PublicQueryCore(req, out var total, ct);
        var ids = results.Select(x => x.Id);
        return Task.FromResult((ids, total));
    }

    private IEnumerable<SearchResultEntity> PublicQueryCore(SearchRequest req, out long total, CancellationToken ct)
    {
        total = 0;

        if (req == null || string.IsNullOrWhiteSpace(req.SearchQuery))
        {
            return Array.Empty<SearchResultEntity>();
        }

        ct.ThrowIfCancellationRequested();

        req.SearchFields = req.SearchFields?.Any() == true ? req.SearchFields : DefaultPublicFields();

        if (req.SearchFields?.Any() != true)
        {
            return Array.Empty<SearchResultEntity>();
        }

        var luceneQuery = new StringBuilder();

        try
        {
            var examineIndex = !string.IsNullOrWhiteSpace(req.ExamineIndex)
                ? req.ExamineIndex
                : _config.ExamineSearchIndex;

            if (!_examineManager.TryGetIndex(examineIndex, out var index) || index is not IUmbracoIndex)
            {
                _logger.LogWarning("Examine index not found or not an Umbraco index. Index: {Index}", examineIndex);
                return Array.Empty<SearchResultEntity>();
            }

            var searcher = index.Searcher ?? throw new InvalidOperationException("Searcher not found. " + examineIndex);
            var searchTerms = BuildSearchTerms(req.SearchQuery);

            if (searchTerms.Count == 0)
            {
                return Array.Empty<SearchResultEntity>();
            }

            BuildRequiredFieldQuery(req.SearchFields, searchTerms, luceneQuery);
            ct.ThrowIfCancellationRequested();

            var searchQuery = searcher.CreateQuery("content");
            if (searchQuery is LuceneSearchQueryBase luceneSearch)
            {
                luceneSearch.QueryParser.AllowLeadingWildcard = true;
            }

            var booleanOperation = searchQuery.NativeQuery(luceneQuery.ToString());

            if (req.NodeTypeAlias?.Any() == true)
            {
                booleanOperation = booleanOperation.And().GroupedOr(["__NodeTypeAlias"], req.NodeTypeAlias);
            }

            if (!string.IsNullOrWhiteSpace(req.SearchNodeById))
            {
                booleanOperation = booleanOperation.And().Field("ekmSearchPath", "|" + req.SearchNodeById + "|");
            }

            var results = _query.Search(
                    booleanOperation,
                    req.Page ?? 0,
                    req.PageSize ?? int.MaxValue,
                    out total)
                .OrderByDescending(x => x.Score);

            return results.Select(x => new SearchResultEntity
            {
                Name = x.Content.Name,
                Id = x.Content.Id,
                Key = x.Content.Key,
                Score = x.Score,
                Path = x.Content.Path,
                DocType = x.Content.ContentType.Alias,
                ParentName = x.Content.Parent != null ? x.Content.Parent.Name : string.Empty,
                ParentId = x.Content.IsDocumentType("ekmProduct")
                    ? x.Content.Id
                    : x.Content.IsDocumentType("ekmVariant")
                        ? x.Content.Parent?.Parent?.Id ?? x.Content.Id
                        : x.Content.Id,
                SKU = x.Content.HasProperty("sku") ? x.Content.Value<string>("sku") ?? string.Empty : string.Empty,
                Url = x.Content.Url(),
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query search service. Query: {Query}. Message: {Message}", req.SearchQuery, ex.Message);
            _logger.LogInformation("Lucene query: {LuceneQuery}", luceneQuery.ToString());
            total = 0;
            return Array.Empty<SearchResultEntity>();
        }
    }

    private IEnumerable<SearchResultEntity> InternalQueryCore(SearchRequest req, out long total, CancellationToken ct)
    {
        total = 0;

        if (req == null || string.IsNullOrEmpty(req.SearchQuery))
        {
            return Array.Empty<SearchResultEntity>();
        }

        req.SearchFields = req.SearchFields == null || !req.SearchFields.Any()
            ? DefaultInternalFields()
            : req.SearchFields;

        var luceneQuery = new StringBuilder();

        try
        {
            var examineIndex = !string.IsNullOrEmpty(req.ExamineIndex) ? req.ExamineIndex : _config.ExamineSearchIndex;

            if (!_examineManager.TryGetIndex(examineIndex, out var index) || index is not IUmbracoIndex)
            {
                _logger.LogWarning("Examine index not found or not an Umbraco index. Index: {Index}", examineIndex);
                return Array.Empty<SearchResultEntity>();
            }

            var searcher = index.Searcher ?? throw new InvalidOperationException("Searcher not found. " + examineIndex);
            var searchTerms = BuildSearchTerms(req.SearchQuery);

            for (var i = 0; i < searchTerms.Count; i++)
            {
                if (i != 0)
                {
                    luceneQuery.Append(" AND ");
                }

                if (i == 0)
                {
                    luceneQuery.Append('+');
                }

                luceneQuery.Append(" (");

                foreach (var field in req.SearchFields)
                {
                    luceneQuery.Append(" (");
                    luceneQuery.Append(BuildFieldClause(field, searchTerms[i]));
                    luceneQuery.Append(')');
                }

                luceneQuery.Append(')');
            }

            ct.ThrowIfCancellationRequested();

            var searchQuery = searcher.CreateQuery("content");
            if (searchQuery is LuceneSearchQueryBase luceneSearch)
            {
                luceneSearch.QueryParser.AllowLeadingWildcard = true;
            }

            var results = searchQuery.NativeQuery(luceneQuery.ToString()).Execute();

            return results.Select(x => new SearchResultEntity
            {
                DocType = x.Values.TryGetValue("__NodeTypeAlias", out var docType) ? docType : string.Empty,
                Name = x.Values.TryGetValue("nodeName", out var name) ? name : string.Empty,
                Id = int.TryParse(x.Id, out var id) ? id : 0,
                Key = x.Values.TryGetValue("__Key", out var keyValue) && Guid.TryParse(keyValue, out var key) ? key : Guid.Empty,
                Score = x.Score,
                Path = x.Values.TryGetValue("__Path", out var path) ? path : string.Empty,
                SKU = x.Values.TryGetValue("sku", out var sku) ? sku : string.Empty,
                ParentId = x.Values.TryGetValue("parentID", out var parentIdValue) && int.TryParse(parentIdValue, out var parentId) ? parentId : 0,
            }).Where(x => x.ParentId != -1);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query internal search service. Query: {Query}. Message: {Message}", req.SearchQuery, ex.Message);
            _logger.LogInformation("Lucene query: {LuceneQuery}", luceneQuery.ToString());
            return Array.Empty<SearchResultEntity>();
        }
    }

    private static List<EkomSearchField> DefaultPublicFields() =>
    [
        new() { Name = "nodeName", Booster = "^4.0" },
        new() { Name = "sku", Booster = "^10.0", SearchType = EkomSearchType.Wildcard },
        new() { Name = "title", Booster = "^4.0" },
        new() { Name = "description", Booster = "^2.0", SearchType = EkomSearchType.Wildcard },
        new() { Name = "searchTags", Booster = "^2.0", SearchType = EkomSearchType.Wildcard },
        new() { Name = "id", Booster = "^10.0", SearchType = EkomSearchType.Exact },
    ];

    private static List<EkomSearchField> DefaultInternalFields() =>
    [
        new() { Name = "nodeName", Booster = "^4.0", FuzzyConfiguration = "0.7" },
        new() { Name = "sku", Booster = "^12.0", SearchType = EkomSearchType.Wildcard },
        new() { Name = "title", Booster = "^4.0", FuzzyConfiguration = "0.7" },
        new() { Name = "description", Booster = "^2.0", SearchType = EkomSearchType.Wildcard },
        new() { Name = "searchTags", Booster = "^2.0", SearchType = EkomSearchType.Wildcard },
        new() { Name = "id", Booster = "^10.0", SearchType = EkomSearchType.Exact },
    ];

    private static List<string> BuildSearchTerms(string searchQuery)
    {
        var withoutStopWords = searchQuery.RemoveStopWords();
        var baseQuery = string.IsNullOrWhiteSpace(withoutStopWords) ? searchQuery : withoutStopWords;
        var cleanQuery = SearchHelper.RemoveDiacritics(baseQuery);

        return cleanQuery
            .Split([' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(QueryParser.Escape)
            .ToList();
    }

    private static void BuildRequiredFieldQuery(IEnumerable<EkomSearchField> fields, IReadOnlyList<string> searchTerms, StringBuilder luceneQuery)
    {
        for (var i = 0; i < searchTerms.Count; i++)
        {
            if (i > 0)
            {
                luceneQuery.Append(" AND ");
            }

            luceneQuery.Append("+(");
            var wroteAnyFieldClause = false;

            foreach (var field in fields)
            {
                var clause = BuildFieldClause(field, searchTerms[i]);
                if (string.IsNullOrWhiteSpace(clause))
                {
                    continue;
                }

                if (wroteAnyFieldClause)
                {
                    luceneQuery.Append(" OR ");
                }

                luceneQuery.Append(clause);
                wroteAnyFieldClause = true;
            }

            luceneQuery.Append(')');
        }
    }

    private static string BuildFieldClause(EkomSearchField field, string term)
    {
        var booster = string.IsNullOrWhiteSpace(field.Booster) ? string.Empty : field.Booster;

        return field.SearchType switch
        {
            EkomSearchType.Wildcard => $"{field.Name}:*{term}*{booster}",
            EkomSearchType.Fuzzy => $"{field.Name}:{term}~{field.FuzzyConfiguration}{booster}",
            EkomSearchType.FuzzyAndWilcard => $"({field.Name}:*{term}* OR {field.Name}:{term}~{field.FuzzyConfiguration}){booster}",
            EkomSearchType.Exact => $"{field.Name}:{term}{booster}",
            _ => $"{field.Name}:{term}{booster}",
        };
    }
}

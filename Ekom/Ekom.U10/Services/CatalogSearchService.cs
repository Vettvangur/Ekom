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

public class CatalogSearchService : ICatalogSearchService
{
    private readonly ILogger _logger;
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

    // =========================
    // Async
    // =========================

    public virtual Task<(IEnumerable<SearchResultEntity> Results, long Total)> PublicQueryAsync(
        SearchRequest req,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var results = PublicQueryCore(req, out var total, ct);
        return Task.FromResult((results, total));
    }

    public virtual Task<(IEnumerable<SearchResultEntity> Results, long Total)> InternalQueryAsync(
        SearchRequest req,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var results = InternalQueryCore(req, out var total, ct);
        return Task.FromResult((results, total));
    }

    public virtual Task<(IEnumerable<int> Ids, long Total)> ProductQueryAsync(
        SearchRequest req,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var results = PublicQueryCore(req, out var total, ct);
        var ids = results.Select(x => x.Id);
        return Task.FromResult((ids, total));
    }

    // =========================
    // Core implementations with cancellation checkpoints
    // =========================

    private IEnumerable<SearchResultEntity> PublicQueryCore(SearchRequest req, out long total, CancellationToken ct)
    {
        total = 0;

        if (req == null || string.IsNullOrWhiteSpace(req.SearchQuery))
            return Array.Empty<SearchResultEntity>();

        ct.ThrowIfCancellationRequested();

        var defaultFields = new List<EkomSearchField>
    {
        new() { Name = "nodeName", Booster = "^4.0" },
        new() { Name = "sku", Booster = "^10.0", SearchType = EkomSearchType.Wildcard },
        new() { Name = "title", Booster = "^4.0" },
        new() { Name = "description", Booster = "^2.0", SearchType = EkomSearchType.Wildcard },
        new() { Name = "searchTags", Booster = "^2.0", SearchType = EkomSearchType.Wildcard },
        new() { Name = "id", Booster = "^10.0", SearchType = EkomSearchType.Exact }
    };

        // Default fields if missing OR empty
        req.SearchFields = (req.SearchFields?.Any() == true) ? req.SearchFields : defaultFields;

        // If still empty for some reason, bail (prevents "+ ()")
        if (req.SearchFields?.Any() != true)
            return Array.Empty<SearchResultEntity>();

        var luceneQuery = new StringBuilder();

        try
        {
            ct.ThrowIfCancellationRequested();

            var examineIndex = !string.IsNullOrWhiteSpace(req.ExamineIndex)
                ? req.ExamineIndex
                : _config.ExamineSearchIndex;

            if (!_examineManager.TryGetIndex(examineIndex, out var index) || index is not IUmbracoIndex)
            {
                _logger.LogWarning("Examine index not found or not an Umbraco index. Index: {Index}", examineIndex);
                return Array.Empty<SearchResultEntity>();
            }

            var searcher = index.Searcher ?? throw new Exception("Searcher not found. " + examineIndex);

            // Build terms: stopwords -> diacritics -> tokenize -> escape
            var withoutStopWords = req.SearchQuery.RemoveStopWords();
            var baseQuery = string.IsNullOrWhiteSpace(withoutStopWords) ? req.SearchQuery : withoutStopWords;
            var cleanQuery = SearchHelper.RemoveDiacritics(baseQuery);

            var searchTerms = cleanQuery
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(QueryParser.Escape)
                .ToList();

            // No usable tokens => no results (prevents empty query / weird parse)
            if (searchTerms.Count == 0)
                return Array.Empty<SearchResultEntity>();

            ct.ThrowIfCancellationRequested();

            // Each term is required (+) and must match at least one of the fields (OR across fields)
            for (var i = 0; i < searchTerms.Count; i++)
            {
                var term = searchTerms[i];

                if (i > 0)
                    luceneQuery.Append(" AND ");

                // Require each term-group
                luceneQuery.Append("+(");

                var wroteAnyFieldClause = false;

                foreach (var field in req.SearchFields)
                {
                    var clause = BuildFieldClause(field, term);
                    if (string.IsNullOrWhiteSpace(clause))
                        continue;

                    // OR across fields within a term-group
                    if (wroteAnyFieldClause)
                        luceneQuery.Append(" OR ");

                    luceneQuery.Append(clause);
                    wroteAnyFieldClause = true;
                }

                luceneQuery.Append(")");

                // Safety: never allow "+()" to escape
                if (!wroteAnyFieldClause)
                    return Array.Empty<SearchResultEntity>();
            }

            ct.ThrowIfCancellationRequested();

            IQuery searchQuery = searcher.CreateQuery("content");
            if (searchQuery is LuceneSearchQueryBase luceneSearch)
            {
                luceneSearch.QueryParser.AllowLeadingWildcard = true;
            }

            var booleanOperation = searchQuery.NativeQuery(luceneQuery.ToString());

            if (req.NodeTypeAlias?.Any() == true)
            {
                booleanOperation = booleanOperation.And().GroupedOr(
                    ["__NodeTypeAlias"],
                    req.NodeTypeAlias);
            }

            if (!string.IsNullOrWhiteSpace(req.SearchNodeById))
            {
                booleanOperation = booleanOperation.And().Field("ekmSearchPath", "|" + req.SearchNodeById + "|");
            }

            ct.ThrowIfCancellationRequested();

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
                ParentName = x.Content.Parent != null ? x.Content.Parent.Name : "",
                ParentId = x.Content.IsDocumentType("ekmProduct")
                    ? x.Content.Id
                    : x.Content.IsDocumentType("ekmVariant")
                        ? x.Content.Parent?.Parent?.Id ?? x.Content.Id
                        : x.Content.Id,
                SKU = x.Content.HasProperty("sku") ? x.Content.Value<string>("sku") ?? "" : "",
                Url = x.Content.Url()
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to query search service. Query: {Query}. Message: {Message}",
                req.SearchQuery,
                ex.Message);

            _logger.LogInformation("Lucene query: {LuceneQuery}", luceneQuery.ToString());

            total = 0;
            return Array.Empty<SearchResultEntity>();
        }

        static string BuildFieldClause(EkomSearchField field, string term)
        {
            // Lucene classic syntax: field:value (no space after ':')
            // Wrap term-values with parentheses only where needed; keep it simple and valid.

            var booster = string.IsNullOrWhiteSpace(field.Booster) ? "" : field.Booster;

            return field.SearchType switch
            {
                EkomSearchType.Wildcard =>
                    $"{field.Name}:*{term}*{booster}",

                EkomSearchType.Fuzzy =>
                    $"{field.Name}:{term}~{field.FuzzyConfiguration}{booster}",

                EkomSearchType.FuzzyAndWilcard =>
                    // (wild OR fuzzy) inside a single field-clause
                    $"({field.Name}:*{term}* OR {field.Name}:{term}~{field.FuzzyConfiguration}){booster}",

                EkomSearchType.Exact =>
                    $"{field.Name}:{term}{booster}",

                _ =>
                    // Default to exact if unknown
                    $"{field.Name}:{term}{booster}"
            };
        }
    }

    private IEnumerable<SearchResultEntity> InternalQueryCore(SearchRequest req, out long total, CancellationToken ct)
    {
        total = 0;

        if (req == null || string.IsNullOrEmpty(req.SearchQuery))
            return new List<SearchResultEntity>();

        ct.ThrowIfCancellationRequested();

        var luceneQuery = new StringBuilder();

        var defaultFields = new List<EkomSearchField>
        {
            new() { Name = "nodeName", Booster = "^4.0", FuzzyConfiguration = "0.7" },
            new() { Name = "sku", Booster = "^12.0", SearchType = EkomSearchType.Wildcard },
            new() { Name = "title", Booster = "^4.0", FuzzyConfiguration = "0.7" },
            new() { Name = "description", Booster = "^2.0", SearchType = EkomSearchType.Wildcard },
            new() { Name = "searchTags", Booster = "^2.0", SearchType = EkomSearchType.Wildcard },
            new() { Name = "id", Booster = "^10.0", SearchType = EkomSearchType.Exact }
        };

        req.SearchFields = req.SearchFields == null || !req.SearchFields.Any()
            ? defaultFields
            : req.SearchFields;

        try
        {
            ct.ThrowIfCancellationRequested();

            var examineIndex = !string.IsNullOrEmpty(req.ExamineIndex) ? req.ExamineIndex : _config.ExamineSearchIndex;

            if (!_examineManager.TryGetIndex(examineIndex, out var index) || index is not IUmbracoIndex)
            {
                _logger.LogWarning("Examine index not found or not an Umbraco index. Index: {Index}", examineIndex);
                return new List<SearchResultEntity>();
            }

            var searcher = index.Searcher ?? throw new Exception("Searcher not found. " + examineIndex);

            var queryWithOutStopWords = req.SearchQuery.RemoveStopWords();
            var cleanQuery = SearchHelper.RemoveDiacritics(string.IsNullOrEmpty(queryWithOutStopWords) ? req.SearchQuery : queryWithOutStopWords);

            var searchTerms = cleanQuery
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(QueryParser.Escape)
                .ToList();

            ct.ThrowIfCancellationRequested();

            for (var i = 0; i < searchTerms.Count; i++)
            {
                var term = searchTerms[i];

                if (i != 0)
                    luceneQuery.Append(" AND ");

                if (i == 0)
                    luceneQuery.Append("+");

                luceneQuery.Append(" (");

                foreach (var field in req.SearchFields)
                {
                    luceneQuery.Append(" (");

                    if (field.SearchType == EkomSearchType.Wildcard || field.SearchType == EkomSearchType.FuzzyAndWilcard)
                        luceneQuery.Append("(" + field.Name + ": " + "*" + term + "*" + ")" + (string.IsNullOrEmpty(field.Booster) ? "" : field.Booster));

                    if (field.SearchType == EkomSearchType.Fuzzy || field.SearchType == EkomSearchType.FuzzyAndWilcard)
                        luceneQuery.Append(" (" + field.Name + ": " + term + "~" + field.FuzzyConfiguration + ")" + (string.IsNullOrEmpty(field.Booster) ? "" : field.Booster));

                    if (field.SearchType == EkomSearchType.Exact)
                        luceneQuery.Append(" (" + field.Name + ": " + term + ") " + (string.IsNullOrEmpty(field.Booster) ? "" : field.Booster));

                    luceneQuery.Append(")");
                }

                luceneQuery.Append(")");
            }

            ct.ThrowIfCancellationRequested();

            IQuery searchQuery = searcher.CreateQuery("content");
            ((LuceneSearchQueryBase)searchQuery).QueryParser.AllowLeadingWildcard = true;

            var booleanOperation = searchQuery.NativeQuery(luceneQuery.ToString());

            var results = booleanOperation.Execute();

            var entities = results.Select(x => new SearchResultEntity
            {
                DocType = x.Values.TryGetValue("__NodeTypeAlias", out var dt) ? dt : "",
                Name = x.Values.TryGetValue("nodeName", out var name) ? name : "",
                Id = int.TryParse(x.Id, out var id) ? id : 0,
                Key = x.Values.TryGetValue("__Key", out var keyStr) && Guid.TryParse(keyStr, out var key) ? key : Guid.Empty,
                Score = x.Score,
                Path = x.Values.TryGetValue("__Path", out var path) ? path : "",
                SKU = x.Values.TryGetValue("sku", out var sku) ? sku : "",
                ParentId = x.Values.TryGetValue("parentID", out var pidStr) && int.TryParse(pidStr, out var pid) ? pid : 0
            });

            return entities.Where(x => x.ParentId != -1);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query internal search service. Query: {Query}. Message: {Message}", req.SearchQuery, ex.Message);
            _logger.LogInformation(luceneQuery.ToString());
            return new List<SearchResultEntity>();
        }
    }
}

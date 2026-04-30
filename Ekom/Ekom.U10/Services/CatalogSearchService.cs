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

            // Build terms: stopwords -> tokenize. Normalized terms target *_normalized fields.
            var withoutStopWords = req.SearchQuery.RemoveStopWords();
            var baseQuery = string.IsNullOrWhiteSpace(withoutStopWords) ? req.SearchQuery : withoutStopWords;
            var searchTerms = ExamineSearchTextNormalizer.Tokenize(baseQuery, normalize: false);
            var normalizedSearchTerms = ExamineSearchTextNormalizer.Tokenize(baseQuery, normalize: true);

            // No usable tokens => no results (prevents empty query / weird parse)
            if (searchTerms.Count == 0 && normalizedSearchTerms.Count == 0)
                return Array.Empty<SearchResultEntity>();

            ct.ThrowIfCancellationRequested();

            var normalizedFieldNames = GetNormalizedFieldNames();
            luceneQuery.Append(BuildLuceneQuery(req.SearchFields, searchTerms, normalizedSearchTerms, normalizedFieldNames));

            if (luceneQuery.Length == 0)
                return Array.Empty<SearchResultEntity>();

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
            var baseQuery = string.IsNullOrEmpty(queryWithOutStopWords) ? req.SearchQuery : queryWithOutStopWords;
            var searchTerms = ExamineSearchTextNormalizer.Tokenize(baseQuery, normalize: false);
            var normalizedSearchTerms = ExamineSearchTextNormalizer.Tokenize(baseQuery, normalize: true);

            if (searchTerms.Count == 0 && normalizedSearchTerms.Count == 0)
                return Array.Empty<SearchResultEntity>();

            ct.ThrowIfCancellationRequested();

            var normalizedFieldNames = GetNormalizedFieldNames();
            luceneQuery.Append(BuildLuceneQuery(req.SearchFields, searchTerms, normalizedSearchTerms, normalizedFieldNames));

            if (luceneQuery.Length == 0)
                return Array.Empty<SearchResultEntity>();

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

    private HashSet<string> GetNormalizedFieldNames()
    {
        return _config.ExamineSearchNormalizedFields
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildLuceneQuery(
        IReadOnlyCollection<EkomSearchField> fields,
        IReadOnlyList<string> searchTerms,
        IReadOnlyList<string> normalizedSearchTerms,
        HashSet<string> normalizedFieldNames)
    {
        var query = new StringBuilder();
        var termCount = Math.Max(searchTerms.Count, normalizedSearchTerms.Count);

        for (var i = 0; i < termCount; i++)
        {
            var term = i < searchTerms.Count ? QueryParser.Escape(searchTerms[i]) : string.Empty;
            var normalizedTerm = i < normalizedSearchTerms.Count ? QueryParser.Escape(normalizedSearchTerms[i]) : string.Empty;

            if (string.IsNullOrWhiteSpace(term) && string.IsNullOrWhiteSpace(normalizedTerm))
                continue;

            if (query.Length > 0)
                query.Append(" AND ");

            query.Append("+(");

            var wroteAnyFieldClause = false;
            foreach (var field in fields)
            {
                if (string.IsNullOrWhiteSpace(field.Name))
                    continue;

                if (!string.IsNullOrWhiteSpace(term))
                    wroteAnyFieldClause = AppendFieldClause(query, field, term, wroteAnyFieldClause);

                if (!string.IsNullOrWhiteSpace(normalizedTerm) && normalizedFieldNames.Contains(field.Name))
                {
                    var normalizedField = new EkomSearchField
                    {
                        Name = ExamineSearchTextNormalizer.NormalizedFieldName(field.Name),
                        SearchType = field.SearchType,
                        FuzzyConfiguration = field.FuzzyConfiguration,
                        Booster = field.Booster
                    };

                    wroteAnyFieldClause = AppendFieldClause(query, normalizedField, normalizedTerm, wroteAnyFieldClause);
                }
            }

            query.Append(')');

            if (!wroteAnyFieldClause)
                return string.Empty;
        }

        return query.ToString();
    }

    private static bool AppendFieldClause(StringBuilder query, EkomSearchField field, string term, bool wroteAnyFieldClause)
    {
        var clause = BuildFieldClause(field, term);
        if (string.IsNullOrWhiteSpace(clause))
            return wroteAnyFieldClause;

        if (wroteAnyFieldClause)
            query.Append(" OR ");

        query.Append(clause);
        return true;
    }

    private static string BuildFieldClause(EkomSearchField field, string term)
    {
        // Lucene classic syntax: field:value (no space after ':')
        // Wrap term-values with parentheses only where needed; keep it simple and valid.

        if (string.IsNullOrWhiteSpace(field.Name))
            return string.Empty;

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

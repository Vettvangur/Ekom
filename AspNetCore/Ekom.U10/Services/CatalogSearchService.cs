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

namespace Ekom.Umb.Services
{
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

        public virtual IEnumerable<int> ProductQuery(SearchRequest req, out long total)
        {
            return Query(req, out total).Select(x => x.Id);
        }

        public IEnumerable<SearchResultEntity> Query(SearchRequest req, out long total)
        {
            total = 0;

            if (req == null || string.IsNullOrEmpty(req.SearchQuery))
            {
                return null;
            }

            var luceneQuery = new StringBuilder();

            var defaultFields = new List<EkomSearchField>()
            {
                new EkomSearchField()
                {
                    Name = "nodeName",
                    Booster = "^4.0"
                },
                new EkomSearchField()
                {
                    Name = "sku",
                    Booster = "^10.0",
                    SearchType = EkomSearchType.Wildcard
                },
                new EkomSearchField()
                {
                    Name = "title",
                    Booster = "^4.0"
                },
                 new EkomSearchField()
                {
                    Name = "description",
                    Booster = "^2.0"
                },
                new EkomSearchField()
                {
                    Name = "searchTags",
                    Booster = "^2.0"
                },
                new EkomSearchField()
                {
                    Name = "id",
                    Booster = "^10.0",
                    SearchType = EkomSearchType.Exact
                }
            };

            req.SearchFields = req.SearchFields == null ? defaultFields : req.SearchFields;

            try
            {
                var examineIndex = !string.IsNullOrEmpty(req.ExamineIndex) ? req.ExamineIndex : _config.ExamineSearchIndex;
                if (_examineManager.TryGetIndex(examineIndex, out var index) || !(index is IUmbracoIndex umbIndex))
                {
                    var searcher = index.Searcher;

                    if (searcher == null)
                    {
                        throw new Exception("Searcher not found. " + examineIndex);
                    }

                    var queryWithOutStopWords = req.SearchQuery.RemoveStopWords();

                    var cleanQuery = SearchHelper.RemoveDiacritics(string.IsNullOrEmpty(queryWithOutStopWords) ? req.SearchQuery : queryWithOutStopWords);

                    var searchTerms = cleanQuery
                        .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(QueryParser.Escape);

                    int i = 0;
                    foreach (var term in searchTerms)
                    {
                        if (i != 0)
                        {
                            luceneQuery.Append(" AND ");
                        }

                        if (i == 0)
                        {
                            luceneQuery.Append("+");
                        }

 
                        luceneQuery.Append(" (");
                        foreach (var field in req.SearchFields)
                        {
                            luceneQuery.Append(" (");

                            if (field.SearchType == EkomSearchType.Wildcard || field.SearchType == EkomSearchType.FuzzyAndWilcard)
                            {
                                luceneQuery.Append("(" + field.Name + ": " + "*" + term + "*" + ")" + (!string.IsNullOrEmpty(field.Booster) ? field.Booster : ""));
                            }

                            if (field.SearchType == EkomSearchType.Fuzzy || field.SearchType == EkomSearchType.FuzzyAndWilcard)
                            {
                                luceneQuery.Append(" (" + field.Name + ": " + term + "~" + field.FuzzyConfiguration + ")" + (!string.IsNullOrEmpty(field.Booster) ? field.Booster : ""));
                            }

                            if (field.SearchType == EkomSearchType.Exact)
                            {
                                luceneQuery.Append(" (" + field.Name + ": " + term + ") " + (!string.IsNullOrEmpty(field.Booster) ? field.Booster : ""));
                            }

                            luceneQuery.Append(")");
                        }
                        
                        luceneQuery.Append(")");
                        
                        i++;
                    }

                    IQuery searchQuery = searcher.CreateQuery("content");

                    ((LuceneSearchQueryBase)searchQuery).QueryParser.AllowLeadingWildcard = true;

                    var booleanOperation = searchQuery.NativeQuery(luceneQuery.ToString());

                    if (req.NodeTypeAlias != null && req.NodeTypeAlias.Any())
                    {
                        booleanOperation = booleanOperation.And().GroupedOr(new string[1] {
                        "__NodeTypeAlias"
                    }, req.NodeTypeAlias);
                    }

                    if (!string.IsNullOrEmpty(req.SearchNodeById))
                    {
                        booleanOperation = booleanOperation.And().Field("ekmSearchPath", "|" + req.SearchNodeById + "|");
                    }

                    var results = _query.Search(booleanOperation, req.Page.HasValue ? req.Page.Value : 0, req.PageSize.HasValue ? req.PageSize.Value : int.MaxValue, out total).OrderByDescending(x => x.Score);

                    var searchResultEntities = results.Select(x => new SearchResultEntity()
                    {
                        Name = x.Content.Name,
                        Id = x.Content.Id,
                        Key = x.Content.Key,
                        Score = x.Score,
                        Path = x.Content.Path,
                        DocType = x.Content.ContentType.Alias,
                        ParentName = x.Content.Parent != null ? x.Content.Parent.Name : "",
                        ParentId = x.Content.IsDocumentType("ekmProduct") ? x.Content.Id : x.Content.IsDocumentType("ekmVariant") ? x.Content.Parent.Parent.Id : x.Content.Id,
                        SKU = x.Content.HasProperty("sku") ? x.Content.Value<string>("sku") : "",
                        Url = x.Content.Url()
                    });

                    return searchResultEntities;
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query search service. Query: " + req.SearchQuery + " Message: " + ex.Message);
                _logger.LogInformation(luceneQuery.ToString());
            }

            return null;
        }
    }
}

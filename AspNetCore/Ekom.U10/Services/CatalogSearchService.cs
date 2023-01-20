using Ekom.Models;
using Ekom.Services;
using Examine;
using Examine.Lucene.Search;
using Examine.Search;
using Lucene.Net.QueryParsers.Classic;
using Microsoft.Extensions.Logging;
using System.Text;
using Umbraco.Cms.Core;
using Umbraco.Cms.Infrastructure.Examine;
using Umbraco.Extensions;
using static Ekom.Utilities.SearchHelper;

namespace Ekom.Umb.Services
{
    class CatalogSearchService : ICatalogSearchService
    {
        private readonly ILogger _logger;
        private readonly IPublishedContentQuery _query;
        private readonly IExamineManager _examineManager;
        public CatalogSearchService(
            IPublishedContentQuery query,
            ILogger<CatalogSearchService> logger,
            IExamineManager examineManager)
        {
            _logger = logger;
            _query = query;
            _examineManager = examineManager;
        }
        public IEnumerable<SearchResultEntity> QueryCatalog(string query, out long totalRecords, int take = 30)
        {
            totalRecords = 0;
            var luceneQuery = new StringBuilder();

            try
            {
                if (_examineManager.TryGetIndex("InternalIndex", out var index) || !(index is IUmbracoIndex umbIndex))
                {
                    var fields = new List<SearchField>()
                    {
                        new SearchField()
                        {
                            Name = "nodeName",
                            Booster = "^2.0"
                        },
                        new SearchField()
                        {
                            Name = "sku",
                            Booster = "^5.0",
                            SearchType = SearchType.Wildcard
                        },
                        new SearchField()
                        {
                            Name = "title",
                            Booster = "^2.0"
                        },
                        new SearchField()
                        {
                            Name = "id",
                            Booster = "^10.0",
                            SearchType = SearchType.Exact
                        }
                    };

                    var searcher = index.Searcher;

                    if (searcher == null)
                    {
                        throw new Exception("Searcher not found. InternalIndex");
                    }

                    var queryWithOutStopWords = query.RemoveStopWords();

                    var cleanQuery = RemoveDiacritics(string.IsNullOrEmpty(queryWithOutStopWords) ? query : queryWithOutStopWords);

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

                        if (string.IsNullOrEmpty(queryWithOutStopWords))
                        {
                            luceneQuery.Append(" (");
                            luceneQuery.Append("*" + term + "*");
                            luceneQuery.Append(" " + term + ("~" + "0.5"));
                            luceneQuery.Append(")");
                        }
                        else
                        {
                            luceneQuery.Append(" (");
                            foreach (var field in fields)
                            {
                                luceneQuery.Append(" (");
                                if (field.SearchType == SearchType.Wildcard || field.SearchType == SearchType.FuzzyAndWilcard)
                                {
                                    luceneQuery.Append("(" + FieldCultureName(field.Name) + ": " + "*" + term + "*" + ")" + (!string.IsNullOrEmpty(field.Booster) ? field.Booster : ""));
                                }

                                if (field.SearchType == SearchType.Fuzzy || field.SearchType == SearchType.FuzzyAndWilcard)
                                {
                                    luceneQuery.Append(" (" + FieldCultureName(field.Name) + ": " + term + "~" + field.FuzzyConfiguration + ")" + (!string.IsNullOrEmpty(field.Booster) ? field.Booster : ""));
                                }

                                if (field.SearchType == SearchType.Exact)
                                {
                                    luceneQuery.Append(" (" + FieldCultureName(field.Name) + ": " + term + ") " + (!string.IsNullOrEmpty(field.Booster) ? field.Booster : ""));
                                }

                                luceneQuery.Append(")");
                            }
                            luceneQuery.Append(")");
                        }
                        
                        i++;
                    }

                    IQuery searchQuery = searcher.CreateQuery("content");

                    ((LuceneSearchQueryBase)searchQuery).QueryParser.AllowLeadingWildcard = true;

                    var booleanOperation = searchQuery.NativeQuery(luceneQuery.ToString());

                    var results = _query.Search(booleanOperation, 0, take, out totalRecords).OrderByDescending(x => x.Score);

                    var searchResultEntities = results.Select(x => new SearchResultEntity()
                    {
                        Name = x.Content.Name,
                        Id = x.Content.Id,
                        Key = x.Content.Key,
                        Score = x.Score,
                        Path = x.Content.Path,
                        DocType = x.Content.ContentType.Alias,
                        ParentName = x.Content.Parent != null ? x.Content.Parent.Name : "",
                        SKU = x.Content.HasProperty("sku") ? x.Content.Value<string>("sku") : "",
                        Url = x.Content.Url()
                    });

                    return searchResultEntities;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query catalog search service. Query: " + query + " Message: " + ex.Message);
                _logger.LogInformation(luceneQuery.ToString());
            }

            return null;
        }
    }
}

using Examine;
using Lucene.Net.QueryParsers;
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Examine;

namespace Ekom.Services
{
    /// <summary>
    /// Intended for Ekom library users, will have more Ekom specific functionality later on
    /// </summary>
    public class SearchService
    {
        /// <summary>
        /// SearchService Instance
        /// </summary>
        public static SearchService Instance => Current.Factory.GetInstance<SearchService>();

        ILogger _logger;
        IExamineManager _examineManager;
        /// <summary>
        /// ctor
        /// </summary>
        public SearchService(
            ILogger logger,
            IExamineManager examineManager)
        {
            _examineManager = examineManager;
        }

        /// <summary>
        /// Intended for Ekom library users, will have more Ekom specific functionality later on
        /// Builds and run the Lucene query.
        /// </summary>
        public ISearchResults SearchResult(string query, string searchProvider, out long total)
        {
            total = 0;

            if (_examineManager.TryGetSearcher(searchProvider, out ISearcher searcher))
            {
                var luceneQuery = new StringBuilder();

                if (!string.IsNullOrWhiteSpace(query))
                {
                    var searchTerms = query
                        .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(QueryParser.Escape)
                        .SelectMany(st => new[] { st + "* " + st + "~0.6" });

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

                        luceneQuery.Append("(");
                        luceneQuery.Append(term);
                        luceneQuery.Append(")");

                        i++;
                    }
                }

                ISearchResults searchResults;

                // If no filters were selected search for nodes with ID 0 that will return no results.
                if (luceneQuery.Length > 0)
                {
                    var rawQuery = searcher.CreateQuery("content").NativeQuery(luceneQuery.ToString());
                    searchResults = rawQuery.Execute();
                }
                else
                {
                    searchResults = EmptySearchResults.Instance;
                }

                total = searchResults.TotalItemCount;

                return searchResults;
            }
            else
            {
                _logger.Error<SearchService>($"Unable to get Searcher {searchProvider}!");

                return null;
            }
        }
    }
}

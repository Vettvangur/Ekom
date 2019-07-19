using Ekom.Models.Abstractions;
using Examine;
using log4net;
using Lucene.Net.QueryParsers;
using System;
using System.Linq;
using System.Reflection;
using System.Text;

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
        public static SearchService Instance => Configuration.container.GetInstance<SearchService>();

        ExamineManagerBase _examineManager;
        /// <summary>
        /// ctor
        /// </summary>
        public SearchService(ExamineManagerBase examineManager)
        {
            _examineManager = examineManager;
        }

        /// <summary>
        /// Intended for Ekom library users, will have more Ekom specific functionality later on
        /// Builds and run the Lucene query.
        /// </summary>
        public ISearchResults SearchResult(string query, string searchProvider, out int total)
        {
            total = 0;

            try
            {
                var searcher = _examineManager.SearchProviderCollection[searchProvider];
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

                ISearchResults ISearchResults;

                // If no filters were selected search for nodes with ID 0 that will return no results.
                if (luceneQuery.Length > 0)
                {
                    var rawQuery = searcher.CreateSearchCriteria().RawQuery(luceneQuery.ToString());
                    ISearchResults = searcher.Search(rawQuery);
                }
                else
                {
                    ISearchResults = searcher.Search(searcher.CreateSearchCriteria().Id(0).Compile());
                }

                total = ISearchResults.TotalItemCount;

                return ISearchResults;
            }
            catch (Exception ex)
            {
                Log.Error("ISearchResult Error!", ex);

                return null;
            }
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}

using Examine;
using log4net;
using Lucene.Net.QueryParsers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Services
{
    public class SearchService
    {
        /// <summary>
        /// Builds and run the Lucene query.
        /// </summary>
        public static ISearchResults SearchResult(string query, string searchProvider, out int total)
        {
            total = 0;

            try
            {
                var searcher = ExamineManager.Instance.SearchProviderCollection[searchProvider];

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
                    var rawQuery = searcher.CreateSearchCriteria().RawQuery(luceneQuery.ToString());
                    searchResults = searcher.Search(rawQuery);
                }
                else
                {
                    searchResults = searcher.Search(searcher.CreateSearchCriteria().Id(0).Compile());
                }

                total = searchResults.TotalItemCount;

                return searchResults;

            }
            catch (Exception ex)
            {
                Log.Error("SearchResult Error!", ex);

                return null;
            }

        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}

using Examine;
using Lucene.Net.QueryParsers;
using System;
using System.Linq;
using System.Text;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Examine;

namespace Ekom.Services
{
    /// <summary>
    /// Quick and easy examine querying
    /// </summary>
    public class ExamineService
    {
        /// <summary>
        /// ExamineService Instance
        /// </summary>
        public static ExamineService Instance => Current.Factory.GetInstance<ExamineService>();

        public static DateTime ConvertToDatetime(string value)
        {
            return DateTime.ParseExact(value, "yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        }

        readonly ILogger _logger;
        readonly Configuration _config;
        readonly IExamineManager _examineMgr;
        /// <summary>
        /// Initializes a new instance of the <see cref="ExamineService"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="config">The configuration.</param>
        /// <param name="examineMgr">The examine manager.</param>
        public ExamineService(
            ILogger logger,
            Configuration config,
            IExamineManager examineMgr)
        {
            _logger = logger;
            _config = config;
            _examineMgr = examineMgr;
        }

        public ISearchResult GetExamineNode(int Id)
        {
            if (_examineMgr.TryGetIndex(_config.ExamineIndex, out IIndex index))
            {
                var searcher = index.GetSearcher();

                var results = searcher.CreateQuery("content")
                    .Id(Id)
                    .Execute()
                    ;

                return results.FirstOrDefault();
            }

            _logger.Warn<ExamineService>("GetNodeFromExamine Failed. Node with Id " + Id + " not found.");
            return null;
        }
        public ISearchResult GetExamineNode(string Key)
        {
            if (string.IsNullOrEmpty(Key))
            {
                throw new ArgumentNullException(nameof(Key));
            }

            if (_examineMgr.TryGetIndex(_config.ExamineIndex, out IIndex index))
            {
                var searcher = index.GetSearcher();
                var results = searcher.CreateQuery()
                    .NativeQuery($"__Key: \"{Key.Replace("-", " ")}\"")
                    .Execute();

                return results.FirstOrDefault();
            }

            _logger.Warn<ExamineService>("GetNodeFromExamine Failed. Node with Key " + Key + " not found.");
            return null;
        }

        /// <summary>
        /// Intended for Ekom library users, will have more Ekom specific functionality later on
        /// Builds and runS the Lucene query.
        /// </summary>
        public ISearchResults SearchResult(string query, string examineIndex, out long total)
        {
            total = 0;

            if (_examineMgr.TryGetIndex(examineIndex, out IIndex index))
            {
                var searcher = index.GetSearcher();

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
                _logger.Error<ExamineService>($"Unable to get Searcher {examineIndex}!");

                return null;
            }
        }
    }
}

using Ekom.Interfaces;
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
    /// <inheritDoc/>
    public class ExamineService : IExamineService
    {
        /// <summary>
        /// ExamineService Instance
        /// </summary>
        public static IExamineService Instance => Current.Factory.GetInstance<IExamineService>();


        public static DateTime ConvertToDatetime(string value)
        {
            try
            {
                return new DateTime(Convert.ToInt64(value));
            }
            catch
            {
                return DateTime.ParseExact(value, "yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        readonly ILogger _logger;
        readonly Configuration _config;
        readonly IExamineManager _examineMgr;
        readonly IndexRebuilder _rebuilder;
        /// <summary>
        /// Initializes a new instance of the <see cref="ExamineService"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="config">The configuration.</param>
        /// <param name="examineMgr">The examine manager.</param>
        public ExamineService(
            ILogger logger,
            Configuration config,
            IExamineManager examineMgr,
            IndexRebuilder rebuilder)
        {
            _logger = logger;
            _config = config;
            _examineMgr = examineMgr;
            _rebuilder = rebuilder;
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

            _logger.Warn<ExamineService>(
                "GetNodeFromExamine Failed. Node with Id {Id} not found.", Id);
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

            _logger.Warn<ExamineService>(
                "GetNodeFromExamine Failed. Node with Key {Key} not found.", Key);
            return null;
        }

        /// <inheritDoc/>
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
                            luceneQuery.Append('+');
                        }

                        luceneQuery.Append('(');
                        luceneQuery.Append(term);
                        luceneQuery.Append(')');

                        i++;
                    }
                }

                ISearchResults searchResults;

                // If no filters were selected search for nodes with ID 0 that will return no results.
                if (luceneQuery.Length > 0)
                {
                    var rawQuery = searcher.CreateQuery("content").NativeQuery(luceneQuery.ToString());
                    searchResults = rawQuery.Execute(int.MaxValue);
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
                _logger.Error<ExamineService>("Unable to get Searcher {ExamineIndex}!", examineIndex);

                return null;
            }
        }

        public void Rebuild()
        {
            try
            {
                _logger.Info<ExamineService>("Trying to rebuild indexes if they are empty.");

                foreach (var index in _examineMgr.Indexes)
                {
                    var searcher = index.GetSearcher();

                    string category = "content";

                    if (index.Name.StartsWith("Member"))
                    {
                        continue;
                    }

                    var results = searcher.CreateQuery(category).All().Execute();

                    var count = results.Count();
                    var canRebuild = _rebuilder.CanRebuild(index);

                    _logger.Info<ExamineService>("Examine Index '" + index.Name + "' Status:  Count:" + count + " CanRebuild:" + canRebuild);

                    if (count <= 1 && canRebuild)
                    {
                        _logger.Info<ExamineService>("Index rebuild on startup.  Index:" + index.Name + " Category:" + category);
                        _rebuilder.RebuildIndex(index.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error<ExamineService>(ex, "Could not rebuild indexes on startup");
            }

        }
    }
}

using Examine;
using Umbraco.Examine;
using System;
using System.Linq;

namespace Ekom.Services
{
    /// <summary>
    /// Unused as service currently
    /// </summary>
    class ExamineService
    {
        public static DateTime ConvertToDatetime(string value)
        {
            return DateTime.ParseExact(value, "yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        }

        Configuration _config;
        IExamineManager _examineMgr;
        public ExamineService(Configuration config, IExamineManager examineMgr)
        {
            _config = config;
            _examineMgr = examineMgr;
        }

        public ISearchResult GetExamineNode(int Id)
        {
            if (_examineMgr.TryGetSearcher(_config.ExamineSearcher, out ISearcher searcher))
            {
                var results = searcher.CreateQuery("content")
                    .Id(Id)
                    .Execute()
                    ;

                return results.FirstOrDefault();
            }

            return null;
        }
    }
}

using Examine;
using Examine.SearchCriteria;
using System;
using System.Linq;

namespace Ekom.Services
{
    class ExamineService
    {
        public static DateTime ConvertToDatetime(string value)
        {
            return DateTime.ParseExact(value, "yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        }

        Configuration _config;
        ExamineManager _examineMgr;
        public ExamineService(Configuration config, ExamineManager examineMgr)
        {
            _config = config;
            _examineMgr = examineMgr;
        }

        public SearchResult GetExamineNode(int Id)
        {
            var searcher = _examineMgr.SearchProviderCollection[_config.ExamineSearcher];

            if (searcher != null)
            {
                ISearchCriteria searchCriteria = searcher.CreateSearchCriteria();
                var query = searchCriteria.Id(Id);
                var results = searcher.Search(query.Compile());

                return results.First();
            }

            return null;
        }
    }
}

using Examine;
using Examine.SearchCriteria;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace uWebshop.Helpers
{
    internal class ExamineHelper
    {
        internal static DateTime ConvertToDatetime(string value)
        {
            return DateTime.ParseExact(value, "yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static SearchResult GetExamindeNode(int Id)
        {
            var searcher = ExamineManager.Instance.SearchProviderCollection[Configuration.ExamineSearcher];

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

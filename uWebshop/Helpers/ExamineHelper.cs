using Examine;
using Examine.SearchCriteria;
using Microsoft.Practices.Unity;
using System;
using System.Linq;
using uWebshop.App_Start;

namespace uWebshop.Helpers
{
    internal class ExamineHelper
    {
        private static Configuration _config
        {
            get
            {
                return UnityConfig.GetConfiguredContainer().Resolve<Configuration>();
            }
        }

        internal static DateTime ConvertToDatetime(string value)
        {
            return DateTime.ParseExact(value, "yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static SearchResult GetExamindeNode(int Id)
        {
            var searcher = ExamineManager.Instance.SearchProviderCollection[_config.ExamineSearcher];

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

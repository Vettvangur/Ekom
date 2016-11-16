using Examine;
using Examine.SearchCriteria;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Logging;

namespace uWebshop.Services
{
    public static class ExamineService
    {
        private static readonly ILog Log =
        LogManager.GetLogger(
            MethodBase.GetCurrentMethod().DeclaringType
        );

        public static string GetProperty(SearchResult item, string field, string storeAlias)
        {

            var fieldExist = item.Fields.Any(x => x.Key == field + "_" + storeAlias);

            if (fieldExist)
            {
                return item.Fields[field + "_" + storeAlias];
            }
            else
            {
                return item.Fields.Any(x => x.Key == field) ? item.Fields[field] : "";
            }

        }

        public static SearchResult GetNodeFromExamine(int id)
        {

            var searcher = ExamineManager.Instance.SearchProviderCollection["ExternalSearcher"];

            if (searcher != null)
            {

                ISearchCriteria searchCriteria = searcher.CreateSearchCriteria();
                var query = searchCriteria.Id(id);
                var result = searcher.Search(query.Compile());

                if (result.Any())
                {
                    return result.FirstOrDefault();
                }
                else
                {
                    Log.Error("GetNodeFromExamine Failed. Node with Id " + id + " not found.");
                }

            }

            return null;
        }

        public static IEnumerable<SearchResult> GetAllCatalogItemsFromPath(string path)
        {
            var list = new List<SearchResult>();

            var pathArray = path.Split(',');

            var Ids = pathArray.Skip(3);

            foreach (var id in Ids)
            {
                var examineItem = ExamineService.GetNodeFromExamine(Convert.ToInt32(id));

                list.Add(examineItem);
            }

            return list;
        }
    }
}

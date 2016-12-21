using Examine;
using Examine.SearchCriteria;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Logging;
using uWebshop.Models;

namespace uWebshop.Helpers
{
    public static class ExamineHelper
    {
        public static IEnumerable<SearchResult> GetAllCatalogItemsFromPath(string path)
        {
            var list = new List<SearchResult>();

            var pathArray = path.Split(',');

            var Ids = pathArray.Skip(3);

            foreach (var id in Ids)
            {
                var examineItem = GetNodeFromExamine(int.Parse(id));

                list.Add(examineItem);
            }

            return list;
        }

        public static SearchResult GetNodeFromExamine(int id)
        {
            var searcher = ExamineManager.Instance.SearchProviderCollection["ExternalSearcher"];

            if (searcher != null)
            {
                ISearchCriteria searchCriteria = searcher.CreateSearchCriteria();

                var query  = searchCriteria.Id(id);
                var result = searcher.Search(query.Compile());

                if (result.Any())
                {
                    return result.FirstOrDefault();
                }
                else
                {
                    LogHelper.Info(MethodBase.GetCurrentMethod().DeclaringType, 
                        "GetNodeFromExamine Failed. Node with Id " + id + " not found.");
                }
            }

            return null;
        }

        /// <summary>
        /// Determine if an examine item is unpublished <para />
        /// Traverses up content tree, checking all parents
        /// </summary>
        /// <returns>True if disabled</returns>
        public static bool IsItemUnpublished(this SearchResult searchResult)
        {
            string path = searchResult.Fields["path"];

            foreach (var item in GetAllCatalogItemsFromPath(path))
            {
                // Unpublished items can't be found in the examine index
                if (item == null)
                { 
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determine if an examine item is disabled/unpublished <para />
        /// Traverses up content tree, checking all parents
        /// </summary>
        /// <returns>True if disabled</returns>
        public static bool IsItemDisabled(this SearchResult searchResult, Store store, string path = "")
        {
            path = string.IsNullOrEmpty(path) ? searchResult.Fields["path"] : path;

            foreach (var item in GetAllCatalogItemsFromPath(path))
            {
                if (item != null)
                {
                    var disableField = GetProperty(item, "disable", store.Alias);

                    if (!string.IsNullOrEmpty(disableField))
                    {
                        if (disableField == "1" || disableField.ToLower() == "true")
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Retrieve a store specific property <para/>
        /// alias name = field + "_" + storeAlias <para/>
        /// f.x. disabled_IS
        /// </summary>
        /// <param name="field">Umbraco Alias</param>
        /// <returns>Property Value</returns>
        public static string GetProperty(SearchResult item, string field, string storeAlias)
        {
            var fieldExist = item.Fields.Any(x => x.Key == field + "_" + storeAlias);

            if (fieldExist)
            {
                // temp fix for 66north  2 disable fields. 'disable' && 'disable_IS'
                var value = item.Fields[field + "_" + storeAlias];

                if (( string.IsNullOrEmpty(value) || value == "0" ) && storeAlias.ToLower() == "is")
                {
                    value = item.Fields.Any(x => x.Key == field) ? item.Fields[field] : "";
                }

                return value;
            }
            else
            {
                return item.Fields.Any(x => x.Key == field) ? item.Fields[field] : "";
            }
        }
    }
}

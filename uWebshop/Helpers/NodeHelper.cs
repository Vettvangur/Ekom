using Examine;
using Examine.SearchCriteria;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Web;
using uWebshop.Models;
using uWebshop.Utilities;

namespace uWebshop.Helpers
{
    public static class NodeHelper
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

        /// <summary>
        /// Gets a few close parents, skipping two levels of uWebshop hierarchy
        /// </summary>
        /// <param name="path"></param>
        /// <param name="umbHelper"></param>
        /// <returns></returns>
        public static IEnumerable<SearchResult> GetParents(string path)
        {
            var list = new List<SearchResult>();

            var pathArray = path.Split(',');

            var Ids = pathArray.Take(pathArray.Length - 1).Skip(3);

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
        /// Determine if an <see cref="IContent"/> item is unpublished <para />
        /// Traverses up content tree, checking all parents
        /// </summary>
        /// <returns>True if disabled</returns>
        public static bool IsItemUnpublished(this IContent node)
        {
            string path = node.Path;

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
        /// Traverses up content tree, checking all parents, looks for Umbraco properties matching stores country code
        /// </summary>
        /// <param name="store">Used to look for umbraco properties matching stores country code </param>
        /// <returns>True if disabled</returns>
        public static bool IsItemDisabled(this SearchResult searchResult, 
                                          Store store, 
                                          string path = "", 
                                          IEnumerable<SearchResult> allCatalogItems = null)
        {
            path = string.IsNullOrEmpty(path) ? searchResult.Fields["path"] : path;

            allCatalogItems = allCatalogItems == null ? GetAllCatalogItemsFromPath(path) : 
                                                        allCatalogItems;

            foreach (var item in allCatalogItems)
            {
                if (item != null)
                {
                    var disableField = GetStoreProperty(item, "disable", store.Alias);

                    if (!string.IsNullOrEmpty(disableField))
                    {
                        return disableField.ConvertToBool();
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
        /// Determine if an <see cref="IContent"/> item is disabled/unpublished <para />
        /// Traverses up content tree, checking all parents, looks for Umbraco properties matching stores country code
        /// </summary>
        /// <param name="store">Used to look for umbraco properties matching stores country code </param>
        /// <returns>True if disabled</returns>
        public static bool IsItemDisabled(this IContent node,
                                          Store store,
                                          string path = "",
                                          IEnumerable<SearchResult> allCatalogItems = null)
        {
            path = string.IsNullOrEmpty(path) ? node.Path : path;

            allCatalogItems = allCatalogItems == null ? GetParents(path) : allCatalogItems;

            foreach (var item in allCatalogItems)
            {
                if (item != null)
                {
                    var disableField = GetStoreProperty(item, "disable", store.Alias);

                    if (!string.IsNullOrEmpty(disableField))
                    {
                        if (disableField == "1" || disableField.Equals("true", StringComparison.InvariantCultureIgnoreCase))
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
        public static string GetStoreProperty(this SearchResult item, string field, string storeAlias)
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

        /// <summary>
        /// Retrieve a store specific property <para/>
        /// alias name = field + "_" + storeAlias <para/>
        /// f.x. disabled_IS
        /// </summary>
        /// <param name="storeAlias">Umbraco Alias</param>
        /// <returns>Property Value</returns>
        public static string GetStoreProperty(this Dictionary<string, string> items, string property, string storeAlias)
        {
            var fieldExist = items.ContainsKey(property + "_" + storeAlias);

            if (fieldExist)
            {
                return items[property + "_" + storeAlias];
            }
            // temp fix for 66north  2 disable fields. 'disable' && 'disable_IS'
            else
            {
                return items.ContainsKey(property) ? items[property] : "";
            }
        }

        /// <summary>
        /// Retrieve a store specific property <para/>
        /// alias name = field + "_" + storeAlias <para/>
        /// f.x. disabled_IS
        /// </summary>
        /// <param name="field">Umbraco Alias</param>
        /// <returns>Property Value</returns>
        public static string GetStoreProperty(this IContent item, string field, string storeAlias)
        {
            if (item.HasProperty(field + "_" + storeAlias))
            {
                var fieldValue = item.GetValue<string>(field + "_" + storeAlias);

                // temp fix for 66north  2 disable fields. 'disable' && 'disable_IS'
                if (storeAlias.ToLower() == "is" && (string.IsNullOrEmpty(fieldValue) || 
                                                     fieldValue == "0" ))
                {
                    fieldValue = item.GetValue<string>(field);
                }

                return fieldValue;
            }
            else
            {
                return item.HasProperty(field) ? item.GetValue<string>(field) : "";
            }
        }

        /// <summary>
        /// Retrieve a store specific property <para/>
        /// alias name = field + "_" + storeAlias <para/>
        /// f.x. disabled_IS
        /// </summary>
        /// <param name="field">Umbraco Alias</param>
        /// <returns>Property Value</returns>
        public static string GetStoreProperty(this IPublishedContent item, string field, string storeAlias)
        {
            if (item.HasProperty(field + "_" + storeAlias))
            {
                var fieldValue = item.GetPropertyValue<string>(field + "_" + storeAlias);

                // temp fix for 66north  2 disable fields. 'disable' && 'disable_IS'
                if (storeAlias.ToLower() == "is" && (string.IsNullOrEmpty(fieldValue) ||
                                                     fieldValue == "0" ))
                {
                    fieldValue = item.GetPropertyValue<string>(field);
                }

                return fieldValue;
            }
            else
            {
                return item.HasProperty(field) ? item.GetPropertyValue<string>(field) : "";
            }
        }
    }
}

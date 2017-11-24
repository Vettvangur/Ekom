using Ekom.Models;
using Ekom.Utilities;
using Examine;
using Examine.SearchCriteria;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Web;
using Ekom.Utilities;
using Umbraco.Core;

namespace Ekom.Helpers
{
    static class NodeHelper
    {
        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
        );

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
        /// Gets a few close parents, skipping two levels of Ekom hierarchy
        /// </summary>
        /// <param name="path"></param>
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

                var query = searchCriteria.Id(id);
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
        /// <param name="searchResult"></param>
        /// <param name="store">Used to look for umbraco properties matching stores country code </param>
        /// <param name="path"></param>
        /// <param name="allCatalogItems"></param>
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
        /// <param name="node"></param>
        /// <param name="store">Used to look for umbraco properties matching stores country code </param>
        /// <param name="path"></param>
        /// <param name="allCatalogItems"></param>
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
                        if (disableField.IsBoolean())
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
        /// <param name="item"></param>
        /// <param name="field">Umbraco Alias</param>
        /// <param name="storeAlias"></param>
        /// <returns>Property Value</returns>
        public static string GetStoreProperty(this SearchResult item, string field, string storeAlias)
        {
            try
            {
                var fieldExist = item.Fields.Any(x => x.Key == field);

                if (fieldExist)
                {
                    
                    var value = item.Fields[field];

                    return value.GetVortoValue(storeAlias);
                }

                return string.Empty;

            } catch(Exception ex)
            {
                var json = new JavaScriptSerializer().Serialize(item);
                Log.Error("Failed to get StoreProperty. Item : " + json + " field: " + field + " store: " + storeAlias);
                throw ex;
            }

        }

        /// <summary>
        /// Retrieve a store specific property <para/>
        /// alias name = field + "_" + storeAlias <para/>
        /// f.x. disabled_IS
        /// </summary>
        /// <param name="items"></param>
        /// <param name="property"></param>
        /// <param name="storeAlias">Umbraco Alias</param>
        /// <returns>Property Value</returns>
        public static string GetStoreProperty(this Dictionary<string, string> items, string property, string storeAlias)
        {
            var fieldExist = items.ContainsKey(property);

            if (fieldExist)
            {
                return items[property].GetVortoValue(storeAlias);
            }

            return string.Empty;
        }

        /// <summary>
        /// Retrieve a store specific property <para/>
        /// alias name = field + "_" + storeAlias <para/>
        /// f.x. disabled_IS
        /// </summary>
        /// <param name="item"></param>
        /// <param name="field">Umbraco Alias</param>
        /// <param name="storeAlias"></param>
        /// <returns>Property Value</returns>
        public static string GetStoreProperty(this IContent item, string field, string storeAlias)
        {
            if (item.HasProperty(field))
            {
                var fieldValue = item.GetValue<string>(field);

                return fieldValue.GetVortoValue(storeAlias);
            }

            return string.Empty;
        }

        /// <summary>
        /// Retrieve a store specific property <para/>
        /// alias name = field + "_" + storeAlias <para/>
        /// f.x. disabled_IS
        /// </summary>
        /// <param name="item"></param>
        /// <param name="field">Umbraco Alias</param>
        /// <param name="storeAlias"></param>
        /// <returns>Property Value</returns>
        public static string GetStoreProperty(this IPublishedContent item, string field, string storeAlias)
        {

            if (item.HasProperty(field))
            {
                var fieldValue = item.GetPropertyValue<string>(field);

                return fieldValue.GetVortoValue(storeAlias);
            }

            return string.Empty;
        }


        /// <summary>
        /// Get Ipublished media node
        /// </summary>
        /// <param name="id"></param>
        /// <returns>Property Value</returns>
        public static IPublishedContent GetMediaNode(string id)
        {

            if (!string.IsNullOrEmpty(id))
            {

                if (int.TryParse(id, out int mediaId))
                {
                    var umbracoHelper = new UmbracoHelper(UmbracoContext.Current);

                    var node = umbracoHelper.TypedMedia(mediaId);

                    if (node != null)
                    {
                        return node;
                    }
                }

            }

            return null;
        }

        /// <summary>
        /// Get Ipublished content node by Udi
        /// </summary>
        /// <param name="udi"></param>
        /// <returns>Property Value</returns>
        public static IPublishedContent GetNodeByUdi(string udi)
        {
            if (!string.IsNullOrEmpty(udi))
            {

                if (Udi.TryParse(udi, out Udi id))
                {
                    var umbracoHelper = new UmbracoHelper(UmbracoContext.Current);

                    var node = umbracoHelper.TypedContent(id);

                    if (node != null)
                    {
                        return node;
                    }
                }

            }

            return null;
        }


    }
}

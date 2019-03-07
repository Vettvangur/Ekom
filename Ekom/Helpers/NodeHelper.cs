using Ekom.Interfaces;
using Ekom.Models.Abstractions;
using Ekom.Utilities;
using Examine;
using Examine.SearchCriteria;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Web;

namespace Ekom.Helpers
{
    public static class NodeHelper
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
                if (examineItem != null)
                {
                    list.Add(examineItem);
                }
                
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

        /// <summary>
        /// Recursively gets first <see cref="SearchResult"/> item with matching doc type, null otherwise
        /// </summary>
        public static SearchResult GetFirstParentWithDocType(SearchResult item, string docTypeAlias)
        {
            if (item == null) return item;

            else if (item.Fields["nodeTypeAlias"] == docTypeAlias)
            {
                return item;
            }
            else
            {
                var parentId = Convert.ToInt32(item.Fields["parentID"]);
                var parent = GetNodeFromExamine(parentId);
                return GetFirstParentWithDocType(parent, docTypeAlias);
            }
        }
        /// <summary>
        /// Recursively gets first <see cref="IContent"/> item with matching doc type, null otherwise
        /// </summary>
        public static IContent GetFirstParentWithDocType(IContent node, string docTypeAlias)
        {
            if (node == null) return node;

            else if (node.ContentType.Alias == docTypeAlias)
            {
                return node;
            }
            else
            {

                return GetFirstParentWithDocType(node.Parent(), docTypeAlias);
            }
        }

        public static SearchResult GetNodeFromExamine(int id)
        {
            var examineMgr = Configuration.container.GetInstance<ExamineManagerBase>();
            var searcher = examineMgr.SearchProviderCollection["ExternalSearcher"];

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
                    Log.Info("GetNodeFromExamine Failed. Node with Id " + id + " not found.");
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
        public static bool IsItemDisabled(
            this SearchResult searchResult,
            IStore store,
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
        public static bool IsItemDisabled(
            this IContent node,
            IStore store,
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

            }
            catch (Exception ex)
            {
                var json = JsonConvert.SerializeObject(item);
                Log.Error("Failed to get StoreProperty. Item : " + json + " field: " + field + " store: " + storeAlias, ex);
                throw;
            }
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
        /// Get <see cref="IPublishedContent"/> media node
        /// </summary>
        /// <param name="id"></param>
        /// <returns>Property Value</returns>
        public static IPublishedContent GetMediaNode(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                if (int.TryParse(id, out int mediaId))
                {
                    var umbracoHelper = Configuration.container.GetInstance<UmbracoHelper>();

                    var node = umbracoHelper.TypedMedia(mediaId);

                    if (node != null)
                    {
                        return node;
                    }
                }
                else if (Guid.TryParse(id, out Guid mediaGuid))
                {
                    var umbracoHelper = Configuration.container.GetInstance<UmbracoHelper>();

                    var node = umbracoHelper.TypedMedia(mediaGuid);

                    if (node != null)
                    {
                        return node;
                    }
                }

                return GetMediaByUdi(id);
            }

            return null;
        }

        /// <summary>
        /// Get <see cref="IPublishedContent"/> media node
        /// </summary>
        /// <param name="id"></param>
        /// <returns>Property Value</returns>
        public static IEnumerable<IPublishedContent> GetMediaNodesByGuid(Guid[] ids)
        {
            var list = new List<IPublishedContent>();

            if (ids.Any())
            {
                foreach (var id in ids)
                {
                    var node = GetMediaNode(id.ToString());

                    if (node != null)
                    {
                        list.Add(node);
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Get <see cref="IPublishedContent"/> node by Udi
        /// </summary>
        /// <param name="udi"></param>
        /// <returns>Property Value</returns>
        public static IPublishedContent GetNodeByUdi(string udi)
        {
            if (!string.IsNullOrEmpty(udi))
            {

                if (Udi.TryParse(udi, out Udi id))
                {
                    var umbracoHelper = Configuration.container.GetInstance<UmbracoHelper>();

                    var node = umbracoHelper.TypedContent(id);

                    if (node != null)
                    {
                        return node;
                    }
                }

            }

            return null;
        }
        /// <summary>
        /// Get <see cref="IPublishedContent"/> node by Udi
        /// </summary>
        /// <param name="udi"></param>
        /// <returns>Property Value</returns>
        public static IPublishedContent GetMediaByUdi(string udi)
        {
            if (!string.IsNullOrEmpty(udi))
            {

                if (Udi.TryParse(udi, out Udi id))
                {
                    var umbracoHelper = Configuration.container.GetInstance<UmbracoHelper>();

                    var node = umbracoHelper.TypedMedia(id);

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

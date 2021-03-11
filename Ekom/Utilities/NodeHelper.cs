using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using Examine;
using Newtonsoft.Json;
using Our.Umbraco.Vorto.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;

namespace Ekom.Utilities
{
    public static class NodeHelper
    {
        public static IEnumerable<ISearchResult> GetAllCatalogItemsFromPath(string path)
        {
            var pathArray = path.Split(',');

            // Skip Root, Ekom container, Catalog container
            var Ids = pathArray.Skip(3);

            return GetAllCatalogItemsFromPath(Ids);
        }
        public static IEnumerable<ISearchResult> GetAllCatalogItemsFromPath(IEnumerable<string> ids)
        {
            var list = new List<ISearchResult>();

            foreach (var id in ids)
            {
                var examineItem = ExamineService.Instance.GetExamineNode(int.Parse(id));
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
        public static IEnumerable<ISearchResult> GetParents(string path)
        {
            var list = new List<ISearchResult>();

            var pathArray = path.Split(',');

            var Ids = pathArray.Take(pathArray.Length - 1).Skip(3);

            foreach (var id in Ids)
            {
                var examineItem = ExamineService.Instance.GetExamineNode(int.Parse(id));

                list.Add(examineItem);
            }

            return list;
        }

        /// <summary>
        /// Recursively gets first <see cref="ISearchResult"/> item with matching doc type, null otherwise
        /// </summary>
        public static ISearchResult GetFirstParentWithDocType(ISearchResult item, string docTypeAlias)
        {
            if (item == null) return item;

            else if (item.Values["__NodeTypeAlias"] == docTypeAlias)
            {
                return item;
            }
            else
            {
                var parentId = Convert.ToInt32(item.Values["parentID"]);
                var parent = ExamineService.Instance.GetExamineNode(parentId);
                return GetFirstParentWithDocType(parent, docTypeAlias);
            }
        }
        /// <summary>
        /// Recursively gets first <see cref="IPublishedContent"/> item with matching doc type, null otherwise
        /// </summary>
        public static IPublishedContent GetFirstParentWithDocType(IPublishedContent node, string docTypeAlias)
        {
            if (node == null) return node;

            else if (node.ContentType.Alias == docTypeAlias)
            {
                return node;
            }
            else
            {
                return GetFirstParentWithDocType(node.Parent, docTypeAlias);
            }
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
                    var umbracoHelper = Current.Factory.GetInstance<UmbracoHelper>();

                    var node = umbracoHelper.Media(mediaId);

                    if (node != null)
                    {
                        return node;
                    }
                }
                else if (Guid.TryParse(id, out Guid mediaGuid))
                {
                    var umbracoHelper = Current.Factory.GetInstance<UmbracoHelper>();

                    var node = umbracoHelper.Media(mediaGuid);

 
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
        /// <param name="ids"></param>
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
                    var umbracoHelper = Current.Factory.GetInstance<UmbracoHelper>();

                    var node = umbracoHelper.Content(id);

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
                    var umbracoHelper = Current.Factory.GetInstance<UmbracoHelper>();

                    var node = umbracoHelper.Media(id);

                    if (node != null)
                    {
                        return node;
                    }
                }
            }

            return null;
        }

        #region Extension Methods

        /// <summary>
        /// Determine if an examine item is unpublished <para />
        /// Traverses up content tree, checking all parents
        /// </summary>
        /// <returns>True if disabled</returns>
        public static bool IsItemUnpublished(this ISearchResult searchResult)
        {
            string path = searchResult.Values["__Path"];
            var catalogItems = GetAllCatalogItemsFromPath(path);
            var pathArray = path.Split(',');

            var Ids = pathArray.Skip(3);

            // Unpublished items can't be found in the external examine index
            // missing items are not returned on get
            return Ids.Count() > catalogItems.Count();
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
            this ISearchResult searchResult,
            IStore store,
            string path = "",
            IEnumerable<ISearchResult> allCatalogItems = null)
        {
            var selfDisableField = GetStoreProperty(searchResult, "disable", store.Alias);

            if (!string.IsNullOrEmpty(selfDisableField))
            {
                if (selfDisableField.ConvertToBool())
                {
                    return true;
                }
            }

            path = string.IsNullOrEmpty(path) ? searchResult.Values["__Path"] : path;

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
            IEnumerable<ISearchResult> allCatalogItems = null)
        {

            var selfDisableField = GetStoreProperty(node, "disable", store.Alias);

            if (!string.IsNullOrEmpty(selfDisableField))
            {
                if (selfDisableField.IsBoolean())
                {
                    return true;
                }
            }

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
        /// </summary>
        /// <param name="item"></param>
        /// <param name="field">Umbraco Alias</param>
        /// <param name="storeAlias"></param>
        /// <returns>Property Value</returns>
        public static string GetStoreProperty(this ISearchResult item, string field, string storeAlias)
        {
            try
            {
                var fieldExist = item.Values.Any(x => x.Key == field);

                if (fieldExist)
                {

                    var value = item.Values[field];

                    return value.GetVortoValue(storeAlias);
                }

                return string.Empty;

            }
            catch (Exception ex)
            {
                var json = JsonConvert.SerializeObject(item);
                Current.Logger.Error(
                    typeof(NodeHelper),
                    ex,
                    $"Failed to get StoreProperty. Item : {json} field: {field} store: {storeAlias}"
                );
                throw;
            }
        }

        /// <summary>
        /// Retrieve a store specific property <para/>
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
        /// Retrieve a price store specific property <para/>
        /// </summary>
        /// <param name="item"></param>
        /// <param name="storeAlias"></param>
        /// <param name="currency"></param>
        /// <returns>Property Value</returns>
        public static decimal GetPriceStoreProperty(this IContent item, string storeAlias, string currency = null)
        {
            if (item.HasProperty("price"))
            {
                var fieldValue = item.GetValue<string>("price");

                var jsonCurrencyValue = fieldValue.GetVortoValue(storeAlias);

                var currencyValues = jsonCurrencyValue.GetCurrencyValues();

                var value = string.IsNullOrEmpty(currency) ? currencyValues.FirstOrDefault() : currencyValues.FirstOrDefault(x => x.Currency == currency);

                return value != null ? value.Value : 0;
            }

            return 0;
        }

        /// <summary>
        /// Set a price store specific property <para/>
        /// </summary>
        /// <param name="item"></param>
        /// <param name="storeAlias"></param>
        /// <param name="currency"></param>
        /// <param name="price"></param>
        /// <returns>Property Value</returns>
        public static void SetPriceStoreValue(this IContent item, string storeAlias, string currency, decimal price)
        {
            if (item.HasProperty("price"))
            {
           
                var fieldValue = item.GetValue<string>("price");

                var currencyPrices = new List<CurrencyPrice>();

                if (!string.IsNullOrEmpty(fieldValue))
                {
                    try
                    {
                        var jsonCurrencyValue = fieldValue.GetVortoValue(storeAlias);

                        currencyPrices = jsonCurrencyValue.GetCurrencyPrices();

                    } catch
                    {

                    }
                }

                if (currencyPrices.Any(x => x.Currency == currency))
                {
                    currencyPrices.FirstOrDefault(x => x.Currency == currency).Price = price;

                } else
                {
                    currencyPrices.Add(new CurrencyPrice(price, currency));
                }

                item.SetVortoValue("price", storeAlias, currencyPrices);
            }
        }

        /// <summary>
        /// Set a store specific property <para/>
        /// </summary>
        /// <param name="item"></param>
        /// <param name="field">Umbraco Alias</param>
        /// <param name="storeAlias"></param>
        /// <param name="value"></param>
        /// <returns>Property Value</returns>
        public static void SetStorePropertyValue(this IContent item, string field, string storeAlias, object value)
        {
            if (item.HasProperty(field))
            {
                item.SetVortoValue(field,storeAlias,value);
            }
        }

        public static string Key(this ISearchResult searchResult) => searchResult.Values["__Key"];

        #endregion
    }
}

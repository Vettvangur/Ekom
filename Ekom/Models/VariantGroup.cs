using Ekom.Cache;
using Ekom.Helpers;
using Ekom.Utilities;
using Ekom.Interfaces;
using Examine;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Umbraco.Core.Models;
using Ekom.API;

namespace Ekom.Models
{
    /// <summary>
    /// A group of variants sharing common properties and a group key
    /// </summary>
    public class VariantGroup : NodeEntity, INodeEntity
    {
        Store _store { get; set; }

        /// <summary>
        /// Get the Product Key
        /// </summary>
        public Guid ProductKey {
            get
            {
                var parentId = Convert.ToInt32(Properties.GetPropertyValue("parentID"));
                var product = Catalog.Current.GetProduct(_store.Alias, parentId);

                if (product == null)
                {
                    throw new Exception("Product not found for Variant group. Id:" + Id);
                }

                return product.Key;
            }
        }

        /// <summary>
        /// Get Images
        /// </summary>
        public IEnumerable<IPublishedContent> Images
        {
            get
            {
                var _images = Properties.GetStoreProperty("images", _store.Alias);

                var imageNodes = _images.GetMediaNodes();

                return imageNodes;
            }
        }

        /// <summary>
        /// Get all variants in this group
        /// </summary>
        [JsonIgnore]
        public IEnumerable<Variant> Variants
        {
            get
            {
                return API.Catalog.Current.GetVariantsByGroup(_store.Alias, Key);
            }
        }

        /// <summary>
        /// Used by Ekom extensions
        /// </summary>
        /// <param name="store"></param>
        /// <param name="cache"></param>
        public VariantGroup(Store store, IPerStoreCache<Variant> cache)
        {

            _store = store;
        }

        /// <summary>
        /// Construct Variant Group from Examine item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        public VariantGroup(SearchResult item, Store store) : base(item)
        {
            _store = store;
        }

        /// <summary>
        /// Construct Variant Group from umbraco publish event
        /// </summary>
        /// <param name="node"></param>
        /// <param name="store"></param>
        public VariantGroup(IContent node, Store store) : base(node)
        {
            _store = store;
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}

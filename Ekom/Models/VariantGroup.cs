using Ekom.API;
using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Utilities;
using Examine;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;
using Umbraco.Core.Models;

namespace Ekom.Models
{
    /// <summary>
    /// A group of variants sharing common properties and a group key
    /// </summary>
    class VariantGroup : NodeEntity, INodeEntity, IVariantGroup
    {
        Store _store { get; set; }

        public IProduct Product
        {
            get
            {
                var product = Catalog.Current.GetProduct(_store.Alias, ProductId);

                if (product == null)
                {
                    throw new Exception("Variant ProductKey could not be created. Product not found. Key: " + ProductId);
                }

                return product;
            }
        }


        /// <summary>
        /// Get the Product Key
        /// </summary>
        public Guid ProductKey
        {
            get
            {
                return Product.Key;
            }
        }

        public int ProductId
        {
            get
            {
                var parentId = Convert.ToInt32(Properties.GetPropertyValue("parentID"));

                return parentId;
            }
        }

        /// <summary>
        /// Get Images
        /// </summary>
        public IEnumerable<IPublishedContent> Images
        {
            get
            {
                var _images = Properties.GetPropertyValue("images", _store.Alias);

                var imageNodes = _images.GetMediaNodes();

                return imageNodes;
            }
        }

        /// <summary>
        /// Get all variants in this group
        /// </summary>
        [JsonIgnore]
        public IEnumerable<IVariant> Variants
        {
            get
            {
                return Catalog.Current.GetVariantsByGroup(_store.Alias, Key);
            }
        }

        /// <summary>
        /// Used by Ekom extensions
        /// </summary>
        /// <param name="store"></param>
        public VariantGroup(Store store)
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

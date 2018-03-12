using Ekom.API;
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
    public class VariantGroup : PerStoreNodeEntity, INodeEntity, IVariantGroup
    {
        /// <summary>
        /// Parent <see cref="IProduct"/> of Variant
        /// </summary>
        public IProduct Product
        {
            get
            {
                var product = Catalog.Instance.GetProduct(Store.Alias, ProductId);

                if (product == null)
                {
                    throw new Exception("Variant ProductKey could not be created. Product not found. Key: " + ProductId);
                }

                return product;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public int ProductId
        {
            get
            {
                var paths = Path.Split(',');

                int productId = Convert.ToInt32(paths[paths.Length - 3]);

                return productId;
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

        // Waiting for variants to be composed with their parent product
        ///// <summary>
        ///// Get the Product Key
        ///// </summary>
        //public Guid ProductKey => Product.Key;
        ///// <summary>
        ///// 
        ///// </summary>
        //public int ProductId => Product.Id;

        /// <summary>
        /// Get Images
        /// </summary>
        public IEnumerable<IPublishedContent> Images
        {
            get
            {
                var _images = Properties.GetPropertyValue("images", Store.Alias);

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
                return Catalog.Instance.GetVariantsByGroup(Store.Alias, Id);
            }
        }

        /// <summary>
        /// Used by Ekom extensions
        /// </summary>
        /// <param name="store"></param>
        public VariantGroup(IStore store) : base(store) { }

        /// <summary>
        /// Construct Variant Group from Examine item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        public VariantGroup(SearchResult item, IStore store) : base(item, store) { }

        /// <summary>
        /// Construct Variant Group from umbraco publish event
        /// </summary>
        /// <param name="node"></param>
        /// <param name="store"></param>
        public VariantGroup(IContent node, IStore store) : base(node, store) { }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}

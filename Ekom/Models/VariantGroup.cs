using Ekom.API;
using Ekom.Cache;
using Ekom.Exceptions;
using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Utilities;
using Examine;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using Umbraco.Core.Models;

namespace Ekom.Models
{
    /// <summary>
    /// Groups multiple <see cref="IVariant"/>'s together with a group key, 
    /// common properties and a shared parent <see cref="IProduct"/>
    /// </summary>
    public class VariantGroup : PerStoreNodeEntity, IVariantGroup
    {
        /// <summary>
        /// Parent <see cref="IProduct"/> of Variant
        /// </summary>
        public IProduct Product { get; internal set; }

        /// <summary>
        /// 
        /// </summary>
        public int ProductId => Product.Id;

        /// <summary>
        /// Get the Product Key
        /// </summary>
        public Guid ProductKey => Product.Key;

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
        /// Variant group Images
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
        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
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
        public VariantGroup(SearchResult item, IStore store) : base(item, store)
        {
            var parentProductExamine = NodeHelper.GetFirstParentWithDocType(item, "ekmProduct");
            var parentProduct = Catalog.Instance.GetProduct(parentProductExamine.Id);

            if (parentProduct == null)
            {
                throw new ProductNotFoundException("Unable to find parent product of variant group");
            }
            Product = parentProduct;
        }

        /// <summary>
        /// Construct Variant Group from umbraco publish event
        /// </summary>
        /// <param name="node"></param>
        /// <param name="store"></param>
        public VariantGroup(IContent node, IStore store) : base(node, store)
        {
            var parentProductExamine = NodeHelper.GetFirstParentWithDocType(node, "ekmProduct");
            var parentProduct = Catalog.Instance.GetProduct(parentProductExamine.Id);

            if (parentProduct == null)
            {
                throw new ProductNotFoundException("Unable to find parent product of variant group");
            }

            Product = parentProduct;
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}

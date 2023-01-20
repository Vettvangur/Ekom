using Ekom.API;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Ekom.Utilities;
#if NET461
using System.Web.Script.Serialization;
#endif

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
        public int ProductId => Product?.Id ?? 0;

        /// <summary>
        /// Get the Product Key
        /// </summary>
        public Guid ProductKey => Product?.Key ?? Guid.Empty;

        /// <summary>
        /// Get the Primary variant price, if no variants then fallback to product price
        /// </summary>
        public IPrice Price
        {
            get
            {
                var variants = Variants;

                if (variants.Any())
                {
                    return variants.FirstOrDefault().Price;
                }

                return Product.Price;
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

        // <summary>
        // Variant Group images
        // </summary>
        public virtual IEnumerable<Image> Images
        {
            get
            {
                var _images = Properties.GetPropertyValue(Configuration.Instance.CustomImage);

                var imageNodes = _images.GetImages();

                return imageNodes;
            }
        }

        /// <summary>
        /// Get all variants in this group
        /// </summary>
#if NET461
        [ScriptIgnore]
#endif
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
        /// Construct Variant Group from UmbracoContent
        /// </summary>
        /// <param name="node"></param>
        /// <param name="store"></param>
        public VariantGroup(UmbracoContent node, IStore store) : base(node, store)
        {
            var pathArray = Path.Split(',');
            var productId = pathArray[pathArray.Count() - 2];
            var parentProduct = Catalog.Instance.GetProduct(store.Alias, Convert.ToInt32(productId));
            Product = parentProduct;
        }
    }
}

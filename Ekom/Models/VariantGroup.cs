using Ekom.API;
using Ekom.Utilities;
using Newtonsoft.Json;
using System.Xml.Serialization;

namespace Ekom.Models
{
    /// <summary>
    /// Groups multiple <see cref="IVariant"/>'s together with a group key, 
    /// common properties and a shared parent <see cref="IProduct"/>
    /// </summary>
    public class VariantGroup : PerStoreNodeEntity, IVariantGroup
    {
        private readonly string storeAlias;
        /// <summary>
        /// Parent <see cref="IProduct"/> of Variant
        /// </summary>
        public IProduct Product { 
            get
            {
                var pathArray = Path.Split(',');
                var productId = pathArray[pathArray.Count() - 2];
                return Catalog.Instance.GetProduct(storeAlias, Convert.ToInt32(productId));
            }
        }

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
               return PrimaryVariant != null ? PrimaryVariant.Price : Product.Price;
            }
        }

        /// <summary>
        /// Get the availability of the variant group
        /// </summary>
        public virtual bool Available => Variants.Any(x => x.Available);

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
        /// Select the Primary variant.
        /// First Variant in the group that is available, if none are available, return the first variant.
        /// </summary>
        public virtual IVariant PrimaryVariant
        {
            get
            {
                var primaryVariant = Variants.FirstOrDefault(x => x.Available);

                if (primaryVariant == null)
                {
                    primaryVariant = Variants.FirstOrDefault();
                }

                return primaryVariant;
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
            storeAlias = store.Alias;
        }
    }
}

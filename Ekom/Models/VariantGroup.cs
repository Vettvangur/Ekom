using Ekom.API;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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
        [JsonIgnore]
        [XmlIgnore]
        public IProduct Product => Catalog.Instance.GetProduct(ParentKey, storeAlias);

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
                var _images = GetValue(Configuration.Instance.CustomImage);

                var imageNodes = _images.GetImages();

                return imageNodes;
            }
        }

        /// <summary>
        /// Get all variants in this group
        /// </summary>
        public IEnumerable<IVariant> Variants
        {
            get
            {
                var cacheKey = $"Variants_{Store.Alias}_{Id}";

                var httpContext = Configuration.Resolver.GetService<IHttpContextAccessor>()?.HttpContext;
                if (httpContext != null)
                {
                    if (httpContext.Items.TryGetValue(cacheKey, out var cachedVariants))
                    {
                        return (IEnumerable<IVariant>)cachedVariants;
                    }

                    var variants = Catalog.Instance.GetVariantsByGroup(Id, Store.Alias);
                    httpContext.Items[cacheKey] = variants;
                    return variants;
                }

                return Catalog.Instance.GetVariantsByGroup(Id, Store.Alias);
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

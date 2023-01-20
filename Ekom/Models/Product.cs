using Ekom.Cache;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Ekom.Utilities;
#if NETCOREAPP
using Microsoft.AspNetCore.Http;
#else
using System.Web;
#endif

namespace Ekom.Models
{
    /// <summary>
    /// An Ekom store product
    /// </summary>
    public class Product : PerStoreNodeEntity, IProduct
    {
        private IPerStoreCache<IVariant> __variantCache;
        private IPerStoreCache<IVariant> _variantCache =>
            __variantCache ?? (__variantCache = Configuration.Resolver.GetService<IPerStoreCache<IVariant>>());

        private IPerStoreCache<IVariantGroup> __variantGroupCache;
        private IPerStoreCache<IVariantGroup> _variantGroupCache =>
            __variantGroupCache ?? (__variantGroupCache = Configuration.Resolver.GetService<IPerStoreCache<IVariantGroup>>());

        public virtual IDiscount ProductDiscount(string price = null)
        {
            price = string.IsNullOrEmpty(price) ? Price.OriginalValue.ToString() : price;

            return Configuration.Resolver.GetService<ProductDiscountService>()
                    .GetProductDiscount(
                        Path,
                        Store.Alias,
                        price,
                        categories.Select(x => x.Id.ToString()).ToArray()
                    );
        }

        /// <summary>
        /// Product Stock Keeping Unit.
        /// </summary>
        public string SKU => Properties.GetPropertyValue("sku");

        /// <summary>
        /// 
        /// </summary>
        public virtual string Description => Properties.GetPropertyValue("description", Store.Alias);

        /// <summary>
        /// 
        /// </summary>
        public virtual string Summary => Properties.GetPropertyValue("summary", Store.Alias);

        /// <summary>
        /// Short spaceless descriptive title used to create URLs
        /// </summary>
        public string Slug => Properties.GetPropertyValue("slug", Store.Alias);

        /// <summary>
        /// Get the current product Stock
        /// </summary>
        public virtual int Stock => API.Stock.Instance.GetStock(Key);

        /// <summary>
        /// Get the backorder status
        /// </summary>
        public virtual bool Backorder
        {
            get
            {
                //TODO Store default setup!

                var backOrderValue = Properties.GetPropertyValue("enableBackorder", Store.Alias);

                return !string.IsNullOrEmpty(backOrderValue) && backOrderValue.IsBoolean();
            }
        }

        // <summary>
        // Product images
        // </summary>
        public virtual IEnumerable<Image> Images
        {

            get
            {
                var primaryVariantGroup = PrimaryVariantGroup;

                if (primaryVariantGroup != null)
                {
                    var imageNodes = primaryVariantGroup.Images;

                    if (!imageNodes.Any() && primaryVariantGroup.Variants.Any())
                    {
                        imageNodes = primaryVariantGroup.Variants.FirstOrDefault()?.Images;
                    }

                    if (imageNodes.Any())
                    {
                        return imageNodes;
                    }
                }
                
                var _images = Properties.GetPropertyValue(Configuration.Instance.CustomImage);

                return _images.GetImages();
            }
        }

        /// <summary>
        /// A product can have multiple variant groups, 
        /// therefore we allow to configure a default/primary variant group.
        /// If none is configured, we return the first possible item.
        /// </summary>
        public virtual IVariantGroup PrimaryVariantGroup
        {
            get
            {
                if (Properties.ContainsKey("primaryVariantGroup"))
                {
                    var primaryGroupValue = Properties.GetPropertyValue("primaryVariantGroup", Store.Alias);

                    if (!string.IsNullOrEmpty(primaryGroupValue) && VariantGroups.Any())
                    {
                        var node = Configuration.Resolver.GetService<INodeService>().NodeById(primaryGroupValue);

                        if (node != null && node.ContentTypeAlias == "ekmProductVariantGroup")
                        {
                            var variantGroup = __variantGroupCache.Cache[Store.Alias][node.Key];

                            return variantGroup;
                        }
                    }
                }

                return VariantGroups.FirstOrDefault();
            }
        }

        /// <summary>
        /// All categories this <see cref="Product"/> belongs to.
        /// Found by traversing up the examine tree and then matching examine items to cached <see cref="ICategory"/>'s
        /// </summary>
        /// <returns></returns>
        [JsonIgnore]
        [XmlIgnore]
        public virtual IEnumerable<ICategory> CategoryAncestors => categoryAncestors.AsReadOnly();
        internal List<ICategory> categoryAncestors = new List<ICategory>();

        /// <summary>
        /// All categories product belongs to, includes parent category.
        /// Does not include categories product is an indirect child of.
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        public virtual IEnumerable<ICategory> Categories => categories.AsReadOnly();
        internal List<ICategory> categories = new List<ICategory>();

        /// <summary>
        /// All ID's of categories product belongs to, includes parent category.
        /// Does not include categories product is an indirect child of.
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        public virtual IEnumerable<Guid> CategoriesIds
        {
            get
            {
                return Categories.Select(x => x.Key);
            }
        }

        /// <inheritdoc/>
        public virtual string Url => Configuration.Resolver.GetService<IUrlService>().GetNodeEntityUrl(this);

        /// <summary>
        /// All product urls, computed from stores and categories.
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        public virtual IEnumerable<string> Urls { get; internal set; }

        /// <summary>
        /// Get Price by current store currency
        /// </summary>
        public IPrice Price => GetPrice();

        private IPrice GetPrice()
        {

#if NETCOREAPP
            var httpCtx = Configuration.Resolver.GetService<IHttpContextAccessor>().HttpContext;
            var cookie = httpCtx?.Request.Cookies["EkomCurrency-" + Store.Alias];
#else
            var httpCtx = Configuration.Resolver.GetService<HttpContextBase>();
            var cookie = httpCtx?.Request.Cookies["EkomCurrency-" + Store.Alias].Value;
#endif

            if (cookie != null && !string.IsNullOrEmpty(cookie))
            {
                var price = Prices.FirstOrDefault(x => x.Currency.CurrencyValue == cookie);

                if (price != null)
                {
                    return price;
                }
            }

            return Prices.FirstOrDefault();
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual List<IPrice> Prices
        {
            get
            {
                var prices = Properties.GetPropertyValue("price", Store.Alias)
                    .GetPriceValues(
                        Store.Currencies,
                        Vat,
                        Store.VatIncludedInPrice,
                        Store.Currency,
                        Store.Alias,
                        Path,
                        Categories.Select(x => x.Id.ToString()).ToArray()
                        );

                return prices;
            }
        }

        public virtual decimal Vat
        {
            get
            {
                if (Properties.HasPropertyValue("vat", Store.Alias))
                {
                    var value = Properties.GetPropertyValue("vat", Store.Alias);

                    if (!string.IsNullOrEmpty(value) && decimal.TryParse(value, out decimal _val))
                    {
                        return _val / 100;
                    }
                }

                return Store.Vat;
            }
        }

        public virtual List<Metavalue> Metafields
        {
            get
            {
                if (Properties.HasPropertyValue("metafields"))
                {
                    var value = GetValue("metafields");

                    return Configuration.Resolver.GetService<IMetafieldService>().SerializeMetafields(value);
                }

                return new List<Metavalue>();
            } 
        }

        /// <summary>
        /// All child variant groups of this product
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        public virtual IEnumerable<IVariantGroup> VariantGroups
        {
            get
            {
                // Use ID Instead of Key, Key is much slower.
                return _variantGroupCache.Cache[Store.Alias]
                                        .Where(x => x.Value.ProductId == Id)
                                        .Select(x => x.Value);
            }
        }

        /// <summary>
        /// All variants belonging to product.
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        public virtual IEnumerable<IVariant> AllVariants
        {
            get
            {
                // Use ID Instead of Key, Key is much slower.
                return _variantCache.Cache[Store.Alias]
                    .Where(x => x.Value.ProductId == Id)
                    .Select(x => x.Value);
            }
        }

        /// <summary>
        /// Get Variant Count
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        public int AllVariantsCount
        {
            get
            {
                // AllVariants is slower with Select, this is done for performance
                return _variantCache.Cache[Store.Alias]
                    .Count(x => x.Value.ProductId == Id);
            }

        }

        /// <summary>
        /// Used by Ekom extensions, keep logic empty to allow full customisation of object construction.
        /// </summary>
        /// <param name="store"></param>
        internal protected Product(IStore store) : base(store) { }

        /// <summary>
        /// Construct Product
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        internal protected Product(UmbracoContent item, IStore store) : base(item, store)
        {
            PopulateCategoryAncestors(item);
            PopulateCategories();

            Urls = Configuration.Resolver.GetService<IUrlService>().BuildProductUrls(GetValue("slug"), Categories, store, item.Id);

            if (!Urls.Any() || string.IsNullOrEmpty(GetValue("title")))
            {
                throw new Exception("No url's or no title present in product. Id: " + item.Id + " Title: " + Title + " HasUrls: " + Urls.Any() + " HasCategories: " + Categories.Any());
            }
        }

        private void PopulateCategories()
        {
            int categoryId = ParentId;

            var categoryField = Properties.ContainsKey("categories") ?
                                Properties.GetPropertyValue("categories") : "";

            var primaryCategory = API.Catalog.Instance.GetCategory(Store.Alias, categoryId);

            if (primaryCategory != null)
            {
                categories.Add(primaryCategory);
            }

            if (!string.IsNullOrEmpty(categoryField))
            {
                var categoryIds = categoryField.Split(',');

                foreach (var catId in categoryIds)
                {
                    var categoryItem
                        = API.Catalog.Instance.GetCategory(Store.Alias, catId);

                    if (categoryItem != null && !categories.Contains(categoryItem))
                    {
                        categories.Add(categoryItem);
                    }
                }
            }
        }

        private void PopulateCategoryAncestors(UmbracoContent node)
        {
            var ancestors = Configuration.Resolver.GetService<INodeService>().NodeCatalogAncestors(node.Id.ToString());

            foreach (var item in ancestors.Where(x => x.IsDocumentType("ekmCategory")))
            {
                var c = API.Catalog.Instance.GetCategory(Store.Alias, item.Id);

                if (c != null)
                {
                    categoryAncestors.Add(c);
                }
            }
        }
    }
}

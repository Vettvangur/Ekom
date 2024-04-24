using Ekom.API;
using Ekom.Cache;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Xml.Serialization;
using Ekom.Models.Umbraco;
using Microsoft.AspNetCore.Localization;

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

            return Configuration.Resolver.GetService<ProductDiscountService>()?
                    .GetProductDiscount(
                        Path,
                        Store.Alias,
                        price,
                        categories.Select(x => x.Id.ToString())?.ToArray()
                    );
        }

        /// <summary>
        /// Product SKU
        /// </summary>
        public string SKU => GetValue("sku");

        /// <summary>
        /// 
        /// </summary>
        public virtual string Description => GetValue("description", Store.Alias);

        /// <summary>
        /// 
        /// </summary>
        public virtual string Summary => GetValue("summary", Store.Alias);

        /// <summary>
        /// Short spaceless descriptive title used to create URLs
        /// </summary>
        public string Slug => GetValue("slug", Store.Alias);

        /// <summary>
        /// Get the current product Stock
        /// </summary>
        public virtual int Stock => API.Stock.Instance.GetStock(Key);

        /// <summary>
        /// Get the availability of the product and the variants
        /// </summary>
        public virtual bool Available => Stock > 0 || Backorder || AllVariants.Any(x => x.Available);

        /// <summary>
        /// Get the backorder status
        /// </summary>
        public virtual bool Backorder
        {
            get
            {
                //TODO Store default setup!

                var backOrderValue = GetValue("enableBackorder", Store.Alias);

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
                    var imageNodes = primaryVariantGroup.Images.ToList();

                    if (!imageNodes.Any() && primaryVariantGroup.Variants.Any())
                    {
                        imageNodes = primaryVariantGroup.Variants.FirstOrDefault()?.Images.ToList();
                    }

                    if (imageNodes.Any())
                    {
                        return imageNodes;
                    }
                }

                var _images = GetValue(Configuration.Instance.CustomImage);

                return _images.GetImages();
            }
        }

        /// <summary>
        /// A product can have multiple variant groups, 
        /// therefore we allow to configure a default/primary variant group.
        /// If none is configured, we return the first possible item.
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        public virtual IVariantGroup PrimaryVariantGroup
        {
            get
            {
                var variantGroups = VariantGroups.ToList();

                if (!variantGroups.Any())
                {
                    return null;
                }

                if (Properties.ContainsKey("primaryVariantGroup"))
                {
                    var primaryGroupValue = GetValue("primaryVariantGroup");

                    if (!string.IsNullOrEmpty(primaryGroupValue))
                    {
                        var node = Configuration.Resolver.GetService<INodeService>()?.NodeById(primaryGroupValue);

                        if (node != null && node.ContentTypeAlias == "ekmProductVariantGroup")
                        {
                            var variantGroup = __variantGroupCache.Cache[Store.Alias][node.Key];

                            return variantGroup;
                        }
                    }
                }

                var primaryGroup = variantGroups.FirstOrDefault(x => x.Available) ?? variantGroups.FirstOrDefault();

                return primaryGroup;
            }
        }

        /// <summary>
        /// Select the Primary variant.
        /// First Variant in the primary variant group that is available, if none are available, return the first variant.
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        public virtual IVariant PrimaryVariant
        {
            get
            {
                var primaryVariantGroup = PrimaryVariantGroup;

                if (primaryVariantGroup == null)
                {
                    return null;
                }

                var primaryVariant = primaryVariantGroup.Variants.FirstOrDefault(x => x.Available);

                if (primaryVariant == null)
                {
                    primaryVariant = primaryVariantGroup.Variants.FirstOrDefault();
                }

                return primaryVariant;
            }
        }

        /// <summary>
        /// All ancestor categories this <see cref="Product"/> belongs to from the primary category.
        /// </summary>
        /// <returns></returns>
        [JsonIgnore]
        [XmlIgnore]
        public virtual IEnumerable<ICategory> CategoryAncestors => categoryAncestors.AsReadOnly();
        internal List<ICategory> categoryAncestors = new();

        /// <summary>
        /// All categories product belongs to, includes parent category and related categories.
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        public virtual IEnumerable<ICategory> Categories => categories.AsReadOnly();
        internal List<ICategory> categories = new();

        /// <summary>
        /// All ID's of categories product belongs to, includes parent category and related categories.
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
        /// All product urls with context, computed from stores and categories.
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        public virtual List<UmbracoUrl> UrlsWithContext { get; internal set; }

        /// <summary>
        /// Get Price by current store currency
        /// </summary>
        public IPrice Price => GetPrice();

        private IPrice GetPrice()
        {
            var httpCtx = Configuration.Resolver.GetService<IHttpContextAccessor>().HttpContext;
            var cookie = httpCtx?.Request.Cookies["EkomCurrency-" + Store.Alias];

            if (cookie != null && !string.IsNullOrEmpty(cookie))
            {
                var price = Prices.FirstOrDefault(x => x.Currency.CurrencyValue == cookie);

                if (price != null)
                {
                    return price;
                }
            }

            var culture = httpCtx?.Request.HttpContext.Features.Get<IRequestCultureFeature>()?.RequestCulture.Culture;
            
            if (culture != null)
            {
                var price = Prices.FirstOrDefault(x => x.Currency.CurrencyValue == culture.Name);

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
        [JsonIgnore]
        [XmlIgnore]
        public virtual List<IPrice> Prices
        {
            get
            {
                var prices = GetValue("price", Store.Alias)
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
        [JsonIgnore]
        [XmlIgnore]
        public virtual IPrice OriginalPrice
        {
            get
            {
                var priceJson = GetValue("price", Store.Alias);

                var currencyValues = priceJson.GetCurrencyValues();

                var value = currencyValues.Any() ? currencyValues.FirstOrDefault().Value : 0;

                return new Price(value, Store.Currency, Store.Vat, true);
            }
        }

        public virtual decimal Vat
        {
            get
            {
                if (Properties.HasPropertyValue("vat", Store.Alias))
                {
                    var value = GetValue("vat", Store.Alias);

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

                    return Configuration.Resolver.GetService<IMetafieldService>().SerializeMetafields(value, Id);
                }

                return new List<Metavalue>();
            }
        }

        /// <summary>
        /// All child variant groups of this product
        /// </summary>
        public virtual IEnumerable<IVariantGroup> VariantGroups
        {
            get
            {
                var variantGroups = from pair in _variantGroupCache.Cache[Store.Alias]
                    let variantGroup = pair.Value
                    where variantGroup.ProductId == Id
                    orderby variantGroup.SortOrder
                    select variantGroup;

                return variantGroups;
            }
        }

        /// <summary>
        /// All variants belonging to product.
        /// </summary>
        public virtual IEnumerable<IVariant> AllVariants
        {
            get
            {
                var variants = from pair in _variantCache.Cache[Store.Alias]
                    let variant = pair.Value
                    where variant.ProductId == Id
                    select variant;

                return variants;
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
                return AllVariants.Count();
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
            PopulateCategoryAncestors();
            PopulateCategories();

            var urls = Configuration.Resolver.GetService<IUrlService>().BuildProductUrlsWithContext(item, Categories, store, item.Id);

            UrlsWithContext = urls;
            Urls = urls.Select(x => x.Url).ToList();
        }

        private void PopulateCategories()
        {
            int categoryId = ParentId;

            var categoryField = Properties.ContainsKey("categories") ?
                                GetValue("categories") : "";

            var primaryCategory = Catalog.Instance.GetCategory(categoryId, Store.Alias);

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
                        = Catalog.Instance.GetCategory(catId, Store.Alias);

                    if (categoryItem != null && !categories.Contains(categoryItem))
                    {
                        categories.Add(categoryItem);
                    }
                }
            }
        }

        private void PopulateCategoryAncestors()
        {

            foreach (var p in PathArray.Skip(2))
            {
                if (int.TryParse(p, out int id))
                {
                    var c = Catalog.Instance.GetCategory(id, Store.Alias);

                    if (c != null)
                    {
                        categoryAncestors.Add(c);
                    }
                }
            }

            categoryAncestors.Reverse();
        }

        public IEnumerable<IProduct> RelatedProducts(int count = 4)
        {
            var relatedProducts = new List<IProduct>();

            if (Properties.HasPropertyValue("relatedProducts"))
            {
                var val = GetValue("relatedProducts");

                if (!string.IsNullOrEmpty(val))
                {
                    UtilityService.ConvertUdisToGuids(val, out IEnumerable<Guid> guids);

                    foreach (var id in guids.Where(x => x != Key).Take(count))
                    {
                        var product = Catalog.Instance.GetProduct(id, Store.Alias);

                        if (product != null && product.Key != Key)
                        {
                            relatedProducts.Add(product);
                        }
                    }
                }
            }

            if (!relatedProducts.Any())
            {
                var category = Catalog.Instance.GetCategory(ParentId);

                if (category != null)
                {
                    relatedProducts = category.ProductsRecursive().Products.Where(x => x.Id != Id).Take(count).ToList();
                }

            }

            return relatedProducts;
        }
    }
}

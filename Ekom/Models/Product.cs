using Ekom.Cache;
using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models.OrderedObjects;
using Ekom.Utilities;
using Examine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Models;
using Umbraco.Web;

namespace Ekom.Models
{
    /// <summary>
    /// An Ekom store product
    /// </summary>
    public class Product : PerStoreNodeEntity, IProduct
    {
        private IPerStoreCache<IVariant> __variantCache;
        private IPerStoreCache<IVariant> _variantCache =>
            __variantCache ?? (__variantCache = Current.Factory.GetInstance<IPerStoreCache<IVariant>>());

        private IPerStoreCache<IVariantGroup> __variantGroupCache;
        private IPerStoreCache<IVariantGroup> _variantGroupCache =>
            __variantGroupCache ?? (__variantGroupCache = Current.Factory.GetInstance<IPerStoreCache<IVariantGroup>>());

        public virtual IDiscount ProductDiscount(string price = null)
        {
            price = string.IsNullOrEmpty(price) ? Price.OriginalValue.ToString() : price;

            return Current.Factory.GetInstance<IProductDiscountService>()
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
        public string Description => Properties.GetPropertyValue("description", Store.Alias);

        /// <summary>
        /// 
        /// </summary>
        public string Summary => Properties.GetPropertyValue("summary", Store.Alias);

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
        public virtual IEnumerable<Image> Images()
        {
            var _images = Properties.GetPropertyValue(Configuration.Current.CustomImage, Store.Alias);

            var imageNodes = _images.GetImages();

            var primaryVariantGroup = PrimaryVariantGroup;

            if (!imageNodes.Any() && primaryVariantGroup != null)
            {
                imageNodes = primaryVariantGroup.Images();

                if (!imageNodes.Any() && primaryVariantGroup.Variants.Any())
                {
                    imageNodes = primaryVariantGroup.Variants.FirstOrDefault()?.Images();
                }
            }

            return imageNodes;
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
                        var node = NodeHelper.GetNodeByUdi(primaryGroupValue);

                        if (node != null && node.ContentType.Alias == "ekmProductVariantGroup")
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
        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public virtual IEnumerable<ICategory> CategoryAncestors => categoryAncestors.AsReadOnly();
        internal List<ICategory> categoryAncestors = new List<ICategory>();

        /// <summary>
        /// All categories product belongs to, includes parent category.
        /// Does not include categories product is an indirect child of.
        /// </summary>
        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public virtual IEnumerable<ICategory> Categories => categories.AsReadOnly();
        internal List<ICategory> categories = new List<ICategory>();

        /// <summary>
        /// All ID's of categories product belongs to, includes parent category.
        /// Does not include categories product is an indirect child of.
        /// </summary>
        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public virtual IEnumerable<Guid> CategoriesIds
        {
            get
            {
                return Categories.Select(x => x.Key);
            }
        }

        /// <summary>
        /// Product url in relation to current request.
        /// </summary>
        public virtual string Url
        {
            get
            {
                // Urls is a list of relative urls.
                // Umbraco cultures & hostnames can include a prefix
                // This code matches to find correct prefix,
                // aside from that, relative urls should be similar between domains
                var umbCtx = Current.Factory.GetInstance<UmbracoContext>();
                var pubReq = umbCtx.PublishedRequest;

                if (pubReq == null)
                {
                    // Yeah this should probably be a method now.. accessing UmbracoContext as well
                    throw new MissingUmbracoContextException(
                        "Missing UmbracoContext, remember to post to SurfaceControllers including the ufprt form param to include the relevant context when accessing url data"
                    );
                }
                var path = pubReq.Domain.Uri
                            .AbsolutePath
                            .ToLower()
                            .AddTrailing();

                var findUrlByPrefix = Urls
                    .FirstOrDefault(x => x.StartsWith(path));

                return findUrlByPrefix ?? Urls.FirstOrDefault();
            }
        }

        /// <summary>
        /// All product urls, computed from stores and categories.
        /// </summary>
        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public virtual IEnumerable<string> Urls { get; internal set; }

        /// <summary>
        /// Get Price by current store currency
        /// </summary>
        public IPrice Price
        {
            get
            {
                var httpCtx = Current.Factory.GetInstance<HttpContextBase>();

                var cookie = httpCtx?.Request.Cookies["EkomCurrency-" + Store.Alias];

                if (cookie != null && !string.IsNullOrEmpty(cookie.Value))
                {
                    var price = Prices.FirstOrDefault(x => x.Currency.CurrencyValue == cookie.Value);

                    if (price != null)
                    {
                        return price;
                    }
                }

                return Prices.FirstOrDefault();
            }
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
                    return Convert.ToDecimal(Properties.GetPropertyValue("vat", Store.Alias));
                }

                return Store.Vat;
            }
        }

        /// <summary>
        /// All child variant groups of this product
        /// </summary>
        [ScriptIgnore]
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
        [ScriptIgnore]
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
        [ScriptIgnore]
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
        public Product(IStore store) : base(store) { }

        /// <summary>
        /// Construct Product from Examine item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        public Product(ISearchResult item, IStore store) : base(item, store)
        {
            PopulateCategoryAncestors();
            PopulateCategories();

            Urls = UrlHelper.BuildProductUrls(Slug, Categories, store);

            if (!Urls.Any() || string.IsNullOrEmpty(Title))
            {
                throw new Exception("No url's or no title present in product." + Title + " - " + Urls.Any() + " - " + Categories.Any());
            }
        }

        /// <summary>
        /// Construct Product from umbraco publish event
        /// </summary>
        /// <param name="node"></param>
        /// <param name="store"></param>
        public Product(IContent node, IStore store) : base(node, store)
        {
            PopulateCategoryAncestors();
            PopulateCategories();

            Urls = UrlHelper.BuildProductUrls(Slug, Categories, store);

            if (!Urls.Any() || string.IsNullOrEmpty(Title))
            {

                throw new Exception("No url's or no title present in product. " + Title + " - " + Urls.Any());
            }
        }

        private void PopulateCategories()
        {
            int categoryId = ParentId;

            var categoryField = Properties.Any(x => x.Key == "categories") ?
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

        private void PopulateCategoryAncestors()
        {
            var examineItemsFromPath = NodeHelper.GetAllCatalogItemsFromPath(Path);

            foreach (var item in examineItemsFromPath)
            {
                var alias = item.Values["__NodeTypeAlias"];

                if (alias == "ekmCategory")
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
}

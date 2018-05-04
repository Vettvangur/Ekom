using Ekom.Cache;
using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models.OrderedObjects;
using Ekom.Services;
using Ekom.Utilities;
using Examine;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using Umbraco.Core.Models;

namespace Ekom.Models
{
    /// <summary>
    /// An Ekom store product
    /// </summary>
    public class Product : PerStoreNodeEntity, IProduct
    {
        private IPerStoreCache<IVariant> __variantCache;
        private IPerStoreCache<IVariant> _variantCache =>
            __variantCache ?? (__variantCache = Configuration.container.GetInstance<IPerStoreCache<IVariant>>());

        private IPerStoreCache<IVariantGroup> __variantGroupCache;
        private IPerStoreCache<IVariantGroup> _variantGroupCache =>
            __variantGroupCache ?? (__variantGroupCache = Configuration.container.GetInstance<IPerStoreCache<IVariantGroup>>());

        private IDiscount _discount;
        /// <summary>
        /// Best discount mapped to product, populated after discount cache fills.
        /// </summary>
        public virtual IDiscount Discount
        {
            get => _discount;
            internal set
            {
                if (_discount == null
                || (_discount.Amount.Type == value.Amount.Type
                && value.CompareTo(_discount) > 0))
                {
                    _discount = value;
                }

                var oldPrice = new Price(Price.OriginalValue, Store, new OrderedDiscount(_discount));

                var newPrice = new Price(Price.OriginalValue, Store, new OrderedDiscount(value));

                if (oldPrice.AfterDiscount.Value <= newPrice.AfterDiscount.Value)
                {
                    _discount = value;
                }
            }
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
        /// 
        /// </summary>
        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public virtual int Stock => API.Stock.Instance.GetStock(Key);

        /// <summary>
        /// Product images
        /// </summary>
        public virtual IEnumerable<Image> Images
        {
            get
            {
                var _images = Properties.GetPropertyValue("images", Store.Alias);

                var imageNodes = _images.GetImages();

                return imageNodes;
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
                var primaryGroupValue = Properties.GetPropertyValue("primaryVariantGroup", Store.Alias);

                if (!string.IsNullOrEmpty(primaryGroupValue) && VariantGroups.Any())
                {
                    var node = NodeHelper.GetNodeByUdi(primaryGroupValue);

                    if (node != null)
                    {
                        var variantGroup = __variantGroupCache.Cache.FirstOrDefault(x => x.Key == Store.Alias).Value.FirstOrDefault(x => x.Value.Id == node.Id);

                        return variantGroup.Value;
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
        public virtual IEnumerable<ICategory> CategoryAncestors => categoryAncestors.AsReadOnly();
        internal List<ICategory> categoryAncestors = new List<ICategory>();

        /// <summary>
        /// All categories product belongs to, includes parent category.
        /// Does not include categories product is an indirect child of.
        /// </summary>
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
                var httpCtx = Configuration.container.GetInstance<HttpContextBase>();
                var path = httpCtx.Request.Url.AbsolutePath;
                var findUrlByPrefix = Urls.FirstOrDefault(x => x.StartsWith(path));

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
        /// 
        /// </summary>
        public virtual IPrice Price { get; }

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
        public Product(SearchResult item, IStore store) : base(item, store)
        {
            PopulateCategoryAncestors();
            PopulateCategories();

            Price = new Price(Properties.GetPropertyValue("price", Store.Alias), Store);
            Urls = UrlService.BuildProductUrls(Slug, Categories, store);

            if (!Urls.Any() || string.IsNullOrEmpty(Title))
            {
                throw new Exception("No url's or no title present in product");
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

            Price = new Price(Properties.GetPropertyValue("price", Store.Alias), Store);
            Urls = UrlService.BuildProductUrls(Slug, Categories, store);

            if (!Urls.Any() || string.IsNullOrEmpty(Title))
            {
                throw new Exception("No url's or no title present in product");
            }
        }

        private void PopulateCategories()
        {
            int categoryId = Convert.ToInt32(Properties.GetPropertyValue("parentID"));

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
                    var intCatId = Convert.ToInt32(catId);

                    var categoryItem
                        = API.Catalog.Instance.GetCategory(Store.Alias, intCatId);

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
                var alias = item.Fields.GetPropertyValue("nodeTypeAlias");

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

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}

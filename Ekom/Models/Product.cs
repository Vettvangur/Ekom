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
        [JsonIgnore]
        public virtual int Stock => API.Stock.Current.GetStock(Key);

        public virtual IEnumerable<Image> Images
        {
            get
            {
                var _images = Properties.GetPropertyValue("images", Store.Alias);

                var imageNodes = _images.GetImages();

                return imageNodes;
            }
        }

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

                if (VariantGroups.Any())
                {
                    return VariantGroups.FirstOrDefault();
                }

                return null;

            }
        }

        public virtual List<ICategory> CategoryAncestors()
        {
            var examineItemsFromPath = NodeHelper.GetAllCatalogItemsFromPath(Path);

            var list = new List<ICategory>();

            foreach (var item in examineItemsFromPath)
            {
                var alias = item.Fields.GetPropertyValue("nodeTypeAlias");

                if (alias == "ekmCategory")
                {
                    var c = API.Catalog.Current.GetCategory(Store.Alias, item.Id);

                    if (c != null)
                    {
                        list.Add(c);
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// All categories product belongs to, includes parent category.
        /// Does not include categories product is an indirect child of.
        /// </summary>
        public virtual IEnumerable<ICategory> Categories()
        {
            int categoryId = Convert.ToInt32(Properties.GetPropertyValue("parentID"));

            var categoryField = Properties.Any(x => x.Key == "categories") ?
                                Properties.GetPropertyValue("categories") : "";

            var categories = new List<ICategory>();


            var primaryCategory = API.Catalog.Current.GetCategory(Store.Alias, categoryId);

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
                        = API.Catalog.Current.GetCategory(Store.Alias, intCatId);

                    if (categoryItem != null && !categories.Contains(categoryItem))
                    {
                        categories.Add(categoryItem);
                    }
                }
            }

            return categories;
        }

        [JsonIgnore]
        public virtual IEnumerable<Guid> CategoriesIds
        {
            get
            {
                return Categories().Select(x => x.Key);
            }
        }

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

        [JsonIgnore]
        public virtual IEnumerable<string> Urls { get; internal set; }

        /// <summary>
        /// 
        /// </summary>
        public virtual IPrice Price { get; }

        [JsonIgnore]
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
        /// Used by Ekom extensions
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
            Price = new Price(Properties.GetPropertyValue("price", Store.Alias), Store);
            Urls = UrlService.BuildProductUrls(Slug, Categories(), store);

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
            Price = new Price(Properties.GetPropertyValue("price", Store.Alias), Store);
            Urls = UrlService.BuildProductUrls(Slug, Categories(), store);

            if (!Urls.Any() || string.IsNullOrEmpty(Title))
            {
                throw new Exception("No url's or no title present in product");
            }
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}

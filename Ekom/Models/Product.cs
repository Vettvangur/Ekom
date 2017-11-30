using Ekom.Cache;
using Ekom.Helpers;
using Ekom.Interfaces;
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
        private IPerStoreCache<Category> __categoryCache;
        private IPerStoreCache<Category> _categoryCache =>
            __categoryCache ?? (__categoryCache = Configuration.container.GetInstance<IPerStoreCache<Category>>());

        private IPerStoreCache<Variant> __variantCache;
        private IPerStoreCache<Variant> _variantCache =>
            __variantCache ?? (__variantCache = Configuration.container.GetInstance<IPerStoreCache<Variant>>());

        private IPerStoreCache<VariantGroup> __variantGroupCache;
        private IPerStoreCache<VariantGroup> _variantGroupCache =>
            __variantGroupCache ?? (__variantGroupCache = Configuration.container.GetInstance<IPerStoreCache<VariantGroup>>());

        /// <summary>
        /// Product Stock Keeping Unit.
        /// </summary>
        public string SKU
        {
            get
            {
                return Properties.GetPropertyValue("sku");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public string Description
        {
            get
            {
                return Properties.GetStoreProperty("description", _store.Alias);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public string Summary
        {
            get
            {
                return Properties.GetStoreProperty("summary", _store.Alias);
            }
        }

        /// <summary>
        /// Short spaceless descriptive title used to create URLs
        /// </summary>
        public string Slug
        {
            get
            {
                return Properties.GetStoreProperty("slug", _store.Alias);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        [JsonIgnore]
        public int Stock => API.Stock.Current.GetStock(Key);

        public IEnumerable<Image> Images
        {
            get
            {
                var _images = Properties.GetStoreProperty("images", _store.Alias);

                var imageNodes = _images.GetImages();

                return imageNodes;
            }
        }

        public VariantGroup PrimaryVariantGroup
        {
            get
            {
                var primaryGroupValue = Properties.GetStoreProperty("primaryVariantGroup", _store.Alias);

                if (!string.IsNullOrEmpty(primaryGroupValue) && VariantGroups.Any())
                {
                    var node = NodeHelper.GetNodeByUdi(primaryGroupValue);

                    if (node != null)
                    {
                        var variantGroup = __variantGroupCache.Cache.FirstOrDefault(x => x.Key == _store.Alias).Value.FirstOrDefault(x => x.Value.Id == node.Id);

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

        /// <summary>
        /// All categories product belongs to, includes parent category.
        /// Does not include categories product is an indirect child of.
        /// </summary>
        public List<ICategory> Categories()
        {
            int categoryId = Convert.ToInt32(Properties.GetPropertyValue("parentID"));

            var categoryField = Properties.Any(x => x.Key == "categories") ?
                                Properties.GetPropertyValue("categories") : "";

            var categories = new List<ICategory>();

            var primaryCategory = _categoryCache.Cache[_store.Alias]
                                                .FirstOrDefault(x => x.Value.Id == categoryId)
                                                .Value;

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
                        = _categoryCache.Cache[_store.Alias]
                                        .FirstOrDefault(x => x.Value.Id == intCatId)
                                        .Value;

                    if (categoryItem != null && !categories.Contains(categoryItem))
                    {
                        categories.Add(categoryItem);
                    }
                }
            }

            return categories;
        }

        [JsonIgnore]
        public Store Store
        {
            get
            {
                return _store;
            }
        }

        [JsonIgnore]
        public IEnumerable<Guid> CategoriesIds
        {
            get
            {
                return Categories().Select(x => x.Key);
            }
        }

        public string Url
        {
            get
            {
                //var appCache = ApplicationContext.Current.ApplicationCache;
                //var r = appCache.RequestCache.GetCacheItem("ekmRequest") as ContentRequest;

                //var findUrlByPrefix = Urls.FirstOrDefault(x => x.StartsWith(r.DomainPrefix));

                var path = HttpContext.Current.Request.Url.AbsolutePath;
                var findUrlByPrefix = Urls.FirstOrDefault(x => x.StartsWith(path));

                return findUrlByPrefix ?? Urls.FirstOrDefault();
            }
        }

        [JsonIgnore]
        public IEnumerable<string> Urls { get; set; }

        IDiscountedPrice _price;
        /// <summary>
        /// 
        /// </summary>
        public IDiscountedPrice Price => _price
            ?? (_price = new Price(Properties.GetStoreProperty("price", _store.Alias), _store));

        [JsonIgnore]
        public IEnumerable<VariantGroup> VariantGroups
        {
            get
            {
                return _variantGroupCache.Cache[_store.Alias]
                                        .Where(x => x.Value.ProductId == Id)
                                        .Select(x => x.Value);
            }
        }

        /// <summary>
        /// All variants belonging to product.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<Variant> AllVariants
        {
            get
            {
                return _variantCache.Cache[_store.Alias]
                    .Where(x => x.Value.ProductId == Id)
                    .Select(x => x.Value);
            }
        }

        /// <summary>
        /// Used by Ekom extensions
        /// </summary>
        /// <param name="store"></param>
        public Product(Store store) : base(store) { }

        /// <summary>
        /// Get value in properties
        /// </summary>
        /// <param name="alias"></param>
        public string GetProperty(string alias)
        {
            if (Properties.Any(x => x.Key == alias))
            {
                return Properties.FirstOrDefault(x => x.Key == alias).Value;
            }

            return string.Empty;
        }

        /// <summary>
        /// Construct Product from Examine item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        public Product(SearchResult item, Store store) : base(item, store)
        {
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
        public Product(IContent node, Store store) : base(node, store)
        {
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

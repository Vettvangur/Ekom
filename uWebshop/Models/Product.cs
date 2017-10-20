﻿using Examine;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using Umbraco.Core.Models;
using uWebshop.Cache;
using uWebshop.Helpers;
using uWebshop.Interfaces;
using uWebshop.Services;
using uWebshop.Utilities;

namespace uWebshop.Models
{
    /// <summary>
    /// An uWebshop store product
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
        public override string Title
        {
            get
            {
                return Properties.GetStoreProperty("title", _store.Alias);
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

        [JsonIgnore]
        public int Stock
        {
            get
            {
                throw new NotImplementedException();
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
                //var r = appCache.RequestCache.GetCacheItem("uwbsRequest") as ContentRequest;

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
                                        .Where(x => x.Value.ProductKey == Key)
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
                    .Where(x => x.Value.ProductKey == Key)
                    .Select(x => x.Value);
            }
        }

        /// <summary>
        /// Used by uWebshop extensions
        /// </summary>
        /// <param name="store"></param>
        public Product(Store store) : base(store) { }

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

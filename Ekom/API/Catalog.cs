using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;

namespace Ekom.API
{
    /// <summary>
    /// The Ekom API, grants access to the current product/category/variant 
    /// and various other depending on your current routed context.
    /// </summary>
    public class Catalog
    {
        /// <summary>
        /// Catalog Instance
        /// </summary>
        public static Catalog Instance => Current.Factory.GetInstance<Catalog>();

        readonly Configuration _config;
        readonly ILogger _logger;
        readonly AppCaches _appCaches;
        readonly IStoreService _storeSvc;
        readonly IPerStoreCache<IProductDiscount> _productDiscountCache; // must be before product cache
        readonly IPerStoreCache<IProduct> _productCache;
        readonly IPerStoreCache<ICategory> _categoryCache;
        readonly IPerStoreCache<IVariant> _variantCache;
        readonly IPerStoreCache<IVariantGroup> _variantGroupCache;
        /// <summary>
        /// ctor
        /// </summary>
        internal Catalog(
            ILogger logger,
            AppCaches appCaches,
            Configuration config,
            IPerStoreCache<IProduct> productCache,
            IPerStoreCache<ICategory> categoryCache,
            IPerStoreCache<IProductDiscount> productDiscountCache,
            IPerStoreCache<IVariant> variantCache,
            IPerStoreCache<IVariantGroup> variantGroupCache,
            IStoreService storeService
        )
        {
            _config = config;
            _logger = logger;
            _appCaches = appCaches;
            _productCache = productCache;
            _categoryCache = categoryCache;
            _variantCache = variantCache;
            _variantGroupCache = variantGroupCache;
            _productDiscountCache = productDiscountCache;
            _storeSvc = storeService;
        }

        /// <summary>
        /// Get current product using data from the ekmRequest <see cref="ContentRequest"/> object
        /// </summary>
        /// <returns></returns>
        public IProduct GetProduct()
        {
            var r = _appCaches.RequestCache.GetCacheItem<ContentRequest>("ekmRequest");

            return r?.Product;
        }

        public IPerStoreCache<IVariant> GetVariantCache()
        {
            return _variantCache;
        }

        public IPerStoreCache<IVariantGroup> GetVariantGroupCache()
        {
            return _variantGroupCache;
        }

        /// <summary>
        /// Get product by Guid using store from ekmRequest
        /// </summary>
        /// <returns></returns>
        public IProduct GetProduct(Guid Key)
        {
            var store = _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                var product = GetProduct(store.Alias, Key);

                return product;
            }

            return null;
        }

        /// <summary>
        /// Get product by Guid from specific store
        /// </summary>
        public IProduct GetProduct(string storeAlias, Guid Id)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }

            _productCache.Cache[storeAlias].TryGetValue(Id, out var prod);
            return prod;
        }

        /// <summary>
        /// Get product by id using store from ekmRequest
        /// </summary>
        public IProduct GetProduct(int Id)
        {
            var store = _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                var product = GetProduct(store.Alias, Id);

                return product;
            }

            return null;
        }

        /// <summary>
        /// Get product by id from specific store
        /// </summary>
        public IProduct GetProduct(string storeAlias, int Id)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }

            return _productCache.Cache[storeAlias].FirstOrDefault(x => x.Value.Id == Id).Value;
        }

        /// <summary>
        /// Get all products in store, using store from ekmRequest
        /// </summary>
        public IEnumerable<IProduct> GetAllProducts()
        {
            var store = _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                var products = GetAllProducts(store.Alias);

                return products;
            }

            return null;
        }

        /// <summary>
        /// Get all products from specific store
        /// </summary>
        public IEnumerable<IProduct> GetAllProducts(string storeAlias)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }

            return _productCache.Cache[storeAlias].Select(x => x.Value).OrderBy(x => x.SortOrder);
        }

        /// <summary>
        /// Get multiple products by id from store in ekmRequest
        /// </summary>
        public IEnumerable<IProduct> GetProductsByIds(IEnumerable<int> productIds)
        {
            if (productIds == null)
            {
                throw new ArgumentNullException(nameof(productIds));
            }

            var store = _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                var products = GetProductsByIds(productIds, store.Alias);

                return products;
            }

            return null;
        }

        /// <summary>
        /// Get multiple products by id from specific store
        /// </summary>
        public IEnumerable<IProduct> GetProductsByIds(IEnumerable<int> productIds, string storeAlias)
        {
            if (productIds == null)
            {
                throw new ArgumentNullException(nameof(productIds));
            }
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }

            return _productCache.Cache[storeAlias].Where(x => productIds.Contains(x.Value.Id)).Select(x => x.Value).OrderBy(x => x.SortOrder);
        }

        /// <summary>
        /// Get multiple products by key from store in ekmRequest
        /// </summary>
        public IEnumerable<IProduct> GetProductsByKeys(IEnumerable<Guid> productKeys)
        {
            if (productKeys == null)
            {
                throw new ArgumentNullException(nameof(productKeys));
            }

            var store = _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                var products = GetProductsByKeys(productKeys, store.Alias);

                return products;
            }

            return null;
        }

        /// <summary>
        /// Get multiple products by key from specific store
        /// </summary>
        public IEnumerable<IProduct> GetProductsByKeys(IEnumerable<Guid> productKeys, string storeAlias)
        {
            if (productKeys == null)
            {
                throw new ArgumentNullException(nameof(productKeys));
            }
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }

            var products = new List<IProduct>();
            foreach (var id in productKeys)
            {
                if (_productCache.Cache[storeAlias].ContainsKey(id))
                {
                    products.Add(
                        _productCache.Cache[storeAlias][id]
                    );
                }
            }

            return products.OrderBy(x => x.SortOrder);
        }

        /// <summary>
        /// Get category from ekmRequest
        /// </summary>
        /// <returns></returns>
        public ICategory GetCategory()
        {
            if (_appCaches.RequestCache.GetCacheItem<ContentRequest>("ekmRequest") is ContentRequest r)
            {
                return r?.Category;
            }

            return null;
        }

        /// <summary>
        /// Get category by id using store from ekmRequest
        /// </summary>
        public ICategory GetCategory(int Id)
        {
            var store = _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                var category = GetCategory(store.Alias, Id);

                return category;
            }

            return null;
        }

        /// <summary>
        /// Get category by string id using store from ekmRequest
        /// </summary>
        public ICategory GetCategory(string Id)
        {
            if (Id == null)
            {
                throw new ArgumentNullException(nameof(Id));
            }

            var store = _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                return GetCategory(store.Alias, Id);

            }

            return null;
        }

        /// <summary>
        /// Gets the category by id.
        /// </summary>
        /// <param name="storeAlias">The store alias.</param>
        /// <param name="Id">The identifier.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">storeAlias</exception>
        public ICategory GetCategory(string storeAlias, int Id)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }

            return _categoryCache.Cache[storeAlias].FirstOrDefault(x => x.Value.Id == Id).Value;
        }

        /// <summary>
        /// Gets the category by key or id.
        /// </summary>
        /// <param name="storeAlias">The store alias.</param>
        /// <param name="Id">The identifier.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">Id</exception>
        /// <exception cref="ArgumentException">storeAlias</exception>
        public ICategory GetCategory(string storeAlias, string Id)
        {
            if (Id == null)
            {
                throw new ArgumentNullException(nameof(Id));
            }
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }

            if (GuidUdi.TryParse(Id, out GuidUdi udi))
            {
                return _categoryCache.Cache[storeAlias].FirstOrDefault(x => x.Value.Key == udi.Guid).Value;
            }
            if (int.TryParse(Id, out int id))
            {
                return GetCategory(storeAlias, id);
            }

            return null;
        }

        public IEnumerable<ICategory> GetRootCategories()
        {
            var store = _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                var categories = GetRootCategories(store.Alias);

                return categories;
            }

            return null;
        }

        public IEnumerable<ICategory> GetRootCategories(string storeAlias)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }

            return _categoryCache.Cache[storeAlias]
                .Where(x => x.Value.Level == _config.CategoryRootLevel)
                .Select(x => x.Value)
                .OrderBy(x => x.SortOrder);
        }

        public IEnumerable<ICategory> GetAllCategories()
        {
            var store = _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                var categories = GetAllCategories(store.Alias);

                return categories;
            }

            return null;
        }

        public IEnumerable<ICategory> GetAllCategories(string storeAlias)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }

            return _categoryCache.Cache[storeAlias]
                                .Select(x => x.Value)
                                .OrderBy(x => x.SortOrder);
        }

        public IVariant GetVariant(Guid Id)
        {
            var store = _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                var variant = GetVariant(store.Alias, Id);

                return variant;
            }

            return null;
        }

        public IVariant GetVariant(string storeAlias, Guid key)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }

            if (_variantCache.Cache[storeAlias].TryGetValue(key, out var val))
            {
                return val;
            }

            return null;
        }

        public IEnumerable<IVariant> GetVariantsByGroup(string storeAlias, int Id)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }

            return _variantCache.Cache[storeAlias]
                                   .Where(x => x.Value.VariantGroup.Id == Id)
                                   .Select(x => x.Value).OrderBy(x => x.SortOrder);
        }

        public IVariantGroup GetVariantGroup(string storeAlias, Guid key)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }

            if (_variantGroupCache.Cache[storeAlias].TryGetValue(key, out var val))
            {
                return val;
            }

            return null;
        }
    }
}

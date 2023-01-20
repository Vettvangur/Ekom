#if NETFRAMEWORK
using System.Web;
using System.Web.Http;
#else
using Microsoft.AspNetCore.Http;
#endif
using Ekom.Cache;
using Ekom.Models;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;

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
        public static Catalog Instance => Configuration.Resolver.GetService<Catalog>();

        readonly Configuration _config;
        readonly ILogger<Catalog> _logger;
        readonly HttpContext _httpContext;
        readonly IStoreService _storeSvc;
        readonly IMetafieldService _metafieldService;
        readonly IPerStoreCache<IProductDiscount> _productDiscountCache; // must be before product cache
        readonly IPerStoreCache<IProduct> _productCache;
        readonly IPerStoreCache<ICategory> _categoryCache;
        readonly IPerStoreCache<IVariant> _variantCache;
        readonly IPerStoreCache<IVariantGroup> _variantGroupCache;
        /// <summary>
        /// ctor
        /// </summary>
        internal Catalog(
            ILogger<Catalog> logger,
            Configuration config,
            IMetafieldService metafieldService,
            IPerStoreCache<IProduct> productCache,
            IPerStoreCache<ICategory> categoryCache,
            IPerStoreCache<IProductDiscount> productDiscountCache,
            IPerStoreCache<IVariant> variantCache,
            IPerStoreCache<IVariantGroup> variantGroupCache,
            IStoreService storeService
#if NETCOREAPP
            ,IHttpContextAccessor httpContextAccessor
#endif
        )
        {
            _config = config;
            _logger = logger;
            _productCache = productCache;
            _categoryCache = categoryCache;
            _variantCache = variantCache;
            _variantGroupCache = variantGroupCache;
            _productDiscountCache = productDiscountCache;
            _storeSvc = storeService;
            _metafieldService = metafieldService;

#if NETFRAMEWORK
            _httpContext = HttpContext.Current;
#else
            _httpContext = httpContextAccessor.HttpContext;
#endif
        }

        /// <summary>
        /// Get current product using data from the ekmRequest <see cref="ContentRequest"/> object
        /// </summary>
        /// <returns></returns>
        public IProduct GetProduct()
        {
            ContentRequest contentRequest = null;
#if NETFRAMEWORK
            if (_httpContext.Items.Contains("ekmRequest"))
#else
            if (_httpContext.Items.ContainsKey("umbrtmche-ekmRequest"))
#endif
            {
                var r = _httpContext.Items["umbrtmche-ekmRequest"] as Lazy<object>;
                contentRequest = r.Value as ContentRequest;
                return contentRequest?.Product;
            }
            return null;
        }

        /// <summary>
        /// Get product by SKU using store from ekmRequest
        /// </summary>
        /// <returns></returns>
        public IProduct GetProduct(string sku)
        {
            var store = _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                var product = GetProduct(store.Alias, sku);

                return product;
            }

            return null;
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
        /// Get product by SKU from specific store
        /// </summary>
        public IProduct GetProduct(string storeAlias, string sku)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }

            if (string.IsNullOrEmpty(sku))
            {
                throw new ArgumentException(nameof(sku));
            }

            if (!_productCache.Cache.ContainsKey(storeAlias))
            {
                return null;
            }

            if (_productCache.Cache[storeAlias].Any(x => x.Value.SKU == sku))
            {
                return _productCache.Cache[storeAlias].FirstOrDefault(x => x.Value.SKU == sku).Value;
            }

            return null;
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

            if (!_productCache.Cache.ContainsKey(storeAlias))
            {
                return null;
            }

            return _productCache.Cache[storeAlias].FirstOrDefault(x => x.Value.Id == Id).Value;
        }

        /// <summary>
        /// Get all products in store, using store from ekmRequest
        /// </summary>
        public ProductResponse GetAllProducts(ProductQuery query = null)
        {
            var store = _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                var products = GetAllProducts(store.Alias, query);

                return products;
            }

            return new ProductResponse(Enumerable.Empty<IProduct>(), query);
        }

        /// <summary>
        /// Get all products from specific store
        /// </summary>
        public ProductResponse GetAllProducts(string storeAlias, ProductQuery query = null)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }

            if (!_productCache.Cache.ContainsKey(storeAlias))
            {
                return null;
            }

            var products = _productCache.Cache[storeAlias].Select(x => x.Value).OrderBy(x => x.SortOrder);
            
            return new ProductResponse(products,query);
        }

        /// <summary>
        /// Get multiple products by id from store in ekmRequest
        /// </summary>
        public ProductResponse GetProductsByIds(IEnumerable<int> productIds, ProductQuery query = null)
        {
            if (productIds == null)
            {
                throw new ArgumentNullException(nameof(productIds));
            }

            var store = _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                return GetProductsByIds(productIds, store.Alias, query);
            }

            return new ProductResponse(Enumerable.Empty<IProduct>(), query);
        }

        /// <summary>
        /// Get multiple products by id from specific store
        /// </summary>
        public ProductResponse GetProductsByIds(IEnumerable<int> productIds, string storeAlias, ProductQuery query = null)
        {
            if (productIds == null)
            {
                throw new ArgumentNullException(nameof(productIds));
            }
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }

            var products = new List<IProduct>();

            foreach (var id in productIds)
            {
                var product = _productCache.Cache[storeAlias].FirstOrDefault(x => x.Value.Id == id).Value;

                if (product != null)
                {
                    products.Add(
                       product
                    );
                }
            }

            return new ProductResponse(products, query);
        }

        /// <summary>
        /// Get multiple products by key from store in ekmRequest
        /// </summary>
        public ProductResponse GetProductsByKeys(IEnumerable<Guid> productKeys, ProductQuery query = null)
        {
            if (productKeys == null)
            {
                throw new ArgumentNullException(nameof(productKeys));
            }

            var store = _storeSvc.GetStoreFromCache();

            if (store != null)
            {
               return GetProductsByKeys(productKeys, store.Alias);
            }

            return new ProductResponse(Enumerable.Empty<IProduct>(), query);
        }

        /// <summary>
        /// Get multiple products by key from specific store
        /// </summary>
        public ProductResponse GetProductsByKeys(IEnumerable<Guid> productKeys, string storeAlias, ProductQuery query = null)
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

            return new ProductResponse(products, query);
        }

        /// <summary>
        /// Get category from ekmRequest
        /// </summary>
        /// <returns></returns>
        public ICategory GetCategory()
        {
#if NETFRAMEWORK
            if (_httpContext.Items.Contains("ekmRequest"))
#else
            if (_httpContext.Items.ContainsKey("umbrtmche-ekmRequest"))
#endif
            {
                var r = _httpContext.Items["umbrtmche-ekmRequest"] as Lazy<object>;
                var contentRequest = r.Value as ContentRequest;
                return contentRequest?.Category;
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
            if (!_categoryCache.Cache.ContainsKey(storeAlias))
            {
                return null;
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
            
            if (!_categoryCache.Cache.ContainsKey(storeAlias))
            {
                return null;
            }


            if (Id.Contains("umb"))
            {
                var value = Id.Substring(Id.LastIndexOf('/') + 1);

                if (Guid.TryParse(value, out Guid guid))
                {
                    return _categoryCache.Cache[storeAlias].FirstOrDefault(x => x.Value.Key == guid).Value;
                }
            }

            if (Guid.TryParse(Id, out Guid _guid))
            {
                return _categoryCache.Cache[storeAlias].FirstOrDefault(x => x.Value.Key == _guid).Value;
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

            return Enumerable.Empty<ICategory>();
        }

        public IEnumerable<ICategory> GetRootCategories(string storeAlias)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }
            if (!_categoryCache.Cache.ContainsKey(storeAlias))
            {
                return null;
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

            return Enumerable.Empty<ICategory>();
        }

        public IEnumerable<ICategory> GetAllCategories(string storeAlias)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }
            
            if (!_categoryCache.Cache.ContainsKey(storeAlias))
            {
                return null;
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

        /// <summary>
        /// Get variant by SKU using store from ekmRequest
        /// </summary>
        /// <returns></returns>
        public IVariant GetVariant(string sku)
        {
            var store = _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                var variant = GetVariant(store.Alias, sku);

                return variant;
            }

            return null;
        }

        /// <summary>
        /// Get variant by SKU from specific store
        /// </summary>
        public IVariant GetVariant(string storeAlias, string sku)
        {
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }

            if (string.IsNullOrEmpty(sku))
            {
                throw new ArgumentException(nameof(sku));
            }

            if (_variantCache.Cache[storeAlias].Any(x => x.Value.SKU == sku))
            {
                return _variantCache.Cache[storeAlias].FirstOrDefault(x => x.Value.SKU == sku).Value;
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
                                   .Where(x => x.Value.VariantGroup != null && x.Value.VariantGroup.Id == Id)
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

        public IEnumerable<Metafield> GetMetafields()
        {
            return _metafieldService.GetMetafields();
        }

    }
}

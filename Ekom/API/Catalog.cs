using Ekom.Cache;
using Ekom.Models;
using Ekom.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Ekom.API
{
    /// <summary>
    /// The Catalog API, grants access to the current product/category/variant 
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
            IStoreService storeService,
            IHttpContextAccessor httpContextAccessor
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
            _httpContext = httpContextAccessor.HttpContext;
        }

        /// <summary>
        /// Get current product using data from the ekmRequest <see cref="ContentRequest"/> object
        /// </summary>
        /// <returns></returns>
        public IProduct GetProduct()
        {
            ContentRequest contentRequest = null;
            if (_httpContext.Items.ContainsKey("umbrtmche-ekmRequest"))
            {
                var r = _httpContext.Items["umbrtmche-ekmRequest"] as Lazy<object>;
                contentRequest = r.Value as ContentRequest;
                return contentRequest?.Product;
            }
            return null;
        }

        /// <summary>
        /// Get product by Route
        /// </summary>
        /// <returns></returns>
        public IProduct? GetProductByRoute(string route, string? storeAlias = null)
        {
            if (string.IsNullOrWhiteSpace(route))
            {
                throw new ArgumentException("Route cannot be null or empty.", nameof(route));
            }

            // Get store based on alias or from cache
            var store = !string.IsNullOrWhiteSpace(storeAlias)
                        ? _storeSvc.GetStoreByAlias(storeAlias)
                        : _storeSvc.GetStoreFromCache();

            if (store is null)
            {
                _logger.LogError("Store not found. Alias: {StoreAlias}", storeAlias ?? "default");
                return null;
            }

            // Try to get the store's products from cache
            if (_productCache.Cache.TryGetValue(store.Alias, out var productDictionary))
            {
                // Return the first product matching the given route, if it exists
                return productDictionary.Values.FirstOrDefault(p => p.Urls.Contains(route));
            }

            _logger.LogError("Product cache does not contain store: {StoreAlias}", store.Alias);

            return null;
        }

        /// <summary>
        /// Get product by SKU
        /// </summary>
        /// <returns></returns>
        public IProduct GetProduct(string sku, string storeAlias = null)
        {
            var store = !string.IsNullOrEmpty(storeAlias) ? _storeSvc.GetStoreByAlias(storeAlias) : _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                if (string.IsNullOrEmpty(sku))
                {
                    throw new ArgumentException(nameof(sku));
                }

                if (!_productCache.Cache.ContainsKey(store.Alias))
                {
                    return null;
                }

                if (_productCache.Cache[store.Alias].Any(x => x.Value.SKU == sku))
                {
                    return _productCache.Cache[store.Alias].FirstOrDefault(x => x.Value.SKU == sku).Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Get product by Guid
        /// </summary>
        /// <returns></returns>
        public IProduct? GetProduct(Guid key, string storeAlias = null)
        {
            // Get the specified store or default store
            var store = !string.IsNullOrEmpty(storeAlias) ? _storeSvc.GetStoreByAlias(storeAlias) : _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                // Try to get the product from the specified store
                if (_productCache.Cache[store.Alias].TryGetValue(key, out var prod))
                {
                    return prod;
                }
            }

            // If not found, check all other stores
            foreach (var otherStore in _storeSvc.GetAllStores())
            {
                // Skip the store we've already checked (if storeAlias was provided)
                if (store != null && otherStore.Alias == store.Alias)
                {
                    continue;
                }

                // Try to get the product from the current store in the iteration
                if (_productCache.Cache[otherStore.Alias].TryGetValue(key, out var prod))
                {
                    return prod;
                }
            }

            // If the product is not found in any store, return null
            return null;
        }

        [Obsolete]
        public IProduct GetProduct(string storeAlias, Guid Key)
        {
            return GetProduct(Key, storeAlias);
        }

        [Obsolete]
        public IProduct GetProduct(string storeAlias, int id)
        {
            return GetProduct(id, storeAlias);
        }

        /// <summary>
        /// Get product by id using store from ekmRequest
        /// </summary>
        public IProduct GetProduct(int Id, string storeAlias = null)
        {
            var store = !string.IsNullOrEmpty(storeAlias) ? _storeSvc.GetStoreByAlias(storeAlias) : _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                if (!_productCache.Cache.ContainsKey(store.Alias))
                {
                    return null;
                }

                var product = _productCache.Cache[store.Alias].FirstOrDefault(x => x.Value.Id == Id).Value;

                return product;
            }

            return null;
        }

        /// <summary>
        /// Get all products in store, using store from ekmRequest
        /// </summary>
        public ProductResponse GetAllProducts(ProductQuery query = null)
        {
            var store = !string.IsNullOrEmpty(query?.StoreAlias) ? _storeSvc.GetStoreByAlias(query.StoreAlias) : _storeSvc.GetStoreFromCache();

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
                return new ProductResponse();
            }

            var products = _productCache.Cache[storeAlias].Select(x => x.Value).OrderBy(x => x.SortOrder);
            
            return new ProductResponse(products,query);
        }

        /// <summary>
        /// Get multiple products by id from store in ekmRequest
        /// </summary>
        public ProductResponse GetProductsByIds(ProductQuery query = null)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            var store = !string.IsNullOrEmpty(query?.StoreAlias) ? _storeSvc.GetStoreByAlias(query.StoreAlias) : _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                return GetProductsByIds(store.Alias, query);
            }

            return new ProductResponse(Enumerable.Empty<IProduct>(), query);
        }

        /// <summary>
        /// Get multiple products by id from specific store
        /// </summary>
        public ProductResponse GetProductsByIds(string storeAlias, ProductQuery query = null)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }

            var products = new List<IProduct>();

            foreach (var id in query.Ids)
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
        public ProductResponse GetProductsByKeys(ProductQuery query = null)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            var store = !string.IsNullOrEmpty(query?.StoreAlias) ? _storeSvc.GetStoreByAlias(query.StoreAlias) : _storeSvc.GetStoreFromCache();
            
            if (store != null)
            {
               return GetProductsByKeys(store.Alias, query);
            }

            return new ProductResponse(Enumerable.Empty<IProduct>(), query);
        }

        /// <summary>
        /// Get multiple products by key from specific store
        /// </summary>
        public ProductResponse GetProductsByKeys(string storeAlias, ProductQuery query = null)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }
            if (string.IsNullOrEmpty(storeAlias))
            {
                throw new ArgumentException(nameof(storeAlias));
            }

            var products = new List<IProduct>();
            foreach (var id in query.Keys)
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
            if (_httpContext.Items.ContainsKey("umbrtmche-ekmRequest"))
            {
                var r = _httpContext.Items["umbrtmche-ekmRequest"] as Lazy<object>;
                var contentRequest = r.Value as ContentRequest;
                return contentRequest?.Category;
            }
            return null;
        }

        /// <summary>
        /// Get category by string id, supports udi, guid and int 
        /// </summary>
        public ICategory GetCategory(string Id, string storeAlias = null)
        {
            if (Id == null)
            {
                throw new ArgumentNullException(nameof(Id));
            }

            var store = !string.IsNullOrEmpty(storeAlias) ? _storeSvc.GetStoreByAlias(storeAlias) : _storeSvc.GetStoreFromCache();

            if (store != null)
            {

                if (!_categoryCache.Cache.ContainsKey(store.Alias))
                {
                    return null;
                }

                if (UtilityService.ConvertUdiToGuid(Id, out Guid guid))
                {
                    return GetCategory(guid, store.Alias);
                }

                if (Guid.TryParse(Id, out Guid _guid))
                {
                    return GetCategory(_guid, store.Alias);
                }

                if (int.TryParse(Id, out int id))
                {
                    return GetCategory(id, store.Alias);
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the category by int id.
        /// </summary>
        /// <param name="Id">The identifier.</param>
        /// <param name="storeAlias">The store alias.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">storeAlias</exception>
        public ICategory? GetCategory(int Id, string storeAlias = null)
        {
            var store = !string.IsNullOrEmpty(storeAlias) ? _storeSvc.GetStoreByAlias(storeAlias) : _storeSvc.GetStoreFromCache();

            if (!_categoryCache.Cache.ContainsKey(store.Alias))
            {
                return null;
            }

            // Attempt to find the category with the given ID in the cache for the specified store alias
            var categoryPair = _categoryCache.Cache[store.Alias].FirstOrDefault(x => x.Value.Id == Id);

            // Check if a valid KeyValuePair was found and if the category is not null
            if (!categoryPair.Equals(default(KeyValuePair<int, ICategory>)) && categoryPair.Value != null)
            {
                return categoryPair.Value;
            }

            return null;
        }

        /// <summary>
        /// Gets the category by guid id.
        /// </summary>
        /// <param name="Id">The identifier.</param>
        /// <param name="storeAlias">The store alias.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">storeAlias</exception>
        public ICategory GetCategory(Guid Id, string storeAlias = null)
        {
            var store = !string.IsNullOrEmpty(storeAlias) ? _storeSvc.GetStoreByAlias(storeAlias) : _storeSvc.GetStoreFromCache();

            if (!_categoryCache.Cache.ContainsKey(store.Alias))
            {
                return null;
            }

            return _categoryCache.Cache[store.Alias].TryGetValue(Id, out var cat) ? cat : null;
        }

        [Obsolete]
        public ICategory GetCategory(string storeAlias, int id)
        {
            return GetCategory(id, storeAlias);
        }

        public IEnumerable<ICategory> GetRootCategories(string storeAlias = null)
        {
            var store = !string.IsNullOrEmpty(storeAlias) ? _storeSvc.GetStoreByAlias(storeAlias) : _storeSvc.GetStoreFromCache();

            if (store != null)
            {

                if (!_categoryCache.Cache.ContainsKey(store.Alias))
                {
                    return null;
                }

                return _categoryCache.Cache[store.Alias]
                    .Where(x => x.Value.Level == _config.CategoryRootLevel)
                    .Select(x => x.Value)
                    .OrderBy(x => x.SortOrder);
            }

            return Enumerable.Empty<ICategory>();
        }

        /// <summary>
        /// Get category by Route
        /// </summary>
        /// <returns></returns>
        public ICategory GetCategoryByRoute(string route, string storeAlias = null)
        {
            var store = !string.IsNullOrEmpty(storeAlias) ? _storeSvc.GetStoreByAlias(storeAlias) : _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                if (string.IsNullOrEmpty(route))
                {
                    throw new ArgumentException(nameof(route));
                }

                if (!_categoryCache.Cache.ContainsKey(store.Alias))
                {
                    return null;
                }

                var category = _categoryCache.Cache[store.Alias].FirstOrDefault(x => x.Value.Urls.Contains(route)).Value;

                return category;
            }

            return null;
        }

        public IEnumerable<ICategory> GetAllCategories(string storeAlias = null)
        {
            var store = !string.IsNullOrEmpty(storeAlias) ? _storeSvc.GetStoreByAlias(storeAlias) : _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                if (!_categoryCache.Cache.ContainsKey(store.Alias))
                {
                    return null;
                }

                return _categoryCache.Cache[store.Alias]
                                    .Select(x => x.Value)
                                    .OrderBy(x => x.SortOrder);
            }

            return Enumerable.Empty<ICategory>();
        }

        /// <summary>
        /// Get multiple categories by id from store in ekmRequest (slower then GetCategoriesByKeys)
        /// </summary>
        public IEnumerable<ICategory> GetCategoriesByIds(int[] ids, string? storeAlias = null)
        {
            var store = !string.IsNullOrEmpty(storeAlias) ? _storeSvc.GetStoreByAlias(storeAlias) : _storeSvc.GetStoreFromCache();

            if (store == null || !_categoryCache.Cache.ContainsKey(store.Alias))
            {
                return Enumerable.Empty<ICategory>();
            }

            var categoriesInStore = _categoryCache.Cache[store.Alias].Values; // Assuming this gets all category values
            var orderedCategories = new List<ICategory>();

            // Iterate over ids to maintain order
            foreach (var id in ids)
            {
                // Filter categories based on id and maintain the order
                var category = categoriesInStore.FirstOrDefault(c => c.Id == id);
                if (category != null)
                {
                    orderedCategories.Add(category);
                }
            }

            return orderedCategories;
        }


        /// <summary>
        /// Get multiple categories by key from store in ekmRequest (faster then GetCategoriesByIds)
        /// </summary>
        public IEnumerable<ICategory> GetCategoriesByKeys(Guid[] keys, string? storeAlias = null)
        {
            var store = !string.IsNullOrEmpty(storeAlias) ? _storeSvc.GetStoreByAlias(storeAlias) : _storeSvc.GetStoreFromCache();

            if (store == null || !_categoryCache.Cache.ContainsKey(store.Alias))
            {
                return Enumerable.Empty<ICategory>();
            }

            var categoriesInStore = _categoryCache.Cache[store.Alias];
            var orderedCategories = new List<ICategory>();

            // Iterate over ids to maintain order
            foreach (var key in keys)
            {
                // Attempt to find the category by KEY and preserve the order based on the input array
                if (categoriesInStore.TryGetValue(key, out var category))
                {
                    orderedCategories.Add(category);
                }
            }

            return orderedCategories;
        }

        public IVariant GetVariant(Guid Id, string storeAlias = null)
        {
            var store = !string.IsNullOrEmpty(storeAlias) ? _storeSvc.GetStoreByAlias(storeAlias) : _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                if (_variantCache.Cache[store.Alias].TryGetValue(Id, out var val))
                {
                    return val;
                }
            }

            return null;
        }

        /// <summary>
        /// Get variant by SKU
        /// </summary>
        /// <returns></returns>
        public IVariant GetVariant(string sku, string storeAlias = null)
        {
            var store = !string.IsNullOrEmpty(storeAlias) ? _storeSvc.GetStoreByAlias(storeAlias) : _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                if (string.IsNullOrEmpty(store.Alias))
                {
                    throw new ArgumentException(nameof(store.Alias));
                }

                if (string.IsNullOrEmpty(sku))
                {
                    throw new ArgumentException(nameof(sku));
                }

                if (_variantCache.Cache[store.Alias].Any(x => x.Value.SKU == sku))
                {
                    return _variantCache.Cache[store.Alias].FirstOrDefault(x => x.Value.SKU == sku).Value;
                }
            }

            return null;
        }

        [Obsolete]
        public IVariant GetVariant(string storeAlias, Guid key)
        {
            return GetVariant(key, storeAlias);
        }

        public IEnumerable<IVariant> GetVariantsByGroup(int id, string storeAlias = null)
        {
            var store = !string.IsNullOrEmpty(storeAlias)
                        ? _storeSvc.GetStoreByAlias(storeAlias)
                        : _storeSvc.GetStoreFromCache();

            if (store == null)
            {
                return Enumerable.Empty<IVariant>();
            }

            if (_variantCache?.Cache.TryGetValue(store.Alias, out var variants) != true)
            {
                return Enumerable.Empty<IVariant>();
            }

            return variants.Values
                           .Where(v => v.VariantGroupId == id)
                           .OrderBy(v => v.SortOrder);
        }

        [Obsolete]
        public IEnumerable<IVariant> GetVariantsByGroup(string storeAlias, int Id)
        {
            return GetVariantsByGroup(Id, storeAlias);
        }

        public IVariantGroup GetVariantGroup(Guid key, string storeAlias = null)
        {
            var store = !string.IsNullOrEmpty(storeAlias) ? _storeSvc.GetStoreByAlias(storeAlias) : _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                if (_variantGroupCache.Cache[store.Alias].TryGetValue(key, out var val))
                {
                    return val;
                }
            }

            return null;
        }

        [Obsolete]
        public IVariantGroup GetVariantGroup(string storeAlias, Guid key)
        {
            return GetVariantGroup(key, storeAlias);
        }

        public IVariantGroup GetVariantGroup(int id, string storeAlias = null)
        {
            var store = !string.IsNullOrEmpty(storeAlias) ? _storeSvc.GetStoreByAlias(storeAlias) : _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                if (_variantGroupCache.Cache.TryGetValue(store.Alias, out var variantGroups))
                {
                    return variantGroups.Values.FirstOrDefault(v => v.Id == id);
                }
            }

            return null;
        }

        [Obsolete]
        public IVariantGroup GetVariantGroup(string storeAlias, int id)
        {
            return GetVariantGroup(id, storeAlias);
        }


        public IEnumerable<Metafield> GetMetafields()
        {
            return _metafieldService.GetMetafields();
        }

        /// <summary>
        /// Get Related Products
        /// </summary>
        public IEnumerable<IProduct> GetRelatedProducts(Guid productId, int count = 4, string? storeAlias = null)
        {            
            var product = GetProduct(productId, storeAlias);

            if (product == null)
            {
                return Enumerable.Empty<IProduct>();
            }
            
            return product.RelatedProducts(count);
        }

        /// <summary>
        /// Get Related Products By Sku
        /// </summary>
        public IEnumerable<IProduct> GetRelatedProductsBySku(string sku, int count = 4, string storeAlias = null)
        {
            var product = GetProduct(sku, storeAlias);

            if (product == null)
            {
                throw new ArgumentNullException(nameof(product));
            }

            return product.RelatedProducts(count);
        }

        /// <summary>
        /// Search Products
        /// </summary>
        public ProductResponse ProductSearch(SearchRequest req)
        {
            if (string.IsNullOrEmpty(req?.SearchQuery))
            {
                return new ProductResponse();
            }

            if (req.NodeTypeAlias == null || !req.NodeTypeAlias.Any())
            {
                req.NodeTypeAlias = new string[] { "ekmProduct", "ekmVariant" };
            }

            var scope = Configuration.Resolver.CreateScope();
            var _searhService = scope.ServiceProvider.GetService<ICatalogSearchService>();

            var result = _searhService.ProductQuery(req, out long total);

            scope.Dispose();

            var productQuery = new ProductQuery();

            productQuery.Ids = result.Select(x => x);
            productQuery.MetaFilters = req.MetaFilters;
            productQuery.PropertyFilters = req.PropertyFilters;
            productQuery.OrderBy = req.OrderBy;
            productQuery.StoreAlias = req.StoreAlias;
            
            var products = GetProductsByIds(productQuery);

            return products;
        }

    }
}

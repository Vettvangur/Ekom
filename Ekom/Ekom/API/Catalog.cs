using Ekom.Cache;
using Ekom.Events;
using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Ekom.API;

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
    readonly IPerStoreIndexedCache<IProduct> _productCache;
    readonly IPerStoreIndexedCache<ICategory> _categoryCache;
    readonly IPerStoreIndexedCache<IVariant> _variantCache;
    readonly IPerStoreIndexedCache<IVariantGroup> _variantGroupCache;
    readonly IProductFilterService _productFilterService;
    /// <summary>
    /// ctor
    /// </summary>
    internal Catalog(
        ILogger<Catalog> logger,
        Configuration config,
        IMetafieldService metafieldService,
        IPerStoreIndexedCache<IProduct> productCache,
        IPerStoreIndexedCache<ICategory> categoryCache,
        IPerStoreCache<IProductDiscount> productDiscountCache,
        IPerStoreIndexedCache<IVariant> variantCache,
        IPerStoreIndexedCache<IVariantGroup> variantGroupCache,
        IStoreService storeService,
        IHttpContextAccessor httpContextAccessor,
        IProductFilterService productFilterService)
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
        _productFilterService = productFilterService;
    }

    /// <summary>
    /// Get current product using data from the ekmRequest <see cref="ContentRequest"/> object
    /// </summary>
    /// <param name="raiseEvent">Control if event should be triggered</param>
    /// <returns></returns>
    public IProduct? GetProduct(bool raiseEvent = true)
    {
        if (_httpContext != null &&
            _httpContext.Items != null &&
            _httpContext.Items.TryGetValue(Configuration.EkmRequestKey, out var item) &&
            item is Lazy<object> lazy &&
            lazy.Value is ContentRequest contentRequest)
        {
            var product = contentRequest.Product;

            if (product != null && raiseEvent)
            {
                return CatalogEvents.RaiseOnBeforeReturnProduct(product);
            }
        }

        return null;
    }

    /// <summary>
    /// Get product by Route
    /// </summary>
    /// <param name="route">The route.</param>
    /// <param name="storeAlias">The store alias.</param>
    /// <param name="raiseEvent">Control if event should be triggered</param>
    /// <returns></returns>
    public IProduct? GetProductByRoute(string route, string? storeAlias = null, bool raiseEvent = true)
    {
        return GetSingleProduct(route, storeAlias, route: true, raiseEvent: raiseEvent);
    }

    /// <summary>
    /// Get product by SKU
    /// </summary>
    /// <param name="sku">The SKU.</param>
    /// <param name="storeAlias">The store alias.</param>
    /// <param name="global">Looks for the category in all store caches as fallback</param>
    /// <param name="raiseEvent">Control if event should be triggered</param>
    /// <returns></returns>
    public IProduct? GetProduct(string sku, string? storeAlias = null, bool? global = null, bool raiseEvent = true)
    {
        return GetSingleProduct(sku, storeAlias, sku: true, global: global, raiseEvent: raiseEvent);
    }

    /// <summary>
    /// Get product by Guid
    /// </summary>
    /// <param name="key">The product key.</param>
    /// <param name="storeAlias">The store alias.</param>
    /// <param name="global">Looks for the category in all store caches as fallback</param>
    /// <param name="raiseEvent">Control if event should be triggered</param>
    /// <returns></returns>
    public IProduct? GetProduct(Guid key, string? storeAlias = null, bool? global = null, bool raiseEvent = true)
    {
        return GetSingleProduct(key.ToString(), storeAlias, global: global, raiseEvent: raiseEvent);
    }

    /// <summary>
    /// Get product by id using store from ekmRequest
    /// </summary>
    /// <param name="Id">The identifier.</param>
    /// <param name="storeAlias">The store alias.</param>
    /// <param name="global">Looks for the category in all store caches as fallback</param>
    /// <param name="raiseEvent">Control if event should be triggered</param>
    public IProduct? GetProduct(int Id, string? storeAlias = null, bool ? global = null, bool raiseEvent = true)
    {
        return GetSingleProduct(Id.ToString(), storeAlias, global: global, raiseEvent: raiseEvent);
    }

    [Obsolete]
    public IProduct? GetProduct(string storeAlias, Guid Key)
    {
        return GetProduct(Key, storeAlias);
    }

    [Obsolete]
    public IProduct? GetProduct(string storeAlias, int id)
    {
        return GetProduct(id, storeAlias);
    }

    private IProduct? GetSingleProduct(
        string id,
        string? storeAlias = null,
        bool route = false,
        bool sku = false,
        bool? global = null,
        bool raiseEvent = true)
    {
        var enableGlobal = global ?? Configuration.Instance.GlobalCatalog;

        var store = !string.IsNullOrEmpty(storeAlias)
            ? _storeSvc.GetStoreByAlias(storeAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null)
            return null;

        // Route lookup still requires scanning unless you add a Url->Key index
        if (route)
        {
            if (!_productCache.Cache.TryGetValue(store.Alias, out var productDict))
                return null;

            var product = productDict.Values
                .FirstOrDefault(p => p.Urls.Any(url => url.Equals(id, StringComparison.OrdinalIgnoreCase)));

            return raiseEvent ? CatalogEvents.RaiseOnBeforeReturnProduct(product) : product;
        }

        // SKU lookup (O(1) with new index)
        if (sku)
        {
            if (_productCache.TryGetBySku(store.Alias, id, out var product) && product != null)
                return raiseEvent ? CatalogEvents.RaiseOnBeforeReturnProduct(product) : product;

            if (enableGlobal)
            {
                var globalProduct = FindProductInAnyStoreBySku(store, id);
                return raiseEvent ? CatalogEvents.RaiseOnBeforeReturnProduct(globalProduct) : globalProduct;
            }

            return null;
        }

        // Integer Id lookup (O(1) with new index)
        if (int.TryParse(id, out var intId))
        {
            if (_productCache.TryGetById(store.Alias, intId, out var product) && product != null)
                return raiseEvent ? CatalogEvents.RaiseOnBeforeReturnProduct(product) : product;

            if (enableGlobal)
            {
                var globalProduct = FindProductInAnyStoreById(store, intId);
                return raiseEvent ? CatalogEvents.RaiseOnBeforeReturnProduct(globalProduct) : globalProduct;
            }

            return null;
        }

        // GUID key lookup (already O(1))
        if (UtilityService.ConvertUdiToGuid(id, out var parsedGuid))
            id = parsedGuid.ToString();

        if (Guid.TryParse(id, out var guid))
        {
            // You can either use TryGetByKey wrapper if you added it, or direct dict lookup
            if (_productCache.TryGetByKey(store.Alias, guid, out var product) && product != null)
                return raiseEvent ? CatalogEvents.RaiseOnBeforeReturnProduct(product) : product;

            if (enableGlobal)
            {
                var globalProduct = FindProductInAnyStoreByKey(store, guid);
                return raiseEvent ? CatalogEvents.RaiseOnBeforeReturnProduct(globalProduct) : globalProduct;
            }
        }

        return null;
    }

    private IProduct? FindProductInAnyStoreById(IStore currentStore, int id)
    {
        foreach (var otherStore in _storeSvc.GetAllStores())
        {
            if (otherStore.Alias == currentStore.Alias)
                continue;

            if (_productCache.TryGetById(otherStore.Alias, id, out var product) && product != null)
                return product;
        }

        return null;
    }

    private IProduct? FindProductInAnyStoreByKey(IStore currentStore, Guid key)
    {
        foreach (var otherStore in _storeSvc.GetAllStores())
        {
            if (otherStore.Alias == currentStore.Alias)
                continue;

            if (_productCache.TryGetByKey(otherStore.Alias, key, out var product) && product != null)
                return product;
        }

        return null;
    }

    private IProduct? FindProductInAnyStoreBySku(IStore currentStore, string sku)
    {
        foreach (var otherStore in _storeSvc.GetAllStores())
        {
            if (otherStore.Alias == currentStore.Alias)
                continue;

            if (_productCache.TryGetBySku(otherStore.Alias, sku, out var product) && product != null)
                return product;
        }

        return null;
    }


    /// <summary>
    /// Get all products in store, using store from ekmRequest
    /// </summary>
    public ProductResponse GetAllProducts(ProductQuery? query = null)
    {
        IStore? store = !string.IsNullOrEmpty(query?.StoreAlias) ? _storeSvc.GetStoreByAlias(query.StoreAlias) : _storeSvc.GetStoreFromCache();

        if (store != null)
        {
            ProductResponse products = GetAllProducts(store.Alias, query);

            return products;
        }

        return new ProductResponse(Enumerable.Empty<IProduct>(), query, _productFilterService);
    }

    /// <summary>
    /// Get products by category route
    /// </summary>
    /// <returns></returns>
    public ProductResponse? GetProductsRescursiveByRoute(string route, ProductQuery? query = null)
    {
        ICategory? category = GetCategoryByRoute(route, query != null ? query.StoreAlias : null);

        if (category == null)
        {
            return new ProductResponse(Enumerable.Empty<IProduct>(), query, _productFilterService);
        }

        return category.ProductsRecursive(query);
    }

    /// <summary>
    /// Get all products from specific store
    /// </summary>
    public ProductResponse GetAllProducts(string storeAlias, ProductQuery? query = null)
    {
        if (string.IsNullOrEmpty(storeAlias))
        {
            throw new ArgumentException(nameof(storeAlias));
        }

        if (!_productCache.Cache.ContainsKey(storeAlias))
        {
            return new ProductResponse();
        }

        IOrderedEnumerable<IProduct> products = _productCache.Cache[storeAlias].Select(x => x.Value).OrderBy(x => x.SortOrder);

        return new ProductResponse(products, query, _productFilterService);
    }

    /// <summary>
    /// Get multiple products by id from store in ekmRequest
    /// </summary>
    public ProductResponse GetProductsByIds(ProductQuery? query = null)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        IStore? store = !string.IsNullOrEmpty(query?.StoreAlias) ? _storeSvc.GetStoreByAlias(query.StoreAlias) : _storeSvc.GetStoreFromCache();

        if (store != null)
        {
            return GetProductsByIds(store.Alias, query);
        }

        return new ProductResponse(Enumerable.Empty<IProduct>(), query, _productFilterService);
    }

    /// <summary>
    /// Get multiple products by id from specific store
    /// </summary>
    public ProductResponse GetProductsByIds(string storeAlias, ProductQuery? query = null)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));
        if (string.IsNullOrWhiteSpace(storeAlias)) throw new ArgumentException(nameof(storeAlias));

        var products = new List<IProduct>();

        foreach (int id in query.Ids)
        {
            if (_productCache.TryGetById(storeAlias, id, out var product) && product != null)
                products.Add(product);
        }

        return new ProductResponse(products, query, _productFilterService);
    }

    /// <summary>
    /// Get multiple products by key from store in ekmRequest
    /// </summary>
    public ProductResponse GetProductsByKeys(ProductQuery? query = null)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        IStore? store = !string.IsNullOrEmpty(query?.StoreAlias) ? _storeSvc.GetStoreByAlias(query.StoreAlias) : _storeSvc.GetStoreFromCache();

        if (store != null)
        {
            return GetProductsByKeys(store.Alias, query);
        }

        return new ProductResponse(Enumerable.Empty<IProduct>(), query, _productFilterService);
    }

    /// <summary>
    /// Get multiple products by key from specific store
    /// </summary>
    public ProductResponse GetProductsByKeys(string storeAlias, ProductQuery? query = null)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }
        if (string.IsNullOrEmpty(storeAlias))
        {
            throw new ArgumentException(nameof(storeAlias));
        }

        List<IProduct> products = new List<IProduct>();
        if (_productCache.Cache.TryGetValue(storeAlias, out var storeProducts))
        {
            foreach (Guid key in query.Keys)
            {
                if (storeProducts.TryGetValue(key, out var product))
                {
                    products.Add(product);
                }
            }
        }
        return new ProductResponse(products, query, _productFilterService);
    }

    /// <summary>
    /// Get category from ekmRequest
    /// </summary>
    /// <returns></returns>
    public ICategory? GetCategory(bool raiseEvent = true)
    {
        // Try match to ContentRequest in ekmRequest
        if (_httpContext != null &&
            _httpContext.Items != null &&
            _httpContext.Items.TryGetValue(Configuration.EkmRequestKey, out var item) &&
            item is Lazy<object> lazy &&
            lazy.Value is ContentRequest contentRequest)
        {
            var category = contentRequest.Category;

            if (category != null)
            {
                return raiseEvent ? CatalogEvents.RaiseOnBeforeReturnCategory(category) : null;
            }
        }

        return null;
    }

    /// <summary>
    /// Get category by string id, supports udi, guid and int 
    /// <param name="Id">The identifier.</param>
    /// <param name="storeAlias">The store alias.</param>
    /// <param name="global">Looks for the category in all store caches as fallback</param>
    /// <param name="raiseEvent">Control if event should be triggered</param>
    /// </summary>
    public ICategory? GetCategory(string Id, string? storeAlias = null, bool global = false, bool raiseEvent = true)
    {
        return GetSingleCategory(Id, storeAlias, global, raiseEvent: raiseEvent);
    }

    /// <summary>
    /// Gets the category by int id.
    /// </summary>
    /// <param name="Id">The identifier.</param>
    /// <param name="storeAlias">The store alias.</param>
    /// <param name="global">Looks for the category in all store caches as fallback</param>
    /// <param name="raiseEvent">Control if event should be triggered</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">storeAlias</exception>
    public ICategory? GetCategory(int Id, string? storeAlias = null, bool global = false, bool raiseEvent = true)
    {
        return GetSingleCategory(Id.ToString(), storeAlias, global, raiseEvent: raiseEvent);
    }

    /// <summary>
    /// Gets the category by guid id.
    /// </summary>
    /// <param name="Id">The identifier.</param>
    /// <param name="storeAlias">The store alias.</param>
    /// <param name="global">Looks for the category in all store caches as fallback</param>
    /// <param name="raiseEvent">Control if event should be triggered</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">storeAlias</exception>
    public ICategory? GetCategory(Guid Id, string? storeAlias = null, bool global = false, bool raiseEvent = true)
    {
        return GetSingleCategory(Id.ToString(), storeAlias, global: global, route: false, raiseEvent: raiseEvent);
    }

    /// <summary>
    /// Get category by Route
    /// </summary>
    /// <returns></returns>
    public ICategory? GetCategoryByRoute(string route, string? storeAlias = null, bool raiseEvent = true)
    {
        return GetSingleCategory(route, storeAlias, global: false, route: true, raiseEvent : raiseEvent);
    }

    private ICategory? GetSingleCategory(
    string id,
    string? storeAlias = null,
    bool global = false,
    bool route = false,
    bool raiseEvent = true)
    {
        var store = !string.IsNullOrEmpty(storeAlias)
            ? _storeSvc.GetStoreByAlias(storeAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null)
            return null;


        if (route)
        {
            if (!_categoryCache.Cache.TryGetValue(store.Alias, out var categoryDict))
                return null;

            var category = categoryDict.Values
                .FirstOrDefault(c => c.Urls.Any(url => url.Equals(id, StringComparison.OrdinalIgnoreCase)));

            return raiseEvent ? CatalogEvents.RaiseOnBeforeReturnCategory(category) : category;
        }

        // Int Id lookup
        if (int.TryParse(id, out var intId))
        {
            if (_categoryCache.TryGetById(store.Alias, intId, out var category) && category != null)
                return raiseEvent ? CatalogEvents.RaiseOnBeforeReturnCategory(category) : category;

            if (Configuration.Instance.GlobalCatalog || global)
            {
                var globalCategory = FindCategoryInAnyStoreById(store.Alias, intId, _categoryCache);
                return raiseEvent ? CatalogEvents.RaiseOnBeforeReturnCategory(globalCategory) : globalCategory;
            }

            return null;
        }

        // UDI -> Guid
        if (UtilityService.ConvertUdiToGuid(id, out var parsedGuid))
            id = parsedGuid.ToString();

        // Key lookup (O(1))
        if (Guid.TryParse(id, out var guid))
        {
            if (_categoryCache.TryGetByKey(store.Alias, guid, out var cat) && cat != null)
                return raiseEvent ? CatalogEvents.RaiseOnBeforeReturnCategory(cat) : cat;

            if (Configuration.Instance.GlobalCatalog || global)
            {
                var globalCategory = FindCategoryInAnyStoreByKey(store.Alias, guid, _categoryCache);
                return raiseEvent ? CatalogEvents.RaiseOnBeforeReturnCategory(globalCategory) : globalCategory;
            }

            return null;
        }

        return null;
    }

    private ICategory? FindCategoryInAnyStoreByKey(
        string requestingStoreAlias,
        Guid key,
        IPerStoreIndexedCache<ICategory> idx)
    {
        foreach (var otherStore in _storeSvc.GetAllStores())
        {
            if (otherStore.Alias == requestingStoreAlias)
                continue;

            // Skip if that store cache isn't present
            if (!_categoryCache.Cache.ContainsKey(otherStore.Alias))
                continue;

            if (idx.TryGetByKey(otherStore.Alias, key, out var catOther) && catOther != null)
            {
                if (IsCategoryDisabledForStore(catOther, requestingStoreAlias))
                    return null;

                return catOther;
            }
        }

        return null;
    }

    private ICategory? FindCategoryInAnyStoreById(
        string requestingStoreAlias,
        int id,
        IPerStoreIndexedCache<ICategory> idx)
    {
        foreach (var otherStore in _storeSvc.GetAllStores())
        {
            if (otherStore.Alias == requestingStoreAlias)
                continue;

            if (!_categoryCache.Cache.ContainsKey(otherStore.Alias))
                continue;

            if (idx.TryGetById(otherStore.Alias, id, out var catOther) && catOther != null)
            {
                if (IsCategoryDisabledForStore(catOther, requestingStoreAlias))
                    return null;

                return catOther;
            }
        }

        return null;
    }

    private static bool IsCategoryDisabledForStore(ICategory category, string requestingStoreAlias)
    {
        // Your existing rule
        string selfDisableField = category.GetValue("disable", requestingStoreAlias);
        return !string.IsNullOrEmpty(selfDisableField) && selfDisableField.ConvertToBool();
    }

    public IEnumerable<ICategory> GetRootCategories(string? storeAlias = null)
    {
        IStore? store = !string.IsNullOrEmpty(storeAlias)
            ? _storeSvc.GetStoreByAlias(storeAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null || !_categoryCache.Cache.TryGetValue(store.Alias, out var dict))
        {
            yield break;
        }

        var rootCategories = dict.Values
            .Where(x => x.Level == _config.CategoryRootLevel)
            .OrderBy(x => x.SortOrder);

        foreach (var category in CatalogEvents.RaiseOnBeforeReturnCategories(rootCategories))
        {
            yield return category;
        }
    }

    public IEnumerable<ICategory> GetAllCategories(string? storeAlias = null)
    {
        var store = !string.IsNullOrEmpty(storeAlias)
            ? _storeSvc.GetStoreByAlias(storeAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null || !_categoryCache.Cache.TryGetValue(store.Alias, out var dict))
        {
            yield break;
        }

        var allCategories = dict.Values
            .OrderBy(x => x.SortOrder);

        foreach (var category in CatalogEvents.RaiseOnBeforeReturnCategories(allCategories))
        {
            yield return category;
        }
    }

    /// <summary>
    /// Get multiple categories by id from store in ekmRequest (slower then GetCategoriesByKeys)
    /// </summary>
    public IEnumerable<ICategory> GetCategoriesByIds(int[] ids, string? storeAlias = null)
    {
        var store = !string.IsNullOrEmpty(storeAlias)
            ? _storeSvc.GetStoreByAlias(storeAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null)
            yield break;

        var matched = new List<ICategory>(ids.Length);

        foreach (var id in ids)
        {
            if (_categoryCache.TryGetById(store.Alias, id, out var cat) && cat != null)
                matched.Add(cat);
        }

        foreach (var category in CatalogEvents.RaiseOnBeforeReturnCategories(matched))
            yield return category;
    }


    /// <summary>
    /// Get multiple categories by key from store in ekmRequest (faster then GetCategoriesByIds)
    /// </summary>
    public IEnumerable<ICategory> GetCategoriesByKeys(Guid[] keys, string? storeAlias = null)
    {
        var store = !string.IsNullOrEmpty(storeAlias)
            ? _storeSvc.GetStoreByAlias(storeAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null || !_categoryCache.Cache.TryGetValue(store.Alias, out var categoriesInStore))
        {
            yield break;
        }

        var matchedCategories = keys
            .Select(key => categoriesInStore.TryGetValue(key, out var category) ? category : null)
            .Where(c => c != null)!;

        foreach (var category in CatalogEvents.RaiseOnBeforeReturnCategories(matchedCategories))
        {
            yield return category;
        }
    }


    public IVariant? GetVariant(Guid Id, string storeAlias = null)
    {
        IStore? store = !string.IsNullOrEmpty(storeAlias) ? _storeSvc.GetStoreByAlias(storeAlias) : _storeSvc.GetStoreFromCache();

        if (store != null)
        {
            if (_variantCache.Cache[store.Alias].TryGetValue(Id, out IVariant? val))
            {
                return val;
            }
        }

        return null;
    }

    public IVariant? GetVariant(int id, string? storeAlias = null)
    {
        IStore? store = !string.IsNullOrEmpty(storeAlias)
            ? _storeSvc.GetStoreByAlias(storeAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null)
            return null;

        return _variantCache.TryGetById(store.Alias, id, out var variant) ? variant : null;
    }

    /// <summary>
    /// Get variant by SKU
    /// </summary>
    /// <returns></returns>
    public IVariant? GetVariant(string sku, string? storeAlias = null)
    {
        IStore? store = !string.IsNullOrEmpty(storeAlias)
            ? _storeSvc.GetStoreByAlias(storeAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null)
            return null;

        if (string.IsNullOrWhiteSpace(store.Alias))
            throw new ArgumentException(nameof(store.Alias));

        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException(nameof(sku));

        return _variantCache.TryGetBySku(store.Alias, sku, out var variant) ? variant : null;
    }


    [Obsolete]
    public IVariant GetVariant(string storeAlias, Guid key)
    {
        return GetVariant(key, storeAlias);
    }

    public IEnumerable<IVariant> GetVariantsByGroup(int id, string? storeAlias = null)
    {
        var store = !string.IsNullOrEmpty(storeAlias)
            ? _storeSvc.GetStoreByAlias(storeAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null) return Enumerable.Empty<IVariant>();

        return ((VariantCache)_variantCache).GetByGroup(store.Alias, id);
    }

    [Obsolete]
    public IEnumerable<IVariant> GetVariantsByGroup(string storeAlias, int Id)
    {
        return GetVariantsByGroup(Id, storeAlias);
    }

    public IVariantGroup? GetVariantGroup(Guid key, string? storeAlias = null)
    {
        IStore? store = !string.IsNullOrEmpty(storeAlias) ? _storeSvc.GetStoreByAlias(storeAlias) : _storeSvc.GetStoreFromCache();

        if (store != null)
        {
            if (_variantGroupCache.Cache[store.Alias].TryGetValue(key, out IVariantGroup? val))
            {
                return val;
            }
        }

        return null;
    }

    [Obsolete]
    public IVariantGroup? GetVariantGroup(string storeAlias, Guid key)
    {
        return GetVariantGroup(key, storeAlias);
    }

    public IVariantGroup? GetVariantGroup(int id, string? storeAlias = null)
    {
        IStore? store = !string.IsNullOrEmpty(storeAlias)
            ? _storeSvc.GetStoreByAlias(storeAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null)
            return null;

        return _variantGroupCache.TryGetById(store.Alias, id, out var group) ? group : null;
    }


    [Obsolete]
    public IVariantGroup? GetVariantGroup(string storeAlias, int id)
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
        IProduct? product = GetProduct(productId, storeAlias);

        if (product == null)
        {
            return Enumerable.Empty<IProduct>();
        }

        return product.RelatedProducts(count);
    }

    /// <summary>
    /// Get Related Products By Sku
    /// </summary>
    public IEnumerable<IProduct> GetRelatedProductsBySku(string sku, int count = 4, string? storeAlias = null)
    {
        IProduct? product = GetProduct(sku, storeAlias);

        if (product == null)
        {
            return Enumerable.Empty<IProduct>();
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
            req.NodeTypeAlias = ["ekmProduct", "ekmVariant"];
        }

        IServiceScope scope = Configuration.Resolver.CreateScope();
        var _searhService = scope.ServiceProvider.GetService<ICatalogSearchService>();

        IEnumerable<int> result = _searhService?.ProductQuery(req, out long total) ?? Enumerable.Empty<int>();

        scope.Dispose();

        ProductQuery productQuery = new ProductQuery();

        productQuery.Ids = result.Select(x => x);
        productQuery.MetaFilters = req.MetaFilters;
        productQuery.PropertyFilters = req.PropertyFilters;
        productQuery.OrderBy = req.OrderBy;
        productQuery.StoreAlias = req.StoreAlias;

        ProductResponse products = GetProductsByIds(productQuery);

        return products;
    }

}

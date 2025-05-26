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
    readonly IPerStoreCache<IProduct> _productCache;
    readonly IPerStoreCache<ICategory> _categoryCache;
    readonly IPerStoreCache<IVariant> _variantCache;
    readonly IPerStoreCache<IVariantGroup> _variantGroupCache;
    readonly IProductFilterService _productFilterService;
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
    /// <returns></returns>
    public IProduct? GetProduct()
    {
        return GetSingleProduct("");
    }

    /// <summary>
    /// Get product by Route
    /// </summary>
    /// <returns></returns>
    public IProduct? GetProductByRoute(string route, string? storeAlias = null)
    {
        return GetSingleProduct(route, storeAlias, route: true);
    }

    /// <summary>
    /// Get product by SKU
    /// </summary>
    /// <returns></returns>
    public IProduct? GetProduct(string sku, string? storeAlias = null)
    {
        return GetSingleProduct(sku, storeAlias, sku: true);
    }

    /// <summary>
    /// Get product by Guid
    /// </summary>
    /// <returns></returns>
    public IProduct? GetProduct(Guid key, string? storeAlias = null)
    {
        return GetSingleProduct(key.ToString(), storeAlias);
    }

    /// <summary>
    /// Get product by id using store from ekmRequest
    /// </summary>
    public IProduct? GetProduct(int Id, string? storeAlias = null)
    {
        return GetSingleProduct(Id.ToString(), storeAlias);
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

    private IProduct? GetSingleProduct(string id, string? storeAlias = null, bool route = false, bool sku = false)
    {

        if (_httpContext != null &&
            _httpContext.Items != null &&
            _httpContext.Items.TryGetValue(Configuration.EkmRequestKey, out var item) &&
            item is Lazy<object> lazy &&
            lazy.Value is ContentRequest contentRequest)
        {
            var product = contentRequest.Product;

            if (product != null)
            {
                return CatalogEvents.RaiseOnBeforeReturnProduct(product);
            }
        }

        var store = !string.IsNullOrEmpty(storeAlias)
            ? _storeSvc.GetStoreByAlias(storeAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null || !_productCache.Cache.TryGetValue(store.Alias, out var productDict))
        {
            return null;
        }
        // Try match by integer ID
        if (int.TryParse(id, out int intId))
        {

            IProduct? product = productDict.FirstOrDefault(x => x.Value.Id == intId).Value;

            if (product != null)
            {
                return CatalogEvents.RaiseOnBeforeReturnProduct(product);
            }

            if (Configuration.Instance.GlobalCatalog)
            {
                return CatalogEvents.RaiseOnBeforeReturnProduct(FindProductInAnyStore(store, intId, null));
            }
        }

        // Try match by GUID key
        if (UtilityService.ConvertUdiToGuid(id, out var parsedGuid))
        {
            id = parsedGuid.ToString();
        }

        if (Guid.TryParse(id, out var guid))
        {
            if (productDict.TryGetValue(guid, out var product) && product != null)
            {
                return CatalogEvents.RaiseOnBeforeReturnProduct(product);
            }

            if (Configuration.Instance.GlobalCatalog)
            {
                return CatalogEvents.RaiseOnBeforeReturnProduct(FindProductInAnyStore(store, null, guid));
            }
        }

        // Try match by route (URL)
        if (route)
        {
            return CatalogEvents.RaiseOnBeforeReturnProduct(productDict.Values
                .FirstOrDefault(c => c.Urls.Any(url => url.Equals(id, StringComparison.OrdinalIgnoreCase))));
        }

        // Try match by SKU
        if (sku)
        {
            IProduct? product = productDict.FirstOrDefault(x => x.Value.SKU == id).Value;

            if (product != null)
            {
                return null;
            }

            if (Configuration.Instance.GlobalCatalog)
            {
                return CatalogEvents.RaiseOnBeforeReturnProduct(FindProductInAnyStore(store, null, null, sku: id));
            }
        }

        return null;
    }

    private IProduct? FindProductInAnyStore(IStore store, int? id, Guid? key, string? sku = null)
    {
        foreach (IStore otherStore in _storeSvc.GetAllStores())
        {
            if (otherStore.Alias == store.Alias)
            {
                continue;
            }

            if (id.HasValue)
            {
                return _productCache.Cache[otherStore.Alias].FirstOrDefault(x => x.Value.Id == id.Value).Value;
            }

            if (key.HasValue)
            {
                // Try to get the product from the current store in the iteration
                if (_productCache.Cache[otherStore.Alias].TryGetValue(key.Value, out IProduct? prod))
                {
                    return prod;
                }
            }

            if (!string.IsNullOrEmpty(sku))
            {
                return _productCache.Cache[otherStore.Alias].FirstOrDefault(x => x.Value.SKU == sku).Value;
            }
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
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }
        if (string.IsNullOrEmpty(storeAlias))
        {
            throw new ArgumentException(nameof(storeAlias));
        }

        List<IProduct> products = new List<IProduct>();

        foreach (int id in query.Ids)
        {
            IProduct product = _productCache.Cache[storeAlias].FirstOrDefault(x => x.Value.Id == id).Value;

            if (product != null)
            {
                products.Add(
                   product
                );
            }
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
    public ICategory? GetCategory()
    {
        return GetSingleCategory("");
    }

    /// <summary>
    /// Get category by string id, supports udi, guid and int 
    /// <param name="Id">The identifier.</param>
    /// <param name="storeAlias">The store alias.</param>
    /// <param name="global">Looks for the category in all store caches as fallback</param>
    /// </summary>
    public ICategory? GetCategory(string Id, string? storeAlias = null, bool global = false)
    {
        return GetSingleCategory(Id, storeAlias, global);
    }

    /// <summary>
    /// Gets the category by int id.
    /// </summary>
    /// <param name="Id">The identifier.</param>
    /// <param name="storeAlias">The store alias.</param>
    /// <param name="global">Looks for the category in all store caches as fallback</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">storeAlias</exception>
    public ICategory? GetCategory(int Id, string? storeAlias = null, bool global = false)
    {
        return GetSingleCategory(Id.ToString(), storeAlias, global);
    }

    /// <summary>
    /// Gets the category by guid id.
    /// </summary>
    /// <param name="Id">The identifier.</param>
    /// <param name="storeAlias">The store alias.</param>
    /// <param name="global">Looks for the category in all store caches as fallback</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">storeAlias</exception>
    public ICategory? GetCategory(Guid Id, string? storeAlias = null, bool global = false)
    {
        return GetSingleCategory(Id.ToString(), storeAlias, global);

    }

    /// <summary>
    /// Get category by Route
    /// </summary>
    /// <returns></returns>
    public ICategory? GetCategoryByRoute(string route, string? storeAlias = null)
    {
        return GetSingleCategory(route, storeAlias, global: false, route: true);
    }

    private ICategory? GetSingleCategory(string id, string? storeAlias = null, bool global = false, bool route = false)
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
                return CatalogEvents.RaiseOnBeforeReturnCategory(category);
            }
        }

        var store = !string.IsNullOrEmpty(storeAlias)
            ? _storeSvc.GetStoreByAlias(storeAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null || !_categoryCache.Cache.TryGetValue(store.Alias, out var categoryDict))
        {
            return null;
        }

        // Try match by integer ID
        if (int.TryParse(id, out int intId))
        {
            var category = categoryDict.Values.FirstOrDefault(c => c.Id == intId);
            if (category != null)
            {
                return CatalogEvents.RaiseOnBeforeReturnCategory(category);
            }

            if (Configuration.Instance.GlobalCatalog || global)
            {
                return CatalogEvents.RaiseOnBeforeReturnCategory(FindCategoryInAnyStore(store.Alias, intId, null));
            }
        }

        // Try match by GUID key
        if (UtilityService.ConvertUdiToGuid(id, out var parsedGuid))
        {
            id = parsedGuid.ToString();
        }

        if (Guid.TryParse(id, out var guid))
        {
            if (categoryDict.TryGetValue(guid, out var cat) && cat != null)
            {
                return CatalogEvents.RaiseOnBeforeReturnCategory(cat);
            }

            if (Configuration.Instance.GlobalCatalog || global)
            {
                return CatalogEvents.RaiseOnBeforeReturnCategory(FindCategoryInAnyStore(store.Alias, null, guid));
            }
        }

        // Try match by route (URL)
        if (route)
        {
            return CatalogEvents.RaiseOnBeforeReturnCategory(categoryDict.Values
                .FirstOrDefault(c => c.Urls.Any(url => url.Equals(id, StringComparison.OrdinalIgnoreCase))));
        }

        return null;
    }

    private ICategory? FindCategoryInAnyStore(string storeAlias, int? id, Guid? key)
    {
        var allStores = _storeSvc.GetAllStores();

        foreach (IStore otherStore in allStores)
        {
            if (otherStore.Alias == storeAlias)
            {
                continue;
            }

            if (!_categoryCache.Cache.ContainsKey(otherStore.Alias))
            {
                continue;
            }

            if (key.HasValue)
            {
                if (_categoryCache.Cache[otherStore.Alias].TryGetValue(key.Value, out ICategory? catOther))
                {
                    string selfDisableField = catOther.GetValue("disable", storeAlias);

                    if (!string.IsNullOrEmpty(selfDisableField) && selfDisableField.ConvertToBool())
                    {
                        return null;
                    }


                    return catOther;
                }
            }

            if (id.HasValue)
            {
                KeyValuePair<Guid, ICategory> categoryPairGlobal = _categoryCache.Cache[otherStore.Alias].FirstOrDefault(x => x.Value.Id == id.Value);

                // Check if a valid KeyValuePair was found and if the category is not null
                if (!categoryPairGlobal.Equals(default(KeyValuePair<int, ICategory>)) && categoryPairGlobal.Value != null)
                {
                    string selfDisableField = categoryPairGlobal.Value.GetValue("disable", storeAlias);

                    if (!string.IsNullOrEmpty(selfDisableField) && selfDisableField.ConvertToBool())
                    {
                        return null;
                    }

                    return categoryPairGlobal.Value;
                }
            }
        }

        return null;
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

        if (store == null || !_categoryCache.Cache.TryGetValue(store.Alias, out var dict))
        {
            yield break;
        }

        var categoriesInStore = dict.Values;

        // Resolve categories in order
        var matchedCategories = ids
            .Select(id => categoriesInStore.FirstOrDefault(c => c.Id == id))
            .Where(c => c != null)!;

        foreach (var category in CatalogEvents.RaiseOnBeforeReturnCategories(matchedCategories))
        {
            yield return category;
        }
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

    public IVariant? GetVariant(int Id, string storeAlias = null)
    {
        IStore? store = !string.IsNullOrEmpty(storeAlias) ? _storeSvc.GetStoreByAlias(storeAlias) : _storeSvc.GetStoreFromCache();

        if (store != null)
        {
            IVariant variant = _variantCache.Cache[store.Alias].FirstOrDefault(x => x.Value.Id == Id).Value;

            return variant;
        }

        return null;
    }

    /// <summary>
    /// Get variant by SKU
    /// </summary>
    /// <returns></returns>
    public IVariant? GetVariant(string sku, string storeAlias = null)
    {
        IStore? store = !string.IsNullOrEmpty(storeAlias) ? _storeSvc.GetStoreByAlias(storeAlias) : _storeSvc.GetStoreFromCache();

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
        IStore? store = !string.IsNullOrEmpty(storeAlias)
                    ? _storeSvc.GetStoreByAlias(storeAlias)
                    : _storeSvc.GetStoreFromCache();

        if (store == null)
        {
            return Enumerable.Empty<IVariant>();
        }

        if (_variantCache?.Cache.TryGetValue(store.Alias, out System.Collections.Concurrent.ConcurrentDictionary<Guid, IVariant>? variants) != true)
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

    public IVariantGroup? GetVariantGroup(Guid key, string storeAlias = null)
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

    public IVariantGroup? GetVariantGroup(int id, string storeAlias = null)
    {
        IStore? store = !string.IsNullOrEmpty(storeAlias) ? _storeSvc.GetStoreByAlias(storeAlias) : _storeSvc.GetStoreFromCache();

        if (store != null)
        {
            if (_variantGroupCache.Cache.TryGetValue(store.Alias, out System.Collections.Concurrent.ConcurrentDictionary<Guid, IVariantGroup>? variantGroups))
            {
                return variantGroups.Values.FirstOrDefault(v => v.Id == id);
            }
        }

        return null;
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

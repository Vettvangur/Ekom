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
    readonly HttpContext? _httpContext;
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
        _httpContext = httpContextAccessor?.HttpContext;
        _productFilterService = productFilterService;
    }

    /// <summary>
    /// Get current product using data from ekmRequest ContentRequest
    /// </summary>
    public IProduct? GetProduct(bool raiseEvent = true)
    {
        if (_httpContext?.Items != null
            && _httpContext.Items.TryGetValue(Configuration.EkmRequestKey, out var item)
            && item is Lazy<object> lazy
            && lazy.Value is ContentRequest contentRequest)
        {
            var product = contentRequest.Product;
            if (product != null)
                return raiseEvent ? CatalogEvents.RaiseOnBeforeReturnProduct(product) : product;
        }

        return null;
    }

    /// <summary>
    /// Get current product using data from ekmRequest ContentRequest (async)
    /// </summary>
    public async Task<IProduct?> GetProductAsync(bool raiseEvent = true, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (_httpContext?.Items != null
            && _httpContext.Items.TryGetValue(Configuration.EkmRequestKey, out var item)
            && item is Lazy<object> lazy
            && lazy.Value is ContentRequest contentRequest)
        {
            var product = contentRequest.Product;
            if (product != null)
                return raiseEvent
                    ? await CatalogEvents.RaiseOnBeforeReturnProductAsync(product, ct)
                    : product;
        }

        return null;
    }

    public IProduct? GetProductByRoute(string route, string? storeAlias = null, bool raiseEvent = true)
        => GetSingleProduct(route, storeAlias, route: true, raiseEvent: raiseEvent);

    public Task<IProduct?> GetProductByRouteAsync(string route, string? storeAlias = null, bool raiseEvent = true, CancellationToken ct = default)
        => GetSingleProductAsync(route, storeAlias, route: true, raiseEvent: raiseEvent, ct: ct);

    public IProduct? GetProduct(string sku, string? storeAlias = null, bool? global = null, bool raiseEvent = true)
        => GetSingleProduct(sku, storeAlias, sku: true, global: global, raiseEvent: raiseEvent);

    public Task<IProduct?> GetProductAsync(string sku, string? storeAlias = null, bool? global = null, bool raiseEvent = true, CancellationToken ct = default)
        => GetSingleProductAsync(sku, storeAlias, sku: true, global: global, raiseEvent: raiseEvent, ct: ct);

    public IProduct? GetProduct(Guid key, string? storeAlias = null, bool? global = null, bool raiseEvent = true)
        => GetSingleProduct(key.ToString(), storeAlias, global: global, raiseEvent: raiseEvent);

    public Task<IProduct?> GetProductAsync(Guid key, string? storeAlias = null, bool? global = null, bool raiseEvent = true, CancellationToken ct = default)
        => GetSingleProductAsync(key.ToString(), storeAlias, global: global, raiseEvent: raiseEvent, ct: ct);

    public IProduct? GetProduct(int Id, string? storeAlias = null, bool? global = null, bool raiseEvent = true)
        => GetSingleProduct(Id.ToString(), storeAlias, global: global, raiseEvent: raiseEvent);

    public Task<IProduct?> GetProductAsync(int Id, string? storeAlias = null, bool? global = null, bool raiseEvent = true, CancellationToken ct = default)
        => GetSingleProductAsync(Id.ToString(), storeAlias, global: global, raiseEvent: raiseEvent, ct: ct);

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

        if (route)
        {
            if (!_productCache.Cache.TryGetValue(store.Alias, out var productDict))
                return null;

            var product = productDict.Values
                .FirstOrDefault(p => p.Urls.Any(url => url.Equals(id, StringComparison.OrdinalIgnoreCase)));

            return raiseEvent ? CatalogEvents.RaiseOnBeforeReturnProduct(product) : product;
        }

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

        if (UtilityService.ConvertUdiToGuid(id, out var parsedGuid))
            id = parsedGuid.ToString();

        if (Guid.TryParse(id, out var guid))
        {
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

    private async Task<IProduct?> GetSingleProductAsync(
        string id,
        string? storeAlias = null,
        bool route = false,
        bool sku = false,
        bool? global = null,
        bool raiseEvent = true,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var enableGlobal = global ?? Configuration.Instance.GlobalCatalog;

        var store = !string.IsNullOrEmpty(storeAlias)
            ? _storeSvc.GetStoreByAlias(storeAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null)
            return null;

        IProduct? result;

        if (route)
        {
            if (!_productCache.Cache.TryGetValue(store.Alias, out var productDict))
                return null;

            result = productDict.Values
                .FirstOrDefault(p => p.Urls.Any(url => url.Equals(id, StringComparison.OrdinalIgnoreCase)));
        }
        else if (sku)
        {
            if (_productCache.TryGetBySku(store.Alias, id, out var product) && product != null)
                result = product;
            else if (enableGlobal)
                result = FindProductInAnyStoreBySku(store, id);
            else
                result = null;
        }
        else if (int.TryParse(id, out var intId))
        {
            if (_productCache.TryGetById(store.Alias, intId, out var product) && product != null)
                result = product;
            else if (enableGlobal)
                result = FindProductInAnyStoreById(store, intId);
            else
                result = null;
        }
        else
        {
            if (UtilityService.ConvertUdiToGuid(id, out var parsedGuid))
                id = parsedGuid.ToString();

            if (Guid.TryParse(id, out var guid))
            {
                if (_productCache.TryGetByKey(store.Alias, guid, out var product) && product != null)
                    result = product;
                else if (enableGlobal)
                    result = FindProductInAnyStoreByKey(store, guid);
                else
                    result = null;
            }
            else
            {
                result = null;
            }
        }

        ct.ThrowIfCancellationRequested();

        if (!raiseEvent)
            return result;

        return await CatalogEvents.RaiseOnBeforeReturnProductAsync(result, ct);
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

    private List<IProduct> GetAllProductsRaw(string storeAlias, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!_productCache.Cache.TryGetValue(storeAlias, out var dict) || dict == null)
            return new List<IProduct>();

        return dict.Values
            .OrderBy(x => x.SortOrder)
            .ToList();
    }

    /// <summary>
    /// Get all products in store, using store from ekmRequest
    /// </summary>
    public ProductResponse GetAllProducts(ProductQuery? query = null)
    {

        var store = !string.IsNullOrEmpty(query?.StoreAlias)
            ? _storeSvc.GetStoreByAlias(query.StoreAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null)
        {
            return new ProductResponse(
                Enumerable.Empty<IProduct>(),
                query,
                _productFilterService,
                category: null);
        }

        return GetAllProducts(store.Alias, query);
    }

    /// <summary>
    /// Get all products from specific store
    /// </summary>
    public ProductResponse GetAllProducts(string storeAlias, ProductQuery? query = null)
    {
        if (string.IsNullOrWhiteSpace(storeAlias))
            throw new ArgumentException(nameof(storeAlias));

        if (!_productCache.Cache.ContainsKey(storeAlias))
            return new ProductResponse();

        var products = GetAllProductsRaw(storeAlias);

        return new ProductResponse(
            products,
            query,
            _productFilterService,
            category: null);
    }

    /// <summary>
    /// Get all products in store, using store from ekmRequest (async).
    /// </summary>
    public async Task<ProductResponse> GetAllProductsAsync(ProductQuery? query = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var store = !string.IsNullOrEmpty(query?.StoreAlias)
            ? _storeSvc.GetStoreByAlias(query.StoreAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null)
        {
            return await ProductResponse.CreateAsync(
                Enumerable.Empty<IProduct>(),
                query,
                _productFilterService,
                category: null,
                ct: ct);
        }

        return await GetAllProductsAsync(store.Alias, query, ct);
    }

    /// <summary>
    /// Get all products from specific store (async)
    /// </summary>
    public async Task<ProductResponse> GetAllProductsAsync(string storeAlias, ProductQuery? query = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(storeAlias))
            throw new ArgumentException(nameof(storeAlias));

        // keep prior behavior: if cache has no store key => empty response
        if (!_productCache.Cache.ContainsKey(storeAlias))
            return new ProductResponse();

        var products = GetAllProductsRaw(storeAlias, ct);

        return await ProductResponse.CreateAsync(
            products,
            query,
            _productFilterService,
            category: null,
            ct: ct);
    }

    /// <summary>
    /// Get products by category route
    /// </summary>
    public ProductResponse GetProductsRescursiveByRoute(string route, ProductQuery? query = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var category = GetCategoryByRoute(route, query?.StoreAlias, raiseEvent: true);

        if (category == null)
        {
            return  new ProductResponse(
                Enumerable.Empty<IProduct>(),
                query,
                _productFilterService,
                category: null);
        }

        return category.ProductsRecursive(query);
    }

    /// <summary>
    /// Get products by category route (async).
    /// </summary>
    public async Task<ProductResponse> GetProductsRescursiveByRouteAsync(string route, ProductQuery? query = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var category = await GetCategoryByRouteAsync(route, query?.StoreAlias, raiseEvent: true, ct: ct);

        if (category == null)
        {
            return await ProductResponse.CreateAsync(
                Enumerable.Empty<IProduct>(),
                query,
                _productFilterService,
                category: null,
                ct: ct);
        }

        return await category.ProductsRecursiveAsync(query, ct);
    }

    /// <summary>
    /// Shared: load products from cache by IDs.
    /// </summary>
    private List<IProduct> GetProductsByIdsRaw(string storeAlias, ProductQuery query, CancellationToken ct)
    {
        if (query.Ids == null)
            return new List<IProduct>();

        var products = new List<IProduct>();

        foreach (var id in query.Ids)
        {
            ct.ThrowIfCancellationRequested();

            if (_productCache.TryGetById(storeAlias, id, out var product) && product != null)
                products.Add(product);
        }

        return products;
    }

    /// <summary>
    /// Get multiple products by id from store in ekmRequest
    /// </summary>
    public ProductResponse GetProductsByIds(ProductQuery? query = null)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));

        var store = !string.IsNullOrEmpty(query.StoreAlias)
            ? _storeSvc.GetStoreByAlias(query.StoreAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null)
            return new ProductResponse(Enumerable.Empty<IProduct>(), query, _productFilterService);

        return GetProductsByIds(store.Alias, query);
    }

    /// <summary>
    /// Get multiple products by id from store in ekmRequest (async).
    /// </summary>
    public async Task<ProductResponse> GetProductsByIdsAsync(ProductQuery? query = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (query == null) throw new ArgumentNullException(nameof(query));

        var store = !string.IsNullOrEmpty(query.StoreAlias)
            ? _storeSvc.GetStoreByAlias(query.StoreAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null)
        {
            return await ProductResponse.CreateAsync(
                Enumerable.Empty<IProduct>(),
                query,
                _productFilterService,
                category: null,
                ct: ct);
        }

        return await GetProductsByIdsAsync(store.Alias, query, ct);
    }

    /// <summary>
    /// Get multiple products by id from specific store
    /// </summary>
    public ProductResponse GetProductsByIds(string storeAlias, ProductQuery? query = null)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));
        if (string.IsNullOrWhiteSpace(storeAlias)) throw new ArgumentException(nameof(storeAlias));

        var products = GetProductsByIdsRaw(storeAlias, query, CancellationToken.None);
        return new ProductResponse(products, query, _productFilterService);
    }

    /// <summary>
    /// Get multiple products by id from specific store (async).
    /// </summary>
    public async Task<ProductResponse> GetProductsByIdsAsync(string storeAlias, ProductQuery? query = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (query == null) throw new ArgumentNullException(nameof(query));
        if (string.IsNullOrWhiteSpace(storeAlias)) throw new ArgumentException(nameof(storeAlias));

        var products = GetProductsByIdsRaw(storeAlias, query, ct);

        return await ProductResponse.CreateAsync(
            products,
            query,
            _productFilterService,
            category: null,
            ct: ct);
    }

    /// <summary>
    /// Shared: load products from cache by Keys.
    /// </summary>
    private List<IProduct> GetProductsByKeysRaw(string storeAlias, ProductQuery query, CancellationToken ct)
    {
        if (query.Keys == null)
            return new List<IProduct>();

        var products = new List<IProduct>();

        if (_productCache.Cache.TryGetValue(storeAlias, out var storeProducts))
        {
            foreach (Guid key in query.Keys)
            {
                ct.ThrowIfCancellationRequested();

                if (storeProducts.TryGetValue(key, out var product) && product != null)
                    products.Add(product);
            }
        }

        return products;
    }

    /// <summary>
    /// Get multiple products by key from store in ekmRequest
    /// </summary>
    public ProductResponse GetProductsByKeys(ProductQuery? query = null)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));

        var store = !string.IsNullOrEmpty(query.StoreAlias)
            ? _storeSvc.GetStoreByAlias(query.StoreAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null)
            return new ProductResponse(Enumerable.Empty<IProduct>(), query, _productFilterService);

        return GetProductsByKeys(store.Alias, query);
    }

    /// <summary>
    /// Get multiple products by key from store in ekmRequest (async).
    /// Uses ProductResponse.CreateAsync so async events + cancellation propagate.
    /// </summary>
    public async Task<ProductResponse> GetProductsByKeysAsync(ProductQuery? query = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (query == null) throw new ArgumentNullException(nameof(query));

        var store = !string.IsNullOrEmpty(query.StoreAlias)
            ? _storeSvc.GetStoreByAlias(query.StoreAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null)
        {
            return await ProductResponse.CreateAsync(
                Enumerable.Empty<IProduct>(),
                query,
                _productFilterService,
                category: null,
                ct: ct);
        }

        return await GetProductsByKeysAsync(store.Alias, query, ct);
    }

    /// <summary>
    /// Get multiple products by key from specific store
    /// </summary>
    public ProductResponse GetProductsByKeys(string storeAlias, ProductQuery? query = null)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));
        if (string.IsNullOrWhiteSpace(storeAlias)) throw new ArgumentException(nameof(storeAlias));

        var products = GetProductsByKeysRaw(storeAlias, query, CancellationToken.None);
        return new ProductResponse(products, query, _productFilterService);
    }

    /// <summary>
    /// Get multiple products by key from specific store (async).
    /// </summary>
    public async Task<ProductResponse> GetProductsByKeysAsync(string storeAlias, ProductQuery? query = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (query == null) throw new ArgumentNullException(nameof(query));
        if (string.IsNullOrWhiteSpace(storeAlias)) throw new ArgumentException(nameof(storeAlias));

        var products = GetProductsByKeysRaw(storeAlias, query, ct);

        return await ProductResponse.CreateAsync(
            products,
            query,
            _productFilterService,
            category: null,
            ct: ct);
    }

    /// <summary>
    /// Get category from ekmRequest
    /// </summary>
    public ICategory? GetCategory(bool raiseEvent = true)
    {
        if (_httpContext?.Items != null
            && _httpContext.Items.TryGetValue(Configuration.EkmRequestKey, out var item)
            && item is Lazy<object> lazy
            && lazy.Value is ContentRequest contentRequest)
        {
            var category = contentRequest.Category;

            if (category != null)
                return raiseEvent ? CatalogEvents.RaiseOnBeforeReturnCategory(category) : category;
        }

        return null;
    }

    /// <summary>
    /// Get category from ekmRequest (async)
    /// </summary>
    public async Task<ICategory?> GetCategoryAsync(bool raiseEvent = true, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (_httpContext?.Items != null
            && _httpContext.Items.TryGetValue(Configuration.EkmRequestKey, out var item)
            && item is Lazy<object> lazy
            && lazy.Value is ContentRequest contentRequest)
        {
            var category = contentRequest.Category;

            if (category != null)
                return raiseEvent
                    ? await CatalogEvents.RaiseOnBeforeReturnCategoryAsync(category, ct)
                    : category;
        }

        return null;
    }

    /// <summary>
    /// Get category by string id, supports udi, guid and int 
    /// </summary>
    public ICategory? GetCategory(string Id, string? storeAlias = null, bool global = false, bool raiseEvent = true)
        => GetSingleCategory(Id, storeAlias, global, route: false, raiseEvent: raiseEvent);

    public Task<ICategory?> GetCategoryAsync(string Id, string? storeAlias = null, bool global = false, bool raiseEvent = true, CancellationToken ct = default)
        => GetSingleCategoryAsync(Id, storeAlias, global, route: false, raiseEvent: raiseEvent, ct: ct);

    public ICategory? GetCategory(int Id, string? storeAlias = null, bool global = false, bool raiseEvent = true)
        => GetSingleCategory(Id.ToString(), storeAlias, global, route: false, raiseEvent: raiseEvent);

    public Task<ICategory?> GetCategoryAsync(int Id, string? storeAlias = null, bool global = false, bool raiseEvent = true, CancellationToken ct = default)
        => GetSingleCategoryAsync(Id.ToString(), storeAlias, global, route: false, raiseEvent: raiseEvent, ct: ct);

    public ICategory? GetCategory(Guid Id, string? storeAlias = null, bool global = false, bool raiseEvent = true)
        => GetSingleCategory(Id.ToString(), storeAlias, global: global, route: false, raiseEvent: raiseEvent);

    public Task<ICategory?> GetCategoryAsync(Guid Id, string? storeAlias = null, bool global = false, bool raiseEvent = true, CancellationToken ct = default)
        => GetSingleCategoryAsync(Id.ToString(), storeAlias, global: global, route: false, raiseEvent: raiseEvent, ct: ct);

    public ICategory? GetCategoryByRoute(string route, string? storeAlias = null, bool raiseEvent = true)
        => GetSingleCategory(route, storeAlias, global: false, route: true, raiseEvent: raiseEvent);

    public Task<ICategory?> GetCategoryByRouteAsync(string route, string? storeAlias = null, bool raiseEvent = true, CancellationToken ct = default)
        => GetSingleCategoryAsync(route, storeAlias, global: false, route: true, raiseEvent: raiseEvent, ct: ct);

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

    private async Task<ICategory?> GetSingleCategoryAsync(
        string id,
        string? storeAlias = null,
        bool global = false,
        bool route = false,
        bool raiseEvent = true,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var store = !string.IsNullOrEmpty(storeAlias)
            ? _storeSvc.GetStoreByAlias(storeAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null)
            return null;

        ICategory? result;

        if (route)
        {
            if (!_categoryCache.Cache.TryGetValue(store.Alias, out var categoryDict))
                return null;

            result = categoryDict.Values
                .FirstOrDefault(c => c.Urls.Any(url => url.Equals(id, StringComparison.OrdinalIgnoreCase)));
        }
        else if (int.TryParse(id, out var intId))
        {
            if (_categoryCache.TryGetById(store.Alias, intId, out var category) && category != null)
            {
                result = category;
            }
            else if (Configuration.Instance.GlobalCatalog || global)
            {
                result = FindCategoryInAnyStoreById(store.Alias, intId, _categoryCache);
            }
            else
            {
                result = null;
            }
        }
        else
        {
            // UDI -> Guid
            if (UtilityService.ConvertUdiToGuid(id, out var parsedGuid))
                id = parsedGuid.ToString();

            if (Guid.TryParse(id, out var guid))
            {
                if (_categoryCache.TryGetByKey(store.Alias, guid, out var cat) && cat != null)
                {
                    result = cat;
                }
                else if (Configuration.Instance.GlobalCatalog || global)
                {
                    result = FindCategoryInAnyStoreByKey(store.Alias, guid, _categoryCache);
                }
                else
                {
                    result = null;
                }
            }
            else
            {
                result = null;
            }
        }

        ct.ThrowIfCancellationRequested();

        if (!raiseEvent)
            return result;

        return await CatalogEvents.RaiseOnBeforeReturnCategoryAsync(result, ct);
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

    public async Task<IReadOnlyList<ICategory>> GetRootCategoriesAsync(string? storeAlias = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var store = !string.IsNullOrEmpty(storeAlias)
            ? _storeSvc.GetStoreByAlias(storeAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null || !_categoryCache.Cache.TryGetValue(store.Alias, out var dict))
            return Array.Empty<ICategory>();

        // materialize once (stable ordering + avoids multiple enumeration)
        var rootCategories = dict.Values
            .Where(x => x.Level == _config.CategoryRootLevel)
            .OrderBy(x => x.SortOrder)
            .ToList();

        ct.ThrowIfCancellationRequested();

        var transformed = await CatalogEvents.RaiseOnBeforeReturnCategoriesAsync(rootCategories, ct);

        // keep a stable concrete result
        return transformed as IReadOnlyList<ICategory> ?? transformed.ToList();
    }

    public async Task<IReadOnlyList<ICategory>> GetAllCategoriesAsync(string? storeAlias = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var store = !string.IsNullOrEmpty(storeAlias)
            ? _storeSvc.GetStoreByAlias(storeAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null || !_categoryCache.Cache.TryGetValue(store.Alias, out var dict))
            return Array.Empty<ICategory>();

        var allCategories = dict.Values
            .OrderBy(x => x.SortOrder)
            .ToList();

        ct.ThrowIfCancellationRequested();

        var transformed = await CatalogEvents.RaiseOnBeforeReturnCategoriesAsync(allCategories, ct);
        return transformed as IReadOnlyList<ICategory> ?? transformed.ToList();
    }

    /// <summary>
    /// Get multiple categories by id (async)
    /// </summary>
    public async Task<IReadOnlyList<ICategory>> GetCategoriesByIdsAsync(int[] ids, string? storeAlias = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (ids == null || ids.Length == 0) return Array.Empty<ICategory>();

        var store = !string.IsNullOrEmpty(storeAlias)
            ? _storeSvc.GetStoreByAlias(storeAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null)
            return Array.Empty<ICategory>();

        var matched = new List<ICategory>(ids.Length);

        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();

            if (_categoryCache.TryGetById(store.Alias, id, out var cat) && cat != null)
                matched.Add(cat);
        }

        ct.ThrowIfCancellationRequested();

        var transformed = await CatalogEvents.RaiseOnBeforeReturnCategoriesAsync(matched, ct);
        return transformed as IReadOnlyList<ICategory> ?? transformed.ToList();
    }

    /// <summary>
    /// Get multiple categories by key (async)
    /// </summary>
    public async Task<IReadOnlyList<ICategory>> GetCategoriesByKeysAsync(Guid[] keys, string? storeAlias = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (keys == null || keys.Length == 0) return Array.Empty<ICategory>();

        var store = !string.IsNullOrEmpty(storeAlias)
            ? _storeSvc.GetStoreByAlias(storeAlias)
            : _storeSvc.GetStoreFromCache();

        if (store == null || !_categoryCache.Cache.TryGetValue(store.Alias, out var categoriesInStore))
            return Array.Empty<ICategory>();

        var matched = new List<ICategory>(keys.Length);

        foreach (var key in keys)
        {
            ct.ThrowIfCancellationRequested();

            if (categoriesInStore.TryGetValue(key, out var category) && category != null)
                matched.Add(category);
        }

        ct.ThrowIfCancellationRequested();

        var transformed = await CatalogEvents.RaiseOnBeforeReturnCategoriesAsync(matched, ct);
        return transformed as IReadOnlyList<ICategory> ?? transformed.ToList();
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
    /// Get Related Products
    /// </summary>
    public async Task<IReadOnlyList<IProduct>> GetRelatedProductsAsync(Guid productId, int count = 4, string? storeAlias = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var product = await GetProductAsync(productId, storeAlias, global: null, raiseEvent: true, ct: ct);

        if (product == null)
            return Array.Empty<IProduct>();

        return await product.RelatedProductsAsync(count, ct);
    }

    /// <summary>
    /// Get Related Products By Sku
    /// </summary>
    public async Task<IReadOnlyList<IProduct>> GetRelatedProductsBySkuAsync(string sku, int count = 4, string? storeAlias = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var product = await GetProductAsync(sku, storeAlias, global: null, raiseEvent: true, ct: ct);

        if (product == null)
            return Array.Empty<IProduct>();

        return await product.RelatedProductsAsync(count, ct);
    }

    /// <summary>
    /// Search Products
    /// </summary>
    public async Task<ProductResponse> ProductSearchAsync(SearchRequest req, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(req?.SearchQuery))
            return new ProductResponse();

        if (req.NodeTypeAlias == null || !req.NodeTypeAlias.Any())
            req.NodeTypeAlias = ["ekmProduct", "ekmVariant"];

        using IServiceScope scope = Configuration.Resolver.CreateScope();
        var _searhService = scope.ServiceProvider.GetService<ICatalogSearchService>();

        var (ids, total) = _searhService == null
            ? (Enumerable.Empty<int>(), 0L)
            : await _searhService.ProductQueryAsync(req, ct);

        ct.ThrowIfCancellationRequested();

        var productQuery = new ProductQuery
        {
            Ids = ids.ToList(),
            MetaFilters = req.MetaFilters,
            PropertyFilters = req.PropertyFilters,
            OrderBy = req.OrderBy,
            StoreAlias = req.StoreAlias
        };

        return await GetProductsByIdsAsync(productQuery, ct);
    }
}

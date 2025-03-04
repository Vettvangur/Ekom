using Ekom.Cache;
using Ekom.Exceptions;
using Ekom.Models;
using Microsoft.AspNetCore.Http;

namespace Ekom.Services;

class StoreService : IStoreService
{
    private readonly IStoreDomainCache _domainCache;
    private readonly IBaseCache<IStore> _storeCache;
    private readonly HttpContext _httpContext;

    /// <summary>
    /// ctor
    /// </summary>
    public StoreService(
        IStoreDomainCache domainCache,
        IBaseCache<IStore> storeCache,
        IHttpContextAccessor httpContextAccessor)
    {
        _domainCache = domainCache;
        _storeCache = storeCache;
        _httpContext = httpContextAccessor.HttpContext;
    }

    public IStore? GetStoreByDomain(string domain = "", string culture = "")
    {
        IStore? store = null;

        if (!string.IsNullOrEmpty(domain))
        {
            domain = domain.TrimEnd('/');

            UmbracoDomain storeDomain
                = _domainCache.Cache
                                  .FirstOrDefault
                                      (x => domain.Equals(x.Value.DomainName, StringComparison.InvariantCultureIgnoreCase))
                                  .Value;

            if (storeDomain != null)
            {
                store = _storeCache.Cache
                                  .FirstOrDefault
                                    (x => x.Value.StoreRootNodeId == storeDomain.RootContentId && x.Value.Culture.Name == culture)
                                  .Value;
            }
        }

        // If no store found by domain or domain is empty, return the first store.
        store ??= GetAllStores().FirstOrDefault();

        return store ?? throw new Exception("No store found in cache.");
    }

    public IStore? GetStoreByAlias(string alias)
    {
        if (!_storeCache.Cache.Any())
        {
            throw new StoreNotFoundException("Unable to find any stores!");
        }

        if (!_storeCache.Cache.Any(x => string.Equals(alias, x.Value.Alias, StringComparison.InvariantCultureIgnoreCase)))
        {
            throw new StoreNotFoundException($"Unable to find any store: {alias}");
        }

        IStore store = _storeCache.Cache
                         .FirstOrDefault(x => string.Equals(alias, x.Value.Alias, StringComparison.InvariantCultureIgnoreCase))
                         .Value;

        // If store is not found by alias then return first store
        return store ?? _storeCache.Cache.FirstOrDefault().Value
            ?? throw new StoreNotFoundException("Unable to find any stores!");
    }

    public IStore? GetStoreFromCache()
    {

        if (_httpContext != null && _httpContext.Items != null && _httpContext.Items.TryGetValue(Configuration.EkmRequestKey, out object? ekmRequestObject))
        {
            ContentRequest? contentRequest = null;

            // Check for Lazy<ContentRequest>
            if (ekmRequestObject is Lazy<ContentRequest> lazyContentRequest)
            {
                contentRequest = lazyContentRequest.Value;
            }
            // Check for Lazy<object> and cast to ContentRequest
            else if (ekmRequestObject is Lazy<object> lazyObject && lazyObject.Value is ContentRequest contentRequestFromObject)
            {
                contentRequest = contentRequestFromObject;
            }

            if (contentRequest != null && contentRequest.Store != null)
            {
                // Use contentRequest as needed
                return contentRequest.Store;
            }
        }

        return GetAllStores().FirstOrDefault();
    }

    public IStore? SetStore(string storeAlias)
    {
        if (string.IsNullOrEmpty(storeAlias))
        {
            return null;
        }

        // Retrieve the store by its alias
        IStore? store = GetStoreByAlias(storeAlias);

        // If no store is found, return null
        if (store == null)
        {
            return null;
        }

        if (_httpContext != null)
        {
            if (_httpContext.Items.TryGetValue(Configuration.EkmRequestKey, out object? ekmRequestObject) &&
                ekmRequestObject is Lazy<ContentRequest> lazyRequest)
            {
                // Access Lazy.Value to ensure it initializes
                ContentRequest contentRequest = lazyRequest.Value;
                if (contentRequest != null)
                {
                    contentRequest.Store = store;
                }
            }
            else
            {
                // Create and initialize a new Lazy<ContentRequest>
                Lazy<ContentRequest> newContentRequest = new Lazy<ContentRequest>(() =>
                {
                    ContentRequest request = new ContentRequest();
                    request.Store = store;
                    return request;
                });

                // Add to HttpContext.Items
                _httpContext.Items[Configuration.EkmRequestKey] = newContentRequest;

                // Access Lazy.Value to ensure initialization
                _ = newContentRequest.Value;
            }
        }

        // Return the store
        return store;
    }

    public IEnumerable<IStore> GetAllStores()
    {
        return _storeCache.Cache.Select(x => x.Value).OrderBy(x => x.SortOrder);
    }

    public IEnumerable<UmbracoDomain> GetDomains()
    {
        return _domainCache.Cache.Select(x => x.Value);
    }
}

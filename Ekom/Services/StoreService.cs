using Ekom.API;
using Ekom.Cache;
using Ekom.Exceptions;
using Ekom.Models;
using Microsoft.AspNetCore.Http;

namespace Ekom.Services
{
    class StoreService : IStoreService
    {
        private readonly IStoreDomainCache _domainCache;
        private readonly IBaseCache<IStore> _storeCache;
        private readonly HttpContext _httpContext;

        private readonly string EkmRequestKey = "umbrtmche-ekmRequest";
        
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

        public IStore GetStoreByDomain(string domain = "", string culture = "")
        {
            IStore store = null;

            if (!string.IsNullOrEmpty(domain))
            {
                domain = domain.TrimEnd('/');

                var storeDomain
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

        public IStore GetStoreByAlias(string alias)
        {
            var store = _storeCache.Cache
                             .FirstOrDefault(x => string.Equals(alias, x.Value.Alias, StringComparison.InvariantCultureIgnoreCase))
                             .Value;

            // If store is not found by alias then return first store
            return store ?? _storeCache.Cache.FirstOrDefault().Value
                ?? throw new StoreNotFoundException("Unable to find any stores!");
        }

        public IStore GetStoreFromCache()
        {
            if (_httpContext.Items.ContainsKey(EkmRequestKey))
            {
                var r = _httpContext.Items[EkmRequestKey] as Lazy<object>;
                var contentRequest = r?.Value as ContentRequest;

                return contentRequest?.Store ?? GetAllStores().FirstOrDefault();
            }

            return GetAllStores().FirstOrDefault();
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
}

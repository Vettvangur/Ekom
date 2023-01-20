#if NETFRAMEWORK
using System.Web;
#else
using Microsoft.AspNetCore.Http;
#endif

using Ekom.Cache;
using Ekom.Exceptions;
using Ekom.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ekom.Services
{
    class StoreService : IStoreService
    {
        readonly IStoreDomainCache _domainCache;
        readonly IBaseCache<IStore> _storeCache;
        readonly HttpContext _httpContext;
        /// <summary>
        /// ctor
        /// </summary>
        public StoreService(
            IStoreDomainCache domainCache,
            IBaseCache<IStore> storeCache
#if NETCOREAPP
            ,IHttpContextAccessor httpContextAccessor
#endif
        )
        {
            _domainCache = domainCache;
            _storeCache = storeCache;
#if NETFRAMEWORK
            _httpContext = HttpContext.Current;
#else
            _httpContext = httpContextAccessor.HttpContext;
#endif
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

            if (store == null)
            {
                store = GetAllStores().FirstOrDefault();
            }

            if (store == null)
            {
                throw new Exception("No store found in cache.");
            }

            return store;
        }

        public IStore GetStoreByAlias(string alias)
        {
            var store = _storeCache.Cache
                             .FirstOrDefault(x => x.Value.Alias.Equals(alias, StringComparison.InvariantCultureIgnoreCase))
                             .Value;
            // If store is not found by alias then return first store
            return store ?? _storeCache.Cache.FirstOrDefault().Value
                ?? throw new StoreNotFoundException("Unable to find any stores!");
        }

        public IStore GetStoreFromCache()
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
            }

            return contentRequest?.Store ?? GetAllStores().FirstOrDefault();
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

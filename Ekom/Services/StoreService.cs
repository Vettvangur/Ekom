using Ekom.Cache;
using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;

namespace Ekom.Services
{
    class StoreService : IStoreService
    {
        readonly ILogger _logger;
        readonly IAppCache _reqCache;
        readonly IStoreDomainCache _domainCache;
        readonly IBaseCache<IStore> _storeCache;

        /// <summary>
        /// ctor
        /// </summary>
        public StoreService(
            ILogger logger,
            IStoreDomainCache domainCache,
            IBaseCache<IStore> storeCache,
            AppCaches appCaches
        )
        {
            _logger = logger;
            _reqCache = appCaches.RequestCache;
            _domainCache = domainCache;
            _storeCache = storeCache;
        }

        public IStore GetStoreByDomain(string domain = "")
        {
            IStore store = null;

            if (!string.IsNullOrEmpty(domain))
            {
                domain = domain.TrimEnd("/");

                var storeDomain
                    = _domainCache.Cache
                                      .FirstOrDefault
                                          (x => domain.Equals(x.Value.DomainName, System.StringComparison.InvariantCultureIgnoreCase))
                                      .Value;

                if (storeDomain != null)
                {
                    store = _storeCache.Cache
                                      .FirstOrDefault
                                        (x => x.Value.StoreRootNode == storeDomain.RootContentId)
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
                             .FirstOrDefault(x => x.Value.Alias.InvariantEquals(alias))
                             .Value;
            // If store is not found by alias then return first store
            return store ?? _storeCache.Cache.FirstOrDefault().Value
                ?? throw new StoreNotFoundException("Unable to find any stores!");
        }

        //private Store GetStoreByAlias(string alias, IDomain umbracoDomain)
        //{
        //    var store = _storeCache.Cache
        //                     .FirstOrDefault(x => x.Value.Alias.InvariantEquals(alias) && x.Value.Domains.Any(z => z.Id == umbracoDomain.Id))
        //                     .Value;

        //    if (store == null)
        //    {
        //        store = GetStoreByDomain(umbracoDomain.DomainName);

        //        // If store found in another domain then reset the cookie
        //        if (store != null)
        //        {
        //            UmbracoContext.Current.HttpContext.Response.Cookies["StoreInfo"].Values["StoreAlias"] = store.Alias;
        //        }
        //    }

        //    // If store is not found by alias then return first store
        //    return store ?? _storeCache.Cache.FirstOrDefault().Value;
        //}

        public IStore GetStoreFromCache()
        {
            var r = _reqCache.GetCacheItem<ContentRequest>("ekmRequest");

            return r?.Store ?? GetAllStores().FirstOrDefault();
        }

        public IEnumerable<IStore> GetAllStores()
        {
            return _storeCache.Cache.Select(x => x.Value).OrderBy(x => x.SortOrder);
        }

        ///// <summary>
        ///// Gets the current store from available request data <para/>
        ///// Saves store in cache and cookies
        ///// </summary>
        ///// <param name="umbracoDomain"></param>
        ///// <param name="httpContext"></param>
        //public Store GetStore(IDomain umbracoDomain, HttpContextBase httpContext)
        //{
        //    //HttpCookie storeInfo = httpContext.Request.Cookies["StoreInfo"];

        //    //_log.Info("debug store: " + (storeInfo != null ? storeInfo.Value : ""));

        //    //string storeAlias = storeInfo?.Values["StoreAlias"];

        //    //_log.Info("debug2 store: " + storeAlias);

        //    _log.Info("GetStore: " + umbracoDomain.DomainName);

        //    Store store = GetStoreByDomain(umbracoDomain.DomainName);

        //    //_log.Info("GetStore store: " + store.alias);

        //    //// Attempt to retrieve Store from cookie data
        //    //if (!string.IsNullOrEmpty(storeAlias))
        //    //{
        //    //    store = GetStoreByAlias(storeAlias, umbracoDomain);
        //    //}

        //    // Get by Alias and Domain. If by alias is not found on the domain then search by domain
        //    //if (store == null && umbracoDomain != null)
        //    //{
        //    //    _log.Info("debug3 store: Get Store By Domain: " + umbracoDomain.DomainName);
        //    //    store = GetStoreByDomain(umbracoDomain.DomainName);
        //    //}

        //    // Grab default store / First store from cache if no umbracoDomain present
        //    if (store == null)
        //    {
        //        store = GetStoreByDomain();
        //    }

        //    return store;
        //}

    }
}

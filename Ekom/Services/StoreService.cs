using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Models;
using Umbraco.Web;
using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models;

namespace Ekom.Services
{
    class StoreService : IStoreService
    {
        ILog _log;
        ApplicationContext _appCtx;
        ICacheProvider _reqCache => _appCtx.ApplicationCache.RequestCache;
        IBaseCache<IDomain> _domainCache;
        IBaseCache<Store> _storeCache;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="logFac"></param>
        /// <param name="domainCache"></param>
        /// <param name="storeCache"></param>
        /// <param name="appCtx"></param>
        public StoreService(
            ILogFactory logFac,
            IBaseCache<IDomain> domainCache,
            IBaseCache<Store> storeCache,
            ApplicationContext appCtx
        )
        {
            _log = logFac.GetLogger(typeof(StoreService));
            _appCtx = appCtx;
            _domainCache = domainCache;
            _storeCache = storeCache;
        }

        public Store GetStoreByDomain(string domain = "")
        {
            Store store = null;

            if (!string.IsNullOrEmpty(domain))
            {
                var storeDomain
                    = _domainCache.Cache
                                      .FirstOrDefault
                                          (x => x.Value.DomainName.Equals(domain.TrimEnd("/"), StringComparison.InvariantCultureIgnoreCase))
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
                store = _storeCache.Cache.FirstOrDefault().Value;
            }

            return store;
        }

        public Store GetStoreByAlias(string alias)
        {
            var store = _storeCache.Cache
                             .FirstOrDefault(x => x.Value.Alias.InvariantEquals(alias))
                             .Value;
            // If store is not found by alias then return first store
            return store ?? _storeCache.Cache.FirstOrDefault().Value;
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

        public Store GetStoreFromCache()
        {
            var r = _reqCache.GetCacheItem("ekmRequest") as ContentRequest;

            if (r == null || r.Store == null)
            {
                return GetAllStores().FirstOrDefault();
            }

            return r?.Store;
        }

        public IEnumerable<Store> GetAllStores()
        {
            return _storeCache.Cache.Select(x => x.Value).OrderBy(x => x.SortOrder);
        }

        /// <summary>
        /// Gets the current store from available request data <para/>
        /// Saves store in cache and cookies
        /// </summary>
        /// <param name="umbracoDomain"></param>
        /// <param name="httpContext"></param>
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

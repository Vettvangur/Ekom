using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using umbraco.cms.businesslogic.web;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Web;
using uWebshop.Cache;
using uWebshop.Models;

namespace uWebshop.Services
{
    public static class StoreService
    {
        private static readonly ILog Log =
                LogManager.GetLogger(
                    MethodBase.GetCurrentMethod().DeclaringType
                );

        public static IEnumerable<IDomain> GetAllStoreDomains()
        {
            var ds = ApplicationContext.Current.Services.DomainService;

            var domains = ds.GetAll(true).Where(x => !x.DomainName.Contains("*"));

            return domains;

        }

        public static Store GetStoreByDomain(string domain = "")
        {
            Store store = null;

            if (!string.IsNullOrEmpty(domain))
            {
                var storeDomain
                    = StoreDomainCache.Cache
                                      .FirstOrDefault
                                          (x => x.Value.DomainName.Equals(domain, StringComparison.InvariantCultureIgnoreCase))
                                      .Value;

                if (storeDomain != null)
                {
                    store = StoreCache.Cache
                                      .FirstOrDefault
                                        (x => x.Value.StoreRootNode == storeDomain.RootContentId)
                                      .Value;
                }
            }

            if (store == null)
            {
                store = StoreCache.Cache.FirstOrDefault().Value;
            }

            return store;
        }

        public static Store GetStoreByAlias(string alias)
        {
            return StoreCache.Cache
                             .FirstOrDefault(x => x.Value.Alias == alias)
                             .Value;
        }

        public static Store GetStoreFromCache()
        {
            var appCache = ApplicationContext.Current.ApplicationCache;

            var r = appCache.RequestCache.GetCacheItem("uwbsRequest") as ContentRequest;

            if (r != null)
            {
                return r.Store;
            }

            return null;
        }

        /// <summary>
        /// Gets the current store from available request data <para/>
        /// Saves store in cache and cookies
        /// </summary>
        /// <param name="umbracoDomain"></param>
        /// <param name="httpContext"></param>
        /// <returns></returns>
        public static Store GetStore(IDomain umbracoDomain, HttpContextBase httpContext)
        {
            HttpCookie storeInfo = httpContext.Request.Cookies["StoreInfo"];
            string storeAlias    = storeInfo != null ? storeInfo.Values["StoreAlias"] : null;

            Store store = null;

            // Attempt to retrieve Store from cookie data
            if (!string.IsNullOrEmpty(storeAlias))
            {
                store = GetStoreByAlias(storeAlias);
            }

            // Get by Domain
            if (store == null && umbracoDomain != null)
            {
                store = GetStoreByDomain(umbracoDomain.DomainName);
            }

            // Grab default store / First store from cache if no umbracoDomain present
            if (store == null)
            {
                store = GetStoreByDomain();
            }

            return store;
        }
    }
}

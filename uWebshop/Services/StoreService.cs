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

        public static Store GetStore()
        {
            var appCache = ApplicationContext.Current.ApplicationCache;

            var r = appCache.RequestCache.GetCacheItem("uwbsRequest") as ContentRequest;

            if (r != null)
            {
                return r.Store;
            }

            if (UmbracoContext.Current != null && UmbracoContext.Current.PublishedContentRequest != null)
            {
                return GetStoreByDomain(UmbracoContext.Current.PublishedContentRequest.UmbracoDomain.DomainName);
            }

            return null;
        }
    }
}

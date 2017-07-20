using Microsoft.Practices.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using uWebshop.App_Start;
using uWebshop.Cache;
using uWebshop.Models;

namespace uWebshop.API
{
    public class Store
    {
        private static IBaseCache<Models.Store> _storeCache
        {
            get
            {
                return UnityConfig.GetConfiguredContainer().Resolve<IBaseCache<Models.Store>>();
            }
        }

        public static Models.Store GetStore()
        {
            var appCache = ApplicationContext.Current.ApplicationCache;
            var r = appCache.RequestCache.GetCacheItem("uwbsRequest") as ContentRequest;

            if (r?.Store != null)
            {
                return r.Store;
            }

            return null;
        }

        public static Models.Store GetStore(string storeAlias)
        {
            return _storeCache.Cache.FirstOrDefault(x => x.Value.Alias == storeAlias).Value;
        }

        public static IEnumerable<Models.Store> GetAllStores()
        {
            return _storeCache.Cache.Select(x => x.Value).OrderBy(x => x.Level);
        }
    }
}

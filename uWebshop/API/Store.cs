using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using uWebshop.Cache;
using uWebshop.Models;

namespace uWebshop.API
{
    public static class Store
    {
        public static uWebshop.Models.Store GetStore()
        {
            var appCache = ApplicationContext.Current.ApplicationCache;
            var r = appCache.RequestCache.GetCacheItem("uwbsRequest") as ContentRequest;

            if (r.Store != null)
            {
                return r.Store;
            }

            return null;
        }

        public static uWebshop.Models.Store GetStore(string storeAlias)
        {
            return StoreCache.Cache.FirstOrDefault(x => x.Value.Alias == storeAlias).Value;
        }

        public static IEnumerable<uWebshop.Models.Store> GetAllStores()
        {
            return StoreCache.Cache.Select(x => x.Value).OrderBy(x => x.Level);
        }
    }
}

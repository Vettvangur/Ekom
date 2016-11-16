using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using uWebshop.Cache;
using uWebshop.Models;

namespace uWebshop.API
{
    public static class Store
    {
        public static uWebshop.Models.Store GetStore()
        {
            var r = (ContentRequest)HttpContext.Current.Cache["uwbsRequest"];

            if (r.Store != null)
            {
                return r.Store;
            }

            return null;
        }

        public static uWebshop.Models.Store GetStore(string storeAlias)
        {
            var store = StoreCache._storeCache.FirstOrDefault(x => x.Value.Alias == storeAlias).Value;

            return store;
        }

        public static IEnumerable<uWebshop.Models.Store> GetAllStores()
        {
            var stores = StoreCache._storeCache.OrderBy(x => x.Value.Level).Select(x => x.Value);

            return stores;
        }
    }
}

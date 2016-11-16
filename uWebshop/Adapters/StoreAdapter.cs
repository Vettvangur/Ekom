using Examine;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Cache;
using uWebshop.Models;

namespace uWebshop.Adapters
{
    public static class StoreAdapter
    {
        private static readonly ILog Log =
                LogManager.GetLogger(
                    MethodBase.GetCurrentMethod().DeclaringType
                );

        public static Store CreateStoreItemFromExamine(SearchResult item)
        {
            
            try
            {
                var store = new Store();

                store.Id = item.Id;
                store.Alias = item.Fields["nodeName"];
                store.StoreRootNode = Convert.ToInt32(item.Fields["storeRootNode"]);
                store.Level = Convert.ToInt32(item.Fields["level"]);
                //store.RootNode = StoreCache._storeNodeCache.FirstOrDefault(x => x.Value.StoreId == item.Id).Value;
                store.Domains = StoreCache._storeDomainCache.Where(x => x.Value.RootContentId == store.StoreRootNode).Select(x => x.Value);

                return store;
            }
            catch (Exception ex)
            {
                Log.Error("Error on creating store item from Examine. Node id: " + item.Id, ex);
                return null;
            }
        }
    }
}

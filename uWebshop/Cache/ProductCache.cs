using Examine;
using Examine.SearchCriteria;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Adapters;
using uWebshop.Models;

namespace uWebshop.Cache
{
    public static class ProductCache
    {

        private static readonly ILog Log =
                LogManager.GetLogger(
                    MethodBase.GetCurrentMethod().DeclaringType
                );

        /// <summary>
        /// Concurrent dictionary cache containing product models
        /// </summary>
        public static ConcurrentDictionary<string, Product> _productCache = new ConcurrentDictionary<string, Product>();

        /// <summary>
        /// Fill category cache with all products in examine
        /// </summary>
        public static void FillCache()
        {

            var searcher = ExamineManager.Instance.SearchProviderCollection["ExternalSearcher"];

            if (searcher != null)
            {

                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                Log.Info("Starting to fill product cache...");

                ISearchCriteria searchCriteria = searcher.CreateSearchCriteria();
                var query = searchCriteria.NodeTypeAlias("uwbsProduct");
                var results = searcher.Search(query.Compile());
                var count = 0;

                foreach (var store in StoreCache._storeCache.Select(x => x.Value))
                {
                    foreach (var r in results.Where(x => x.Fields["template"] != "0"))
                    {
                        var item = ProductAdapter.CreateProductItemFromExamine(r, store);

                        if (item != null)
                        {
                            count++;
                            AddOrUpdateCache(r.Id, store, item);
                        }

                    }
                }

                stopwatch.Stop();

                Log.Info("Finished filling product cache with " + count + " items. Time it took to fill: " + stopwatch.Elapsed);
            }
            else
            {
                Log.Info("No examine search found with the name ExternalSearcher, Can not fill product cache.");
            }

        }

        /// <summary>
        /// Add or Update product item in product cache
        /// </summary>
        public static void AddOrUpdateCache(int id, Store store, Product newCacheItem)
        {
            string cacheKey = id.ToString() + "-" + store.Alias;

            _productCache.AddOrUpdate(
                cacheKey,
                newCacheItem,
                (key, oldCacheItem) => newCacheItem);
        }

        /// <summary>
        /// Remove item from product cache
        /// </summary>
        public static void RemoveItemFromCache(string id)
        {
            Product i = null;
            var remove = _productCache.TryRemove(id, out i);
        }

    }
}

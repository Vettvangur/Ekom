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
    public static class CategoryCache
    {

        private static readonly ILog Log =
                LogManager.GetLogger(
                    MethodBase.GetCurrentMethod().DeclaringType
                );

        /// <summary>
        /// Concurrent dictionary cache containing category models
        /// </summary>
        public static ConcurrentDictionary<string, Category> _categoryCache = new ConcurrentDictionary<string, Category>();

        /// <summary>
        /// Fill category cache with all categories in examine
        /// </summary>
        public static void FillCache()
        {

            var searcher = ExamineManager.Instance.SearchProviderCollection["ExternalSearcher"];

            if (searcher != null)
            {

                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                Log.Info("Starting to fill category cache...");

                ISearchCriteria searchCriteria = searcher.CreateSearchCriteria();
                var query = searchCriteria.NodeTypeAlias("uwbsCategory");
                var results = searcher.Search(query.Compile());
                var count = 0;

                foreach (var store in StoreCache._storeCache.Select(x => x.Value))
                {
                    foreach (var r in results.Where(x => x.Fields["template"] != "0"))
                    {
                        var item = CategoryAdapter.CreateCategoryItemFromExamine(r,store);

                        if (item != null)
                        {
                            count++;
                            AddOrUpdateCache(r.Id, store, item);
                        }

                    }
                }

                stopwatch.Stop();

                Log.Info("Finished filling category cache with " + count + " items. Time it took to fill: " + stopwatch.Elapsed);
            }
            else
            {
                Log.Info("No examine search found with the name ExternalSearcher, Can not fill category cache.");
            }

        }

        /// <summary>
        /// Add or Update category item in category cache
        /// </summary>
        public static void AddOrUpdateCache(int id, Store store, Category newCacheItem)
        {
            string cacheKey = id.ToString() + "-" + store.Alias;

            _categoryCache.AddOrUpdate(
                cacheKey,
                newCacheItem,
                (key, oldCacheItem) => newCacheItem);
        }

        /// <summary>
        /// Remove item from category cache
        /// </summary>
        public static void RemoveItemFromCache(string id)
        {
            Category i = null;
            var remove = _categoryCache.TryRemove(id, out i);
        }

    }
}

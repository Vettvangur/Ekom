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
    public static class VariantCache
    {
        private static readonly ILog Log =
                LogManager.GetLogger(
                    MethodBase.GetCurrentMethod().DeclaringType
                );

        /// <summary>
        /// Concurrent dictionary cache containing variant models
        /// </summary>
        public static ConcurrentDictionary<string, Variant> _variantCache = new ConcurrentDictionary<string, Variant>();

        /// <summary>
        /// Concurrent dictionary cache containing variant group models
        /// </summary>
        public static ConcurrentDictionary<string, VariantGroup> _variantGroupCache = new ConcurrentDictionary<string, VariantGroup>();

        /// <summary>
        /// Fill variant cache with all variants in examine
        /// </summary>
        public static void FillVariantCache()
        {

            var searcher = ExamineManager.Instance.SearchProviderCollection["ExternalSearcher"];

            if (searcher != null)
            {

                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                Log.Info("Starting to fill variant cache...");

                ISearchCriteria searchCriteria = searcher.CreateSearchCriteria();
                var query = searchCriteria.NodeTypeAlias("uwbsProductVariant");
                var results = searcher.Search(query.Compile());
                var count = 0;

                foreach (var store in StoreCache._storeCache.Select(x => x.Value))
                {
                    foreach (var r in results)
                    {
                        var item = VariantAdapter.CreateVariantItemFromExamine(r, store);

                        if (item != null)
                        {
                            count++;
                            AddOrUpdateVariantCache(r.Id, store, item);
                        }

                    }
                }

                stopwatch.Stop();

                Log.Info("Finished filling variant cache with " + count + " items. Time it took to fill: " + stopwatch.Elapsed);
            }
            else
            {
                Log.Info("No examine search found with the name ExternalSearcher, Can not fill variant cache.");
            }

        }
        /// <summary>
        /// Fill variant group cache with all variants in examine
        /// </summary>
        public static void FillVariantGroupCache()
        {

            var searcher = ExamineManager.Instance.SearchProviderCollection["ExternalSearcher"];

            if (searcher != null)
            {

                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                Log.Info("Starting to fill variant group cache...");

                ISearchCriteria searchCriteria = searcher.CreateSearchCriteria();
                var query = searchCriteria.NodeTypeAlias("uwbsProductVariantGroup");
                var results = searcher.Search(query.Compile());

                var count = 0;
                foreach (var store in StoreCache._storeCache.Select(x => x.Value))
                {
                    foreach (var r in results)
                    {
                        var item = VariantAdapter.CreateVariantGroupItemFromExamine(r, store);

                        if (item != null)
                        {
                            count++;
                            AddOrUpdateVariantGroupCache(r.Id, store, item);
                        }

                    }
                }

                stopwatch.Stop();

                Log.Info("Finished filling variant group cache with " + count + " items. Time it took to fill: " + stopwatch.Elapsed);
            }
            else
            {
                Log.Info("No examine search found with the name ExternalSearcher, Can not fill variant group cache.");
            }

        }

        /// <summary>
        /// Add or Update variant item in cache
        /// </summary>
        public static void AddOrUpdateVariantCache(int id, Store store, Variant newCacheItem)
        {
            string cacheKey = id.ToString() + "-" + store.Alias;

            _variantCache.AddOrUpdate(
                cacheKey,
                newCacheItem,
                (key, oldCacheItem) => newCacheItem);
        }

        /// <summary>
        /// Add or Update variant group item in cache
        /// </summary>
        public static void AddOrUpdateVariantGroupCache(int id, Store store, VariantGroup newCacheItem)
        {
            string cacheKey = id.ToString() + "-" + store.Alias;

            _variantGroupCache.AddOrUpdate(
                cacheKey,
                newCacheItem,
                (key, oldCacheItem) => newCacheItem);
        }

        /// <summary>
        /// Remove item from variant cache
        /// </summary>
        public static void RemoveItemFromVariantCache(string id)
        {
            Variant i = null;
            var remove = _variantCache.TryRemove(id, out i);
        }

        /// <summary>
        /// Remove item from variant group cache
        /// </summary>
        public static void RemoveItemFromVariantGroupCache(string id)
        {
            VariantGroup i = null;
            var remove = _variantGroupCache.TryRemove(id, out i);
        }
    }
}

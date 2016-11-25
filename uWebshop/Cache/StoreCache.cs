using Examine;
using Examine.Providers;
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
using umbraco;
using Umbraco.Core.Models;
using uWebshop.Adapters;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshop.Cache
{
    public static class StoreCache
    {
        private static readonly ILog Log =
                LogManager.GetLogger(
                    MethodBase.GetCurrentMethod().DeclaringType
                );

        /// <summary>
        /// Concurrent dictionary cache containing store models
        /// </summary>
        public static ConcurrentDictionary<string, Store> _storeCache = new ConcurrentDictionary<string, Store>();

        /// <summary>
        /// Concurrent dictionary cache containing nodes with storepicker property
        /// </summary>
        //public static ConcurrentDictionary<string, StoreNode> _storeNodeCache = new ConcurrentDictionary<string, StoreNode>();

        /// <summary>
        /// Concurrent dictionary cache containing domains for stores
        /// </summary>
        public static ConcurrentDictionary<string, IDomain> _storeDomainCache = new ConcurrentDictionary<string, IDomain>();



        /// <summary>
        /// Fill category cache with all products in examine
        /// </summary>
        public static void FillStoreCache()
        {

            var searcher = ExamineManager.Instance.SearchProviderCollection["ExternalSearcher"];

            if (searcher != null)
            {

                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                Log.Info("Starting to fill store cache...");

                ISearchCriteria searchCriteria = searcher.CreateSearchCriteria();
                var query = searchCriteria.NodeTypeAlias("uwbsStore");
                var results = searcher.Search(query.Compile());
                int count = 0;
                foreach (var r in results)
                {

                    var item = StoreAdapter.CreateStoreItemFromExamine(r);

                    if (item != null)
                    {
                        count++;
                        AddOrUpdateStoreCache(r.Id, item);
                    }
                }

                stopwatch.Stop();

                Log.Info("Finished filling store cache with " + count + " items. Time it took to fill: " + stopwatch.Elapsed);
            }
            else
            {
                Log.Info("No examine search found with the name ExternalSearcher, Can not fill store cache.");
            }

        }

        /// <summary>
        /// Fill store node cache with nodes that have storepicker in examine
        /// </summary>
        //public static void FillStoreNodesCache()
        //{

        //    var searcher = ExamineManager.Instance.SearchProviderCollection["ExternalSearcher"];

        //    if (searcher != null)
        //    {

        //        Stopwatch stopwatch = new Stopwatch();

        //        stopwatch.Start();

        //        Log.Info("Starting to fill store node cache...");

        //        ISearchCriteria searchCriteria = searcher.CreateSearchCriteria(BooleanOperation.Or);
        //        IBooleanOperation filter = null;

        //        if (_storeCache.Any())
        //        {
        //            foreach (var store in _storeCache)
        //            {
        //                if (filter == null)
        //                {
        //                    filter = searchCriteria.Field("uwbsStorePicker", store.Value.Id.ToString());
        //                }
        //                else
        //                {
        //                    filter = filter.Or().Field("uwbsStorePicker", store.Value.Id.ToString());
        //                }
        //            }

        //            var results = searcher.Search(filter.Compile());

        //            foreach (var r in results)
        //            {
        //                var storeNode = new StoreNode()
        //                {
        //                     Id = r.Id,
        //                     StoreId = Convert.ToInt32(r.Fields["uwbsStorePicker"])
        //                };

        //                AddOrUpdateStoreNodeCache(r.Id, storeNode);
        //            }

        //            stopwatch.Stop();

        //            Log.Info("Finished filling store node cache with " + results.Count() + " node items. Time it took to fill: " + stopwatch.Elapsed);
        //        }

        //    }
        //    else
        //    {
        //        Log.Info("No examine search found with the name ExternalSearcher, Can not fill store node cache.");
        //    }

        //}

        /// <summary>
        /// Fill store domain cache with domains from domain service
        /// </summary>
        public static void FillStoreDomainCache()
        {

            var domains = StoreService.GetAllStoreDomains();

            if (domains.Any())
            {
                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                Log.Info("Starting to fill store domain cache...");

                foreach (var d in domains)
                {
                    AddOrUpdateStoreDomainCache(d.Id, d);
                }

                Log.Info("Finished filling store domain cache with " + domains.Count() + " domain items. Time it took to fill: " + stopwatch.Elapsed);

            }

            BaseSearchProvider searcher = null;

            try
            {
                searcher = ExamineManager.Instance.SearchProviderCollection["ExternalSearcher"];
            }
            catch
            {
                Umbraco.Core.UmbracoApplicationBase.ApplicationStarted += (s, e) => System.Web.HttpRuntime.UnloadAppDomain();
            }

            if (searcher != null)
            {

                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                Log.Info("Starting to fill store cache...");

                ISearchCriteria searchCriteria = searcher.CreateSearchCriteria();
                var query = searchCriteria.NodeTypeAlias("uwbsStore");
                var results = searcher.Search(query.Compile());

                //var storeNodesResults = results.Where(x => !string.IsNullOrEmpty(x.Fields["uwbsStorePicker"]));

                foreach (var r in results)
                {
                    var store = new Store();
                    store.Id = r.Id;

                    AddOrUpdateStoreCache(r.Id, store);
                }

                stopwatch.Stop();

                Log.Info("Finished filling store cache with " + results.Count() + " store items. Time it took to fill: " + stopwatch.Elapsed);
            }
            else
            {
                Log.Info("No examine search found with the name ExternalSearcher, Can not fill store cache.");
            }

        }

        /// <summary>
        /// Add or Update item in store cache
        /// </summary>
        public static void AddOrUpdateStoreCache(int id, Store newCacheItem)
        {
            string cacheKey = id.ToString();

            _storeCache.AddOrUpdate(
                cacheKey,
                newCacheItem,
                (key, oldCacheItem) => newCacheItem);
        }

        /// <summary>
        /// Add or Update item in store node cache
        /// </summary>
        //public static void AddOrUpdateStoreNodeCache(int id, StoreNode newCacheItem)
        //{
        //    string cacheKey = id.ToString();

        //    _storeNodeCache.AddOrUpdate(
        //        cacheKey,
        //        newCacheItem,
        //        (key, oldCacheItem) => newCacheItem);
        //}

        /// <summary>
        /// Add or Update domain item in store domain cache
        /// </summary>
        public static void AddOrUpdateStoreDomainCache(int id, IDomain newCacheItem)
        {
            string cacheKey = id.ToString();

            _storeDomainCache.AddOrUpdate(
                cacheKey,
                newCacheItem,
                (key, oldCacheItem) => newCacheItem);
        }



        /// <summary>
        /// Remove item from product cache
        /// </summary>
        public static void RemoveItemFromCache(string id)
        {
            string i = null;
            //var remove = _storeNodeCache.TryRemove(id, out i);
        }
    }
}

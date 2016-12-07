using Examine;
using Examine.Providers;
using Examine.SearchCriteria;
using System.Diagnostics;
using uWebshop.Models;
using System;

namespace uWebshop.Cache
{
    public class StoreCache : BaseCache<Store>
    {
        public static StoreCache Instance { get; } = new StoreCache();

        protected override string nodeAlias { get; } = "uwbsStore";

        /// <summary>
        /// Fill Store cache with all products in examine
        /// </summary>
        public override void FillCache()
        {
            BaseSearchProvider searcher = null;

            try
            {
                searcher = ExamineManager.Instance.SearchProviderCollection["ExternalSearcher"];
            }
            catch // Restart Application if Examine just initialized
            {
                Umbraco.Core.UmbracoApplicationBase.ApplicationStarted += (s, e) => System.Web.HttpRuntime.UnloadAppDomain();
            }

            if (searcher != null)
            {
                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                Log.Info("Starting to fill store cache...");

                ISearchCriteria searchCriteria = searcher.CreateSearchCriteria();
                var query = searchCriteria.NodeTypeAlias(nodeAlias);
                var results = searcher.Search(query.Compile());

                int count = 0;
                foreach (var r in results)
                {
                    try
                    {
                        var item = new Store(r);

                        count++;
                        AddOrUpdateCache(r.Id, item);
                    }
                    catch { } // Skip on fail
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
        /// Add or Update item in store cache
        /// </summary>
        public void AddOrUpdateCache(int id, Store newCacheItem)
        {
            string cacheKey = id.ToString();

            _cache.AddOrUpdate(
                cacheKey,
                newCacheItem,
                (key, oldCacheItem) => newCacheItem);
        }

        /// <summary>
        /// Not needed as the stores custom FillCache method directly instantiates 
        /// <see cref="Store"/> objects
        /// </summary>
        protected override Store New(SearchResult r, Store store)
        {
            throw new NotImplementedException();
        }
    }
}

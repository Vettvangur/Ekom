using Examine;
using Examine.Providers;
using Examine.SearchCriteria;
using System.Diagnostics;
using uWebshop.Adapters;
using uWebshop.Models;

namespace uWebshop.Cache
{
    public class StoreCache : BaseCache<Store>
    {
        public static StoreCache Instance { get; } = new StoreCache();

        public override string nodeAlias { get; set; } = "uwbsStore";

        /// <summary>
        /// Fill category cache with all products in examine
        /// </summary>
        public void FillCache()
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
                    var item = StoreAdapter.CreateStoreItemFromExamine(r);

                    if (item != null)
                    {
                        count++;
                        AddOrUpdateCache(r.Id, item);
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
    }
}

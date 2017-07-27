using Examine;
using Examine.Providers;
using Examine.SearchCriteria;
using System.Diagnostics;
using System.Collections.Generic;
using uWebshop.Models;
using Umbraco.Core.Models;
using uWebshop.Helpers;
using uWebshop.Services;

namespace uWebshop.Cache
{
    public class StoreCache : BaseCache<Store>
    {
        public override string nodeAlias { get; } = "uwbsStore";

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="storeSvc"></param>
        /// <param name="logFac"></param>
        /// <param name="config"></param>
        /// <param name="examineManager"></param>
        public StoreCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManager examineManager
        )
        {
            _config = config;
            _examineManager = examineManager;
            _log = logFac.GetLogger(typeof(StoreCache));
        }

        /// <summary>
        /// Fill Store cache with all products in examine
        /// </summary>
        public override void FillCache()
        {
            BaseSearchProvider searcher = null;
            try
            {
                searcher = ExamineManager.Instance.SearchProviderCollection[_config.ExamineSearcher];
            }
            catch // Restart Application if Examine just initialized
            {
                Umbraco.Core.UmbracoApplicationBase.ApplicationStarted += (s, e) => System.Web.HttpRuntime.UnloadAppDomain();
            }

            if (searcher != null)
            {
                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                _log.Info("Starting to fill store cache...");
                int count = 0;

                ISearchCriteria searchCriteria = searcher.CreateSearchCriteria();
                var query = searchCriteria.NodeTypeAlias(nodeAlias);
                var results = searcher.Search(query.Compile());

                foreach (var r in results)
                {
                    try
                    {
                        var item = new Store(r);

                        count++;
                        AddOrReplaceFromCache(r.Id, item);
                    }
                    catch {
                        _log.Info("Failed to map to store. Id: " + r.Id);
                    }
                }

                stopwatch.Stop();

                _log.Info("Finished filling store cache with " + count + " items. Time it took to fill: " + stopwatch.Elapsed);
            }
            else
            {
                _log.Info("No examine search found with the name ExternalSearcher, Can not fill store cache.");
            }
        }

        /// <summary>
        /// <see cref="ICache"/> implementation.
        /// <see cref="StoreCache"/> specific implementation triggers refill of all <see cref="PerStoreCache{TItem, Tself}"/>
        /// </summary>
        public override void AddReplace(IContent node)
        {
            if (!node.IsItemUnpublished())
            {
                var item = new Store(node);

                if (item != null)
                {
                    AddOrReplaceFromCache(node.Id, item);

                    IEnumerable<ICache> succeedingCaches = _config.Succeeding(this);

                    // Refill all per store caches
                    foreach (var cacheEntry in succeedingCaches)
                    {
                        if (cacheEntry is IPerStoreCache perStoreCache)
                        {
                            perStoreCache.FillCache(item);
                        }
                    }
                }
            }
        }
    }
}

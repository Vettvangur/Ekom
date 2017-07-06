using Examine;
using Examine.Providers;
using Examine.SearchCriteria;
using System.Diagnostics;
using uWebshop.Models;
using System;
using System.Collections.Concurrent;
using Umbraco.Core.Models;
using uWebshop.Helpers;
using System.Collections.Generic;

namespace uWebshop.Cache
{
    public class StoreCache : BaseCache<Store, StoreCache>
    {
        protected override string nodeAlias { get; } = "uwbsStore";

        /// <summary>
        /// Fill Store cache with all products in examine
        /// </summary>
        public override void FillCache()
        {
            BaseSearchProvider searcher = null;

            try
            {
                searcher = ExamineManager.Instance.SearchProviderCollection[Configuration.ExamineSearcher];
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
                int count = 0;

                try
                {
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
                            Log.Info("Failed to map to store. Id: " + r.Id);
                        }
                    }

                } catch(Exception ex)
                {
                    Log.Error("Filling store cache failed!", ex);
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

                    IEnumerable<ICache> succeedingCaches = Data.InitializationSequence.Succeeding(this);

                    // Refill all per store caches
                    foreach (var cache in succeedingCaches)
                    {
                        if (cache is IPerStoreCache)
                           (cache as IPerStoreCache).FillCache(item);
                    }
                }
            }
        }
    }
}

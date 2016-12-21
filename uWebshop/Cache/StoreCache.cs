using Examine;
using Examine.Providers;
using Examine.SearchCriteria;
using System.Diagnostics;
using uWebshop.Models;
using System;
using System.Collections.Concurrent;

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
                        AddOrReplaceFromCache(r.Id, item);
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
    }
}

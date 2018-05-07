using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Abstractions;
using Ekom.Services;
using Examine.Providers;
using Examine.SearchCriteria;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Umbraco.Core.Models;

namespace Ekom.Cache
{
    class StoreCache : BaseCache<IStore>
    {
        public override string NodeAlias { get; } = "ekmStore";

        /// <summary>
        /// ctor
        /// </summary>
        public StoreCache(
            ILogFactory logFac,
            Configuration config,
            IObjectFactory<IStore> objectFactory
        ) : base(config, objectFactory)
        {
            _log = logFac.GetLogger<StoreCache>();
        }

        /// <summary>
        /// Fill Store cache with all products in examine
        /// </summary>
        public override void FillCache()
        {
            BaseSearchProvider searcher = null;
            try
            {
                searcher = _examineManager.SearchProviderCollection[_config.ExamineSearcher];
            }
            catch // Restart Application if Examine just initialized
            {
                // I have no idea if this does any good
                _log.Warn("Unloading Application Domain");
                Umbraco.Core.UmbracoApplicationBase.ApplicationStarted += (s, e) => System.Web.HttpRuntime.UnloadAppDomain();
            }

            if (searcher != null)
            {
                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                _log.Info("Starting to fill store cache...");
                int count = 0;

                ISearchCriteria searchCriteria = searcher.CreateSearchCriteria();
                var query = searchCriteria.NodeTypeAlias(NodeAlias);
                var results = searcher.Search(query.Compile());

                foreach (var r in results)
                {
                    try
                    {
                        var item = new Store(r);

                        count++;

                        var itemKey = Guid.Parse(r.Fields["key"]);
                        AddOrReplaceFromCache(itemKey, item);
                    }
                    catch (Exception ex) // Skip on fail
                    {
                        _log.Warn("Failed to map to store. Id: " + r.Id, ex);
                    }
                }

                stopwatch.Stop();

                _log.Info("Finished filling store cache with " + count + " items. Time it took to fill: " + stopwatch.Elapsed);
            }
            else
            {
                _log.Error($"No examine search found with the name {_config.ExamineSearcher}, Can not fill store cache.");
            }
        }

        /// <summary>
        /// <see cref="ICache"/> implementation.
        /// <see cref="StoreCache"/> specific implementation triggers refill of all <see cref="BaseCache{TItem}"/>
        /// </summary>
        public override void AddReplace(IContent node)
        {
            if (!node.IsItemUnpublished())
            {
                var item = (Store)(_objFac?.Create(node) ?? Activator.CreateInstance(typeof(Store), node));

                if (item != null)
                {
                    AddOrReplaceFromCache(node.Key, item);

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

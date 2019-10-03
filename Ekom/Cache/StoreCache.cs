using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Utilities;
using Examine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Examine;

namespace Ekom.Cache
{
    class StoreCache : BaseCache<IStore>
    {
        public override string NodeAlias { get; } = "ekmStore";

        /// <summary>
        /// ctor
        /// </summary>
        public StoreCache(
            Configuration config,
            ILogger logger,
            IFactory factory,
            IObjectFactory<IStore> objectFactory
        ) : base(config, logger, factory, objectFactory)
        {
        }

        /// <summary>
        /// Fill Store cache with all products in examine
        /// </summary>
        public override void FillCache()
        {
            if (ExamineManager.TryGetSearcher(_config.ExamineSearcher, out ISearcher searcher))
            {
                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                _logger.Info<StoreCache>("Starting to fill store cache...");
                int count = 0;

                var results = searcher.CreateQuery("content")
                    .NodeTypeAlias(NodeAlias)
                    .Execute();

                foreach (var r in results)
                {
                    try
                    {
                        var item = new Store(r);

                        count++;

                        var itemKey = Guid.Parse(r.Key());
                        AddOrReplaceFromCache(itemKey, item);
                    }
                    catch (Exception ex) // Skip on fail
                    {
                        _logger.Warn<StoreCache>(ex, "Failed to map to store. Id: " + r.Id);
                    }
                }

                stopwatch.Stop();

                _logger.Info<StoreCache>("Finished filling store cache with " + count + " items. Time it took to fill: " + stopwatch.Elapsed);
            }
            else
            {
                _logger.Error<StoreCache>($"No examine search found with the name {_config.ExamineSearcher}, Can not fill store cache.");
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

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
            if (ExamineManager.TryGetIndex(_config.ExamineIndex, out IIndex index))
            {
                // This line can give error Lock obtain timed out.
                var searcher = index.GetSearcher();

#if DEBUG

                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();
#endif

                _logger.Debug<StoreCache>("Starting to fill store cache...");
                int count = 0;

                var results = searcher.CreateQuery()
                    .NodeTypeAlias(NodeAlias)
                    .Execute();

                foreach (var r in results)
                {
                    try
                    {
                        var item = _objFac?.Create(r) ?? new Store(r);

                        if (item != null)
                        {
                            count++;

                            var itemKey = Guid.Parse(r.Key());
                            AddOrReplaceFromCache(itemKey, item);
                        }
                    }
                    catch (Exception ex) // Skip on fail
                    {
                        _logger.Warn<StoreCache>(ex, "Failed to map to store. Id: " + r.Id);
                    }
                }

#if DEBUG
                stopwatch.Stop();

                _logger.Debug<StoreCache>("Finished filling store cache with " + count + " items. Time it took to fill: " + stopwatch.Elapsed);
#endif
#if !DEBUG
                _logger.Debug<StoreCache>("Finished filling per store cache with " + count + " items");
#endif

            }
            else
            {
                _logger.Error<StoreCache>($"No examine index found with the name {_config.ExamineIndex}, Can not fill store cache.");
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

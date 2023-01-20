using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
            ILogger<BaseCache<IStore>> logger,
            IObjectFactory<IStore> objectFactory,
            IServiceProvider serviceProvider
        ) : base(config, logger, objectFactory, serviceProvider)
        {
        }

        /// <summary>
        /// Fill Store cache with all products in examine
        /// </summary>
        public override void FillCache()
        {
            try
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                _logger.LogDebug("Starting to fill store cache...");
                int count = 0;

                var results = nodeService.NodesByTypes(NodeAlias).ToList();

                foreach (var r in results)
                {
                    try
                    {
                        var item = _objFac?.Create(r) ?? new Store(r);

                        count++;

                        AddOrReplaceFromCache(r.Key, item);

                    }
                    catch (Exception ex) // Skip on fail
                    {
                        _logger.LogWarning(ex, "Failed to map to store. Id: {Id}", r.Id);
                    }
                }


                stopwatch.Stop();
                _logger.LogInformation(
                    "Finished filling store cache with {Count} items. Time it took to fill: {Elapsed}",
                    count,
                    stopwatch.Elapsed);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build store Cache");
            }

        }

        /// <summary>
        /// <see cref="ICache"/> implementation.
        /// <see cref="StoreCache"/> specific implementation triggers refill of all <see cref="BaseCache{TItem}"/>
        /// </summary>
        public override void AddReplace(UmbracoContent node)
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

using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Utilities;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Ekom.Cache
{
    class DiscountCache : PerStoreCache<IDiscount>
    {
        public override string NodeAlias { get; } = "ekmOrderDiscount";

        /// <summary>
        /// ctor
        /// </summary>
        public DiscountCache(
            Configuration config,
            ILogger<IPerStoreCache<IDiscount>> logger,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<IDiscount> perStoreFactory,
            IServiceProvider serviceProvider)
            : base(config, logger, storeCache, perStoreFactory, serviceProvider)
        {
        }

        /// <summary>
        /// Fill the given stores cache of TItem
        /// </summary>
        /// <param name="store">The current store being filled of TItem</param>
        /// <param name="results">Examine search results</param>
        /// <returns>Count of items added</returns>
        protected override int FillStoreCache(IStore store, List<UmbracoContent> results)
        {
            int count = 0;

            var curStoreCache
                = Cache[store.Alias] = new ConcurrentDictionary<Guid, IDiscount>();

            foreach (var r in results)
            {
                try
                {
                    var ancestors = nodeService.NodeAncestors(r.Id.ToString());
                    // Traverse up parent nodes, checking disabled status and published status
                    if (!r.IsItemDisabled(store, ancestors))
                    {
                        var item = _objFac?.Create(r, store) ?? new Discount(r, store);

                        count++;

                        curStoreCache[r.Key] = item;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error on adding item with id: {Id} from Examine in Store: {Store}",
                        r.Id,
                        store.Alias
                    );
                }
            }

            return count;
        }

        /// <summary>
        /// Adds or replaces an item from all store caches
        /// </summary>
        public override void AddReplace(UmbracoContent node)
        {
            foreach (var store in _storeCache.Cache)
            {
                try
                {
                    var ancestors = nodeService.NodeAncestors(node.Id.ToString());

                    if (!node.IsItemDisabled(store.Value, ancestors))
                    {

                        var item = _objFac?.Create(node, store.Value)
                            ?? new Discount(node, store.Value);

                        Cache[store.Value.Alias][node.Key] = item;
                    }
                }
                catch (Exception ex) // Skip on fail
                {
                    _logger.LogError(
                        ex,
                        "Error on Add/Replacing item with id: {Id} in store: {Store}",
                        node.Id,
                        store.Value.Alias
                    );
                }
            }
        }

        /// <summary>
        /// <see cref="ICache"/> implementation,
        /// handles removal of nodes when umbraco events fire
        /// </summary>
        public override void Remove(Guid key)
        {
            _logger.LogDebug("Attempting to remove discount with key {Key}", key);

            foreach (var store in _storeCache.Cache)
            {
                Cache[store.Value.Alias].TryRemove(key, out _);
            }
        }
    }
}

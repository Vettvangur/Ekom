using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models.Discounts;
using Ekom.Services;
using Ekom.Utilities;
using Examine;
using Examine.Providers;
using System;
using System.Collections.Concurrent;
using System.Linq;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using GlobalDiscountCache
    = System.Collections.Concurrent.ConcurrentDictionary<string,
        System.Collections.Concurrent.ConcurrentDictionary<System.Guid, Ekom.Interfaces.IDiscount>>;
using PerStoreCouponCache
    = System.Collections.Concurrent.ConcurrentDictionary<string,
        System.Collections.Concurrent.ConcurrentDictionary<string, Ekom.Interfaces.IDiscount>>;

namespace Ekom.Cache
{
    class DiscountCache : PerStoreCache<IDiscount>
    {
        /// <summary>
        /// All order <see cref="IDiscount"/>'s containing no coupon
        /// </summary>
        public GlobalDiscountCache GlobalDiscounts { get; }
            = new GlobalDiscountCache();

        public override string NodeAlias { get; } = "ekmOrderDiscount";

        /// <summary>
        /// ctor
        /// </summary>
        public DiscountCache(
            Configuration config,
            ILogger logger,
            IFactory factory,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<IDiscount> perStoreFactory
        )
            : base(config, logger, factory, storeCache, perStoreFactory)
        {
        }

        /// <summary>
        /// Fill the given stores cache of TItem
        /// </summary>
        /// <param name="store">The current store being filled of TItem</param>
        /// <param name="results">Examine search results</param>
        /// <returns>Count of items added</returns>
        protected override int FillStoreCache(IStore store, ISearchResults results)
        {
            int count = 0;

            var curStoreCache
                = Cache[store.Alias] = new ConcurrentDictionary<Guid, IDiscount>();
            var curStoreGlobalDiscountCache
                = GlobalDiscounts[store.Alias] = new ConcurrentDictionary<Guid, IDiscount>();

            foreach (var r in results)
            {
                try
                {
                    // Traverse up parent nodes, checking disabled status and published status
                    if (!r.IsItemDisabled(store))
                    {
                        var item = _objFac?.Create(r, store) ?? new Discount(r, store);

                        if (item != null)
                        {
                            count++;

                            curStoreGlobalDiscountCache[item.Key] = item;
    
                            var itemKey = Guid.Parse(r.Key());
                            curStoreCache[itemKey] = item;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error<DiscountCache>(
                        ex, 
                        $"Error on adding item with id: {r.Id} from Examine in Store: {store.Alias}"
                    );
                }
            }

            return count;
        }

        /// <summary>
        /// Adds or replaces an item from all store caches
        /// </summary>
        public override void AddReplace(IContent node)
        {
            foreach (var store in _storeCache.Cache)
            {
                try
                {
                    if (!node.IsItemDisabled(store.Value))
                    {

                        var item = _objFac?.Create(node, store.Value)
                            ?? new Discount(node, store.Value);

                        if (item != null)
                        {
                            GlobalDiscounts[store.Value.Alias][item.Key] = item;

                            Cache[store.Value.Alias][node.Key] = item;

                        }

                    }
                }
                catch (Exception ex) // Skip on fail
                {
                    _logger.Error<DiscountCache>(
                        ex,
                        $"Error on Add/Replacing item with id: {node.Id} in store: {store.Value.Alias}"
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
            _logger.Debug<DiscountCache>($"Attempting to remove discount with key {key}");
            foreach (var store in _storeCache.Cache)
            {

                Cache[store.Value.Alias].TryRemove(key, out _);

                GlobalDiscounts[store.Value.Alias].TryRemove(key, out _);
            }
        }
    }
}

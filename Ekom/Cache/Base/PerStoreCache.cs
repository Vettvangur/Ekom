using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Utilities;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Ekom.Cache
{
    /// <summary>
    /// Per store caching for entities of generic type TItem
    /// </summary>
    /// <typeparam name="TItem">Type of entity to cache</typeparam>
    abstract class PerStoreCache<TItem> : ICache, IPerStoreCache, IPerStoreCache<TItem>
        where TItem : class
    {
        protected Configuration _config;
        protected ILogger _logger;
        protected IBaseCache<IStore> _storeCache;
        protected IPerStoreFactory<TItem> _objFac;
        protected IServiceProvider _serviceProvider;

        protected INodeService nodeService => _serviceProvider.GetService<INodeService>();

        public PerStoreCache(
            Configuration config,
            ILogger<IPerStoreCache<TItem>> logger,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<TItem> objFac,
            IServiceProvider serviceProvider)
        {
            _config = config;
            _logger = logger;
            _storeCache = storeCache;
            _objFac = objFac;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Umbraco Node Alias name used in Examine search
        /// </summary>
        public abstract string NodeAlias { get; }

        /// <summary>
        /// Concurrent dictionaries per store
        /// </summary>
        public virtual ConcurrentDictionary<string, ConcurrentDictionary<Guid, TItem>> Cache { get; }
            = new ConcurrentDictionary<string, ConcurrentDictionary<Guid, TItem>>();

        /// <summary>
        /// Class indexer
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public ConcurrentDictionary<Guid, TItem> this[string index] => Cache[index];

        public virtual void FillCache()
        {
            FillCache(null);
        }

        /// <summary>
        /// Base Fill cache method appropriate for most derived caches
        /// </summary>
        /// <param name="storeParam">
        /// This parameter is supplied when adding a store at runtime, 
        /// triggering the given stores filling
        /// </param>
        public virtual void FillCache(IStore storeParam = null)
        {

            if (!string.IsNullOrEmpty(NodeAlias))
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                _logger.LogDebug("Starting to fill per store cache for {NodeAlias}...", NodeAlias);

                int count = 0;

                try
                {
                    var results = nodeService.NodesByTypes(NodeAlias).ToList();

                    if (storeParam == null) // Startup initialization
                    {
                        foreach (var store in _storeCache.Cache.Select(x => x.Value))
                        {
                            count += FillStoreCache(store, results);
                        }
                    }
                    else // Triggered with dynamic addition/removal of store
                    {
                        count += FillStoreCache(storeParam, results);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Filling per store cache Failed for {NodeAlias}!", NodeAlias);
                }

                stopwatch.Stop();
                _logger.LogInformation(
                    "Finished filling per store cache with {Count} items for {NodeAlias}. Time it took to fill: {Elapsed}",
                    count,
                    NodeAlias,
                    stopwatch.Elapsed
                );
            }
            else
            {
                _logger.LogError(
                    "No examine search found with the name {ExamineIndex}, Can not fill cache.",
                    _config.ExamineIndex
                );
            }
        }

        /// <summary>
        /// Fill the given stores cache of TItem
        /// </summary>
        /// <param name="store">The current store being filled of TItem</param>
        /// <param name="results">UmbracoContent results</param>
        /// <returns>Count of items added</returns>
        protected virtual int FillStoreCache(IStore store, List<UmbracoContent> results)
        {
            int count = 0;

            var curStoreCache = Cache[store.Alias] = new ConcurrentDictionary<Guid, TItem>();

            foreach (var r in results)
            {
                try
                {
                    var ancestors = nodeService.NodeAncestors(r.Id.ToString());

                    var isDisabled = r.IsItemDisabled(store, ancestors);

                    if (isDisabled)
                    {
                        continue;
                    }

                    var item = _objFac?.Create(r, store)
                        ?? (TItem)Activator.CreateInstance(typeof(TItem), r, store);

                    if (item != null)
                    {
                        count++;

                        curStoreCache[r.Key] = item;
                    }
                    
                }
                catch (Exception ex) // Skip on fail
                {
                    _logger.LogWarning(
                        ex,
                        "Error on adding item with id: " + r.Id + " from Examine in Store: " + store.Alias
                    );
                }
            }

            return count;
        }

        public void AddOrReplaceFromCache(Guid id, Store store, TItem newCacheItem)
        {
            Cache[store.Alias][id] = newCacheItem;
        }

        public bool RemoveItemFromCache(IStore store, Guid id)
        {
            return Cache[store.Alias].TryRemove(id, out TItem i);
        }

        /// <summary>
        /// Adds or replaces an item from all store caches
        /// </summary>
        public void AddOrReplaceFromAllCaches(UmbracoContent node)
        {
            foreach (var store in _storeCache.Cache)
            {
                try
                {
                    var ancestors = nodeService.NodeAncestors(node.Id.ToString());

                    var isDisabled = node.IsItemDisabled(store.Value, ancestors);

                    if (isDisabled)
                    {
                        Cache[store.Value.Alias].TryRemove(node.Key, out _);
                        continue;
                    }

           
                    var item = _objFac?.Create(node, store.Value)
                        ?? (TItem)Activator.CreateInstance(typeof(TItem), node, store.Value);

                    if (item != null) Cache[store.Value.Alias][node.Key] = item;
                    
     
                }
                catch (Exception ex) // Skip on fail
                {
                    _logger.LogWarning(
                        ex,
                        "Error on adding item with id: " + node.Id + " from Examine in Store: " + store.Value.Alias
                    );
                }
            }
        }

        /// <summary>
        /// Removes an item from all store caches
        /// </summary>
        public void RemoveItemFromAllCaches(Guid id)
        {
            foreach (var store in _storeCache.Cache)
            {
                Cache[store.Value.Alias].TryRemove(id, out _);
            }
        }


        /// <summary>
        /// <see cref="ICache"/> implementation,
        /// handles addition of nodes when umbraco events fire
        /// </summary>
        public virtual void AddReplace(UmbracoContent node)
        {
            AddOrReplaceFromAllCaches(node);
        }

        /// <summary>
        /// <see cref="ICache"/> implementation,
        /// handles removal of nodes when umbraco events fire
        /// </summary>
        public virtual void Remove(Guid id)
        {
            RemoveItemFromAllCaches(id);
        }
    }
}

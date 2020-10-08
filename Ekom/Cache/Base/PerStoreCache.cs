using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Utilities;
using Examine;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Examine;

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
        protected IFactory _factory;

        /// <summary>
        /// This is important since Caches are persistant objects while the ExamineManager should be per request scoped.
        /// </summary>
        protected IExamineManager ExamineManager => _factory.GetInstance<IExamineManager>();

        public PerStoreCache(
            Configuration config,
            ILogger logger,
            IFactory factory,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<TItem> objFac
        )
        {
            _config = config;
            _logger = logger;
            _factory = factory;
            _storeCache = storeCache;
            _objFac = objFac;
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
            if (!string.IsNullOrEmpty(NodeAlias)
            && ExamineManager.TryGetIndex(_config.ExamineIndex, out IIndex index))
            {
#if DEBUG
                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();
#endif
                var searcher = index.GetSearcher();

                _logger.Debug<PerStoreCache<TItem>>("Starting to fill...");
                int count = 0;

                try
                {

                    var results = searcher.CreateQuery("content")
                        .NodeTypeAlias(NodeAlias)
                        .Execute(int.MaxValue);

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
                    _logger.Error<PerStoreCache<TItem>>(ex, "Filling per store cache Failed!");
                }

#if DEBUG
                stopwatch.Stop();
                _logger.Debug<PerStoreCache<TItem>>(
                    $"Finished filling per store cache with {count} items. Time it took to fill: {stopwatch.Elapsed}"
                );
#endif
#if !DEBUG
                _logger.Debug(typeof(PerStoreCache<>), "Finished filling per store cache with " + count + " items");
#endif
            }
            else
            {
                _logger.Error<PerStoreCache<TItem>>(
                    $"No examine search found with the name {_config.ExamineIndex}, Can not fill cache."
                );
            }
        }

        /// <summary>
        /// Fill the given stores cache of TItem
        /// </summary>
        /// <param name="store">The current store being filled of TItem</param>
        /// <param name="results">Examine search results</param>
        /// <returns>Count of items added</returns>
        protected virtual int FillStoreCache(IStore store, ISearchResults results)
        {
            int count = 0;

            var curStoreCache = Cache[store.Alias] = new ConcurrentDictionary<Guid, TItem>();

            foreach (var r in results)
            {
                try
                {
                    // Traverse up parent nodes, checking disabled status and published status
                    if (!r.IsItemDisabled(store))
                    {
                        var item = _objFac?.Create(r, store)
                            ?? (TItem)Activator.CreateInstance(typeof(TItem), r, store);

                        if (item != null)
                        {
                            count++;

                            var itemKey = Guid.Parse(r.Key());
                            curStoreCache[itemKey] = item;
                        }
                    }
                }
                catch (Exception ex) // Skip on fail
                {
                    _logger.Error<PerStoreCache<TItem>>(
                        ex,
                        $"Error on adding item with id: {r.Id} from Examine in Store: {store.Alias}"
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
        public void AddOrReplaceFromAllCaches(IContent node)
        {
            foreach (var store in _storeCache.Cache)
            {
                try
                {
                    if (!node.IsItemDisabled(store.Value))
                    {
                        var item = _objFac?.Create(node, store.Value)
                            ?? (TItem)Activator.CreateInstance(typeof(TItem), node, store.Value);

                        if (item != null) Cache[store.Value.Alias][node.Key] = item;
                    }
                }
                catch (Exception ex) // Skip on fail
                {
                    _logger.Error<PerStoreCache<TItem>>(
                        ex,
                        $"Error on Add/Replacing item with id: {node.Id} in store: {store.Value.Alias}"
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
        public virtual void AddReplace(IContent node)
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

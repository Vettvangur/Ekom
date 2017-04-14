using Examine;
using Examine.SearchCriteria;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Umbraco.Core.Models;
using uWebshop.Extend;
using uWebshop.Helpers;
using uWebshop.Models;

namespace uWebshop.Cache
{
    /// <summary>
    /// Per store caching for entities of type <see cref="TItem"/>
    /// </summary>
    /// <typeparam name="TItem">Type of entity to cache</typeparam>
    /// <typeparam name="Tself">
    /// The inheriting class itself, <para/>
    /// we use this param to deduce the singleton type to create
    /// </typeparam>
    public abstract class PerStoreCache<TItem, Tself> : ICache, IPerStoreCache
                    where Tself : PerStoreCache<TItem, Tself>, new()
    {
        /// <summary>
        /// Retrieve the singletons Cache
        /// </summary>
        public static ConcurrentDictionary<string, ConcurrentDictionary<int, TItem>> 
                        Cache { get { return Instance._cache; } }

        /// <summary>
        /// Singleton
        /// </summary>
        public static Tself Instance { get; } = new Tself();


        /// <summary>
        /// Umbraco Node Alias name used in Examine search
        /// </summary>
        protected abstract string nodeAlias { get; }

        /// <summary>
        /// Concurrent dictionaries per store
        /// </summary>
        protected ConcurrentDictionary<string, ConcurrentDictionary<int, TItem>> _cache
          = new ConcurrentDictionary<string, ConcurrentDictionary<int, TItem>>();

        /// <summary>
        /// Derived classes define simple instantiation methods, 
        /// saving performance vs Activator.CreateInstance
        /// </summary>
        protected abstract TItem New(SearchResult r, Store store);

        /// <summary>
        /// <see cref="ICache"/> implementation.
        /// Allows us to have a common interface for all caches
        /// </summary>
        public void FillCache()
        {
            if (Extending.CacheExtensionMap.ContainsKey(typeof(Tself)))
            {
                var cacheExtensions = Extending.CacheExtensionMap[typeof(Tself)];
                cacheExtensions.FillCache();
            }
            else FillCacheInternal();
        }

        /// <summary>
        /// Base Fill cache method appropriate for most derived caches
        /// </summary>
        /// <param name="storeParam">This parameter is supplied when adding a store at runtime, 
        /// triggering the given stores filling</param>
        public void FillCacheInternal(Store storeParam = null)
        {
            var searcher = ExamineManager.Instance.SearchProviderCollection["ExternalSearcher"];

            if (searcher != null && !string.IsNullOrEmpty(nodeAlias))
            {
                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                Log.Info("Starting to fill " + typeof(Tself).FullName + "...");

                ISearchCriteria searchCriteria = searcher.CreateSearchCriteria();
                IBooleanOperation query        = searchCriteria.NodeTypeAlias(nodeAlias);
                ISearchResults results         = searcher.Search(query.Compile());

                int count = 0;

                if (storeParam == null) // Startup initalization
                {
                    foreach (var store in StoreCache.Cache.Select(x => x.Value))
                    {
                        count += FillStoreCache(store, results);
                    }
                }
                else // Triggered with dynamic addition/removal of store
                {
                    count += FillStoreCache(storeParam, results);
                }

                stopwatch.Stop();

                Log.Info("Finished filling " + typeof(Tself).FullName + " with " + count + " items. Time it took to fill: " + stopwatch.Elapsed);
            }
            else
            {
                Log.Info("No examine search found with the name ExternalSearcher, Can not fill category cache.");
            }
        }

        /// <summary>
        /// Fill the given stores cache of <see cref="TItem"/>
        /// </summary>
        /// <param name="store">The current store being filled of <see cref="TItem"/></param>
        /// <param name="results">Examine search results</param>
        /// <returns>Count of items added</returns>
        private int FillStoreCache(Store store, ISearchResults results)
        {
            int count = 0;

            _cache[store.Alias] = new ConcurrentDictionary<int, TItem>();

            var curStoreCache = _cache[store.Alias];

            foreach (var r in results.Where(x => x.Fields["template"] != "0"))
            {
                try
                {
                    // Traverse up parent nodes, checking disabled status and published status
                    if (!r.IsItemDisabled(store))
                    {
                        var item = New(r, store);

                        if (item != null)
                        {
                            count++;
                            curStoreCache[r.Id] = item;
                        }
                    }
                }
                catch (Exception ex) // Skip on fail
                {
                    Log.Error("Error on adding item with id: " + r.Id + " from Examine", ex);
                }
            }

            return count;
        }

        public void AddOrReplaceFromCache(int id, Store store, TItem newCacheItem)
        {
            _cache[store.Alias][id] = newCacheItem;
        }

        public bool RemoveItemFromCache(Store store, int id)
        {
            TItem i = default(TItem);
            return _cache[store.Alias].TryRemove(id, out i);
        }
        
        /// <summary>
        /// Adds or replaces an item from all store caches
        /// </summary>
        public void AddOrReplaceFromAllCaches(IContent node)
        {
            foreach (var store in StoreCache.Cache)
            {
                try
                {
                    if (!node.IsItemDisabled(store.Value))
                    {
                        var item = (TItem) Activator.CreateInstance(typeof(TItem), node, store.Value);

                        if (item != null) _cache[store.Value.Alias][node.Id] = item;
                    }
                }
                catch (Exception ex) // Skip on fail
                {
                    Log.Error("Error on Add/Replacing item with id: " + node.Id, ex);
                }
            }
        }

        /// <summary>
        /// Removes an item from all store caches
        /// </summary>
        public void RemoveItemFromAllCaches(int id)
        {
            TItem i = default(TItem);

            foreach (var store in StoreCache.Cache)
            {
                _cache[store.Value.Alias].TryRemove(id, out i);
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
        public virtual void Remove(int id)
        {
            RemoveItemFromAllCaches(id);
        }

        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}

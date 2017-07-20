using Examine;
using Examine.SearchCriteria;
using log4net;
using Microsoft.Practices.Unity;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Umbraco.Core.Models;
using uWebshop.App_Start;
using uWebshop.Helpers;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshop.Cache
{
    /// <summary>
    /// Per store caching for entities of type <see cref="TItem"/>
    /// </summary>
    /// <typeparam name="TItem">Type of entity to cache</typeparam>
    public abstract class PerStoreCache<TItem> : ICache, IPerStoreCache, IPerStoreCache<TItem>
    {
        protected Configuration _config;
        protected ExamineManager _examineManager;
        protected ILog _log;
        protected IBaseCache<Store> _storeCache;

        /// <summary>
        /// Umbraco Node Alias name used in Examine search
        /// </summary>
        protected abstract string nodeAlias { get; }

        /// <summary>
        /// Concurrent dictionaries per store
        /// </summary>
        public ConcurrentDictionary<string, ConcurrentDictionary<int, TItem>> Cache { get; }
         = new ConcurrentDictionary<string, ConcurrentDictionary<int, TItem>>();

        /// <summary>
        /// Derived classes define simple instantiation methods, 
        /// saving performance vs Activator.CreateInstance
        /// </summary>
        protected abstract TItem New(SearchResult r, Store store);


        public virtual void FillCache()
        {
            FillCache(null);
        }

        /// <summary>
        /// Base Fill cache method appropriate for most derived caches
        /// </summary>
        /// <param name="storeParam">This parameter is supplied when adding a store at runtime, 
        /// triggering the given stores filling</param>
        public virtual void FillCache(Store storeParam = null)
        {
            var searcher = _examineManager.SearchProviderCollection[_config.ExamineSearcher];

            if (searcher != null && !string.IsNullOrEmpty(nodeAlias))
            {
                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                _log.Info("Starting to fill...");
                int count = 0;

                try
                {
                    ISearchCriteria searchCriteria = searcher.CreateSearchCriteria();
                    IBooleanOperation query = searchCriteria.NodeTypeAlias(nodeAlias);
                    ISearchResults results = searcher.Search(query.Compile());
                    
                    if (storeParam == null) // Startup initalization
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
                    _log.Error("Filling per store cache Failed!", ex);
                } 

                stopwatch.Stop();

                _log.Info("Finished filling cache with " + count + " items. Time it took to fill: " + stopwatch.Elapsed);
            }
            else
            {
                _log.Info("No examine search found with the name ExternalSearcher, Can not fill category cache.");
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

            Cache[store.Alias] = new ConcurrentDictionary<int, TItem>();

            var curStoreCache = Cache[store.Alias];

            foreach (var r in results)
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
                    _log.Error("Error on adding item with id: " + r.Id + " from Examine", ex);
                }
            }

            return count;
        }

        public void AddOrReplaceFromCache(int id, Store store, TItem newCacheItem)
        {
            Cache[store.Alias][id] = newCacheItem;
        }

        public bool RemoveItemFromCache(Store store, int id)
        {
            TItem i = default(TItem);
            return Cache[store.Alias].TryRemove(id, out i);
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
                        var item = (TItem) Activator.CreateInstance(typeof(TItem), node, store.Value);

                        if (item != null) Cache[store.Value.Alias][node.Id] = item;
                    }
                }
                catch (Exception ex) // Skip on fail
                {
                    _log.Error("Error on Add/Replacing item with id: " + node.Id, ex);
                }
            }
        }

        /// <summary>
        /// Removes an item from all store caches
        /// </summary>
        public void RemoveItemFromAllCaches(int id)
        {
            TItem i = default(TItem);

            foreach (var store in _storeCache.Cache)
            {
                Cache[store.Value.Alias].TryRemove(id, out i);
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
    }
}

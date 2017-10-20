using Examine;
using Examine.SearchCriteria;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Umbraco.Core.Models;
using Ekom.Helpers;

namespace Ekom.Cache
{
    /// <summary>
    /// For custom caches or global non store dependant caches
    /// </summary>
    /// <typeparam name="TItem">Type of data to cache</typeparam>
    abstract class BaseCache<TItem> : ICache, IBaseCache<TItem>
    {
        protected Configuration _config;
        protected ExamineManager _examineManager;
        protected ILog _log;

        /// <summary>
        /// Umbraco Node Alias name used in Examine search
        /// </summary>
        public abstract string NodeAlias { get; }

        public ConcurrentDictionary<Guid, TItem> Cache { get; }
         = new ConcurrentDictionary<Guid, TItem>();


        protected void AddOrReplaceFromCache(Guid id, TItem newCacheItem)
        {
            Cache[id] = newCacheItem;
        }

        protected void RemoveItemFromCache(Guid id)
        {
            Cache.TryRemove(id, out TItem i);
        }

        /// <summary>
        /// Derived classes should define simple instantiation methods, <para/> 
        /// saving performance vs Activator.CreateInstance
        /// </summary>
        protected virtual TItem New(SearchResult r)
        {
            return (TItem)Activator.CreateInstance(typeof(TItem), r);
        }

        /// <summary>
        /// Base FillCache method appropriate for most derived caches
        /// </summary>
        public virtual void FillCache()
        {
            var searcher = _examineManager.SearchProviderCollection[_config.ExamineSearcher];

            if (searcher != null && !string.IsNullOrEmpty(NodeAlias))
            {
                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                _log.Info("Starting to fill...");

                var count = 0;

                ISearchCriteria searchCriteria = searcher.CreateSearchCriteria();
                var query = searchCriteria.NodeTypeAlias(NodeAlias);
                var results = searcher.Search(query.Compile());

                foreach (var r in results)
                {
                    try
                    {
                        // Traverse up parent nodes, checking only published status
                        if (!r.IsItemUnpublished())
                        {
                            var item = New(r);

                            if (item != null)
                            {
                                count++;

                                var itemKey = Guid.Parse(r.Fields["key"]);
                                AddOrReplaceFromCache(itemKey, item);
                            }
                        }
                    }
                    catch (Exception ex) // Skip on fail
                    {
                        _log.Info("Failed to map to store. Id: " + r.Id, ex);
                    }
                }

                stopwatch.Stop();

                _log.Info("Finished filling base cache with " + count + " items. Time it took to fill: " + stopwatch.Elapsed);
            }
            else
            {
                _log.Info("No examine search found with the name ExternalSearcher, Can not fill category cache.");
            }
        }

        /// <summary>
        /// <see cref="ICache"/> implementation, <para/>
        /// handles addition of nodes when umbraco events fire
        /// </summary>
        public virtual void AddReplace(IContent node)
        {
            if (!node.IsItemUnpublished())
            {
                var item = (TItem)Activator.CreateInstance(typeof(TItem), node);

                if (item != null) AddOrReplaceFromCache(node.Key, item);
            }
        }

        /// <summary>
        /// <see cref="ICache"/> implementation, <para/>
        /// handles removal of nodes when umbraco events fire
        /// </summary>
        public virtual void Remove(Guid id)
        {
            RemoveItemFromCache(id);
        }
    }
}

using Examine;
using Examine.SearchCriteria;
using log4net;
using Microsoft.Practices.Unity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    /// For custom caches or global non store dependant caches
    /// </summary>
    /// <typeparam name="TItem">Type of data to cache</typeparam>
    public abstract class BaseCache<TItem> : ICache, IBaseCache<TItem>
    {
        protected Configuration _config;
        protected ExamineManager _examineManager;
        protected ILog _log;

        /// <summary>
        /// Umbraco Node Alias name used in Examine search
        /// </summary>
        public abstract string nodeAlias { get; }

        public ConcurrentDictionary<int, TItem> Cache { get; }
         = new ConcurrentDictionary<int, TItem>();


        protected void AddOrReplaceFromCache(int id, TItem newCacheItem)
        {
            Cache[id] = newCacheItem;
        }

        protected void RemoveItemFromCache(int id)
        {
            TItem i = default(TItem);
            Cache.TryRemove(id, out i);
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

            if (searcher != null && !string.IsNullOrEmpty(nodeAlias))
            {
                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                _log.Info("Starting to fill...");

                var count = 0;

                try
                {
                    ISearchCriteria searchCriteria = searcher.CreateSearchCriteria();
                    var query = searchCriteria.NodeTypeAlias(nodeAlias);
                    var results = searcher.Search(query.Compile());

                    foreach (var r in results.Where(x => x.Fields["template"] != "0"))
                    {
                        // Traverse up parent nodes, checking only published status
                        if (!r.IsItemUnpublished())
                        {
                            var item = New(r);

                            if (item != null)
                            {
                                count++;
                                AddOrReplaceFromCache(r.Id, item);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.Error("Filling Base Cache Failed!", ex);
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
        /// <see cref="ICache"/> implementation, <para/>
        /// handles addition of nodes when umbraco events fire
        /// </summary>
        public virtual void AddReplace(IContent node)
        {
            if (!node.IsItemUnpublished())
            {
                var item = (TItem) Activator.CreateInstance(typeof(TItem), node);

                if (item != null) AddOrReplaceFromCache(node.Id, item);
            }
        }

        /// <summary>
        /// <see cref="ICache"/> implementation, <para/>
        /// handles removal of nodes when umbraco events fire
        /// </summary>
        public virtual void Remove(int id)
        {
            RemoveItemFromCache(id);
        }
    }
}

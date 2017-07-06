using Examine;
using Examine.SearchCriteria;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Umbraco.Core.Models;
using uWebshop.Helpers;
using uWebshop.Models;

namespace uWebshop.Cache
{
    /// <summary>
    /// For custom caches or global non store dependant caches
    /// </summary>
    /// <typeparam name="TItem">Type of data to cache</typeparam>
    /// <typeparam name="Tself">The inheriting classes type</typeparam>
    public abstract class BaseCache<TItem, Tself> : ICache
            where Tself : BaseCache<TItem, Tself>, new()
    {
        /// <summary>
        /// Retrieve the singletons Cache
        /// </summary>
        public static ConcurrentDictionary<int, TItem> Cache { get { return Instance._cache; } }

        /// <summary>
        /// Singleton
        /// </summary>
        public static Tself Instance { get; } = new Tself();


        /// <summary>
        /// Umbraco Node Alias name used in Examine search
        /// </summary>
        protected abstract string nodeAlias { get; }

        private ConcurrentDictionary<int, TItem> _cache
          = new ConcurrentDictionary<int, TItem>();


        public void AddOrReplaceFromCache(int id, TItem newCacheItem)
        {
            _cache[id] = newCacheItem;
        }

        public void RemoveItemFromCache(int id)
        {
            TItem i = default(TItem);
            _cache.TryRemove(id, out i);
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
            var searcher = ExamineManager.Instance.SearchProviderCollection[Configuration.ExamineSearcher];

            if (searcher != null && !string.IsNullOrEmpty(nodeAlias))
            {
                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                Log.Info("Starting to fill " + typeof(Tself).FullName + "...");

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
                } catch (Exception ex)
                {
                    Log.Error("Filling Base Cache Failed!", ex);
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

        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}

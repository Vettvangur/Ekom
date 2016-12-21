﻿using Examine;
using Examine.SearchCriteria;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using uWebshop.Helpers;
using uWebshop.Models;

namespace uWebshop.Cache
{
    /// <summary>
    /// For custom caches or global non store dependant caches
    /// </summary>
    /// <typeparam name="T1">Type of data to cache</typeparam>
    /// <typeparam name="T2">The inheriting classes type</typeparam>
    public abstract class BaseCache<T1, T2>
                    where T2 : BaseCache<T1, T2>, new()
    {
        /// <summary>
        /// Retrieve the singletons Cache
        /// </summary>
        public static ConcurrentDictionary<int, T1> Cache { get { return Instance._cache; } }

        /// <summary>
        /// Singleton
        /// </summary>
        public static T2 Instance { get; } = new T2();


        /// <summary>
        /// Umbraco Node Alias name used in Examine search
        /// </summary>
        protected abstract string nodeAlias { get; }

        private ConcurrentDictionary<int, T1> _cache
          = new ConcurrentDictionary<int, T1>();


        public void AddOrReplaceFromCache(int id, T1 newCacheItem)
        {
            _cache[id] = newCacheItem;
        }

        public void RemoveItemFromCache(int id)
        {
            T1 i = default(T1);
            _cache.TryRemove(id, out i);
        }

        /// <summary>
        /// Derived classes define simple instantiation methods, <para/> 
        /// saving performance vs Activator.CreateInstance
        /// </summary>
        protected virtual T1 New(SearchResult r)
        {
            return (T1)Activator.CreateInstance(typeof(T1), r);
        }

        /// <summary>
        /// Base Fill cache method appropriate for most derived caches
        /// </summary>
        public virtual void FillCache()
        {
            var searcher = ExamineManager.Instance.SearchProviderCollection["ExternalSearcher"];

            if (searcher != null && !string.IsNullOrEmpty(nodeAlias))
            {
                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                Log.Info("Starting to fill " +
                    MethodBase.GetCurrentMethod().DeclaringType.FullName + "...");

                ISearchCriteria searchCriteria = searcher.CreateSearchCriteria();
                var query   = searchCriteria.NodeTypeAlias(nodeAlias);
                var results = searcher.Search(query.Compile());
                var count   = 0;

                foreach (var r in results.Where(x => x.Fields["template"] != "0"))
                {
                    try
                    {
                        // Traverse up parent nodes, checking disabled status
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
                    catch
                    {
                        // Skip on fail
                        Log.Info("Failed adding item with id: " + r.Id);
                    }
                }

                stopwatch.Stop();

                Log.Info("Finished filling category cache with " + count + " items. Time it took to fill: " + stopwatch.Elapsed);
            }
            else
            {
                Log.Info("No examine search found with the name ExternalSearcher, Can not fill category cache.");
            }
        }

        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}

using Examine;
using Examine.SearchCriteria;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using uWebshop.Helpers;
using uWebshop.Models;

namespace uWebshop.Cache
{
    public abstract class PerStoreCache<T1, T2> where T2 : PerStoreCache<T1, T2>, new()
    {
        /// <summary>
        /// Retrieve the singletons Cache
        /// </summary>
        public static ConcurrentDictionary<string, ConcurrentDictionary<int, T1>> 
                        Cache { get { return Instance._cache; } }

        /// <summary>
        /// Singleton
        /// </summary>
        public static T2 Instance { get; } = new T2();


        /// <summary>
        /// Umbraco Node Alias name used in Examine search
        /// </summary>
        protected abstract string nodeAlias { get; }

        /// <summary>
        /// Concurrent dictionaries per store
        /// </summary>
        private ConcurrentDictionary<string, ConcurrentDictionary<int, T1>> _cache
          = new ConcurrentDictionary<string, ConcurrentDictionary<int, T1>>();

        /// <summary>
        /// Derived classes define simple instantiation methods, 
        /// saving performance vs Activator.CreateInstance
        /// </summary>
        protected abstract T1 New(SearchResult r, Store store);

        /// <summary>
        /// Base Fill cache method appropriate for most derived caches
        /// </summary>
        public void FillCache()
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

                foreach (var store in StoreCache.Cache.Select(x => x.Value))
                {
                    _cache[store.Alias] = new ConcurrentDictionary<int, T1>();

                    var curStoreCache = _cache[store.Alias];

                    foreach (var r in results.Where(x => x.Fields["template"] != "0"))
                    {
                        try
                        {
                            // Traverse up parent nodes, checking disabled status
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
                        catch
                        {
                            // Skip on fail
                            Log.Info("Failed adding item with id: " + r.Id);
                        }
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

        public void AddOrReplaceFromCache(int id, Store store, T1 newCacheItem)
        {
            _cache[store.Alias][id] = newCacheItem;
        }

        public bool RemoveItemFromCache(Store store, int id)
        {
            T1 i = default(T1);
            return _cache[store.Alias].TryRemove(id, out i);
        }

        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}

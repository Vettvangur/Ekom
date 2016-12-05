using Examine;
using Examine.SearchCriteria;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using uWebshop.Models;

namespace uWebshop.Cache
{
    public abstract class BaseCache<Type>
    {
        public abstract string nodeAlias { get; set; }

        public ConcurrentDictionary<string, Type> _cache 
         = new ConcurrentDictionary<string, Type>();

        // Fill cache
        public void FillCache(Func<SearchResult, Store, Type> CreateItemFromExamine)
        {
            var searcher = ExamineManager.Instance.SearchProviderCollection["ExternalSearcher"];

            if (searcher != null && !string.IsNullOrEmpty(nodeAlias))
            {
                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                Log.Info("Starting to fill " + 
                    MethodBase.GetCurrentMethod().DeclaringType.FullName + "...");

                ISearchCriteria searchCriteria = searcher.CreateSearchCriteria();
                var query = searchCriteria.NodeTypeAlias(nodeAlias);
                var results = searcher.Search(query.Compile());
                var count = 0;

                foreach (var store in StoreCache.Instance._cache.Select(x => x.Value))
                {
                    foreach (var r in results.Where(x => x.Fields["template"] != "0"))
                    {
                        var item = CreateItemFromExamine(r, store);

                        if (item != null)
                        {
                            count++;
                            AddOrUpdateCache(r.Id, store, item);
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

        /// <summary>
        /// Add or Update item in cache
        /// </summary>
        public void AddOrUpdateCache(int id, Store store, Type newCacheItem)
        {
            string cacheKey = id.ToString() + "-" + store.Alias;

            _cache.AddOrUpdate(
                cacheKey,
                newCacheItem,
                (key, oldCacheItem) => newCacheItem);
        }

        /// <summary>
        /// Remove item from cache
        /// </summary>
        public void RemoveItemFromCache(string id)
        {
            Type i = default(Type);
            var remove = _cache.TryRemove(id, out i);
        }

        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}

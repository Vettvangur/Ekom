using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models.Abstractions;
using Examine.SearchCriteria;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Umbraco.Core.Models;

namespace Ekom.Cache
{
    /// <summary>
    /// For custom caches or global non store dependant caches
    /// </summary>
    /// <typeparam name="TItem">Type of data to cache</typeparam>
    abstract class BaseCache<TItem> : ICache, IBaseCache<TItem>
        where TItem : class
    {
        protected Configuration _config;
        protected ILog _log;
        protected IObjectFactory<TItem> _objFac;

        protected ExamineManagerBase _examineManager => Configuration.container.GetInstance<ExamineManagerBase>();

        public BaseCache(
            Configuration config,
            IObjectFactory<TItem> objectFactory
        )
        {
            _config = config;
            _objFac = objectFactory;
        }

        /// <summary>
        /// Umbraco Node Alias name used in Examine search
        /// </summary>
        public abstract string NodeAlias { get; }

        public virtual ConcurrentDictionary<Guid, TItem> Cache { get; }
         = new ConcurrentDictionary<Guid, TItem>();

        /// <summary>
        /// Class indexer
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public TItem this[Guid index]
        {
            get => Cache[index];
            set => Cache[index] = value;
        }

        protected void AddOrReplaceFromCache(Guid id, TItem newCacheItem)
        {
            Cache[id] = newCacheItem;
        }

        protected void RemoveItemFromCache(Guid id)
        {
            Cache.TryRemove(id, out TItem i);
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

                _log.Debug("Starting to fill...");

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
                            var item = (TItem)(_objFac?.Create(r) ?? Activator.CreateInstance(typeof(TItem), r));

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
                        _log.Warn("Failed to map to store. Id: " + r.Id, ex);
                    }
                }

                stopwatch.Stop();

                _log.Debug("Finished filling base cache with " + count + " items. Time it took to fill: " + stopwatch.Elapsed);
            }
            else
            {
                _log.Error($"No examine search found with the name {_config.ExamineSearcher}, Can not fill category cache.");
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
                var item = (TItem)(_objFac?.Create(node) ?? Activator.CreateInstance(typeof(TItem), node));

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

using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;

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
        protected ILogger _logger;
        protected IObjectFactory<TItem> _objFac;
        protected IServiceProvider _serviceProvider;

        protected INodeService nodeService => _serviceProvider.GetService<INodeService>();

        public BaseCache(
            Configuration config,
            ILogger<BaseCache<TItem>> logger,
            IObjectFactory<TItem> objectFactory,
            IServiceProvider serviceProvider)
        {
            _config = config;
            _logger = logger;
            _objFac = objectFactory;
            _serviceProvider = serviceProvider;
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
            if (!string.IsNullOrEmpty(NodeAlias))
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                _logger.LogDebug("Starting to fill...");

                var count = 0;

                var results = nodeService.NodesByTypes(NodeAlias);

                foreach (var r in results)
                {
                    try
                    {
                        // Traverse up parent nodes, checking only published status
                        //if (!r.IsItemUnpublished())
                        //{
                        var item = (TItem)(_objFac?.Create(r) ?? Activator.CreateInstance(typeof(TItem), r));

                        if (item != null)
                        {
                            count++;

                            AddOrReplaceFromCache(r.Key, item);
                        }
                        //}
                    }
                    catch (Exception ex) // Skip on fail
                    {
                        _logger.LogWarning(ex, "Failed to map to store. Id: {Id}" + r.Id);
                    }
                }

                stopwatch.Stop();
                _logger.LogInformation(
                    "Finished filling base cache with {Count} items. Time it took to fill: {Elapsed}", count, stopwatch.Elapsed);
            }
            else
            {
                _logger.LogError(
                    "No examine search found with the name {ExamineIndex}, Can not fill cache.", _config.ExamineIndex);
            }
        }

        /// <summary>
        /// <see cref="ICache"/> implementation, <para/>
        /// handles addition of nodes when umbraco events fire
        /// </summary>
        public virtual void AddReplace(UmbracoContent content)
        {
            if (!nodeService.IsItemUnpublished(content))
            {
                var item = (TItem)(_objFac?.Create(content) ?? Activator.CreateInstance(typeof(TItem), content));

                if (item != null) AddOrReplaceFromCache(content.Key, item);
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

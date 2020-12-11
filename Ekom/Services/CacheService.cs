using Ekom.Models;
using Ekom.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using Umbraco.Core.Cache;

namespace Ekom.Services
{
    /// <summary>
    /// Abstracts access to RuntimeCache / AppPolicyCache / System.Web.Caching.Cache
    /// </summary>
    class CacheService
    {
        const string _prefix = "ekmOrder-";

        readonly IAppPolicyCache _runtimeCache;
        readonly IAppCache _reqCache;

        public CacheService(AppCaches appCaches)
        {
            _runtimeCache = appCaches.RuntimeCache;
            _reqCache = appCaches.RequestCache;
        }

        public ContentRequest GetContentRequest() => _reqCache.GetCacheItem<ContentRequest>("ekmRequest");

        public T GetItem<T>(string cacheKey) 
            => _runtimeCache.GetCacheItem<T>(_prefix + cacheKey);
        public T GetItem<T>(
            string cacheKey, 
            Func<T> getCacheItem
        ) => _runtimeCache.GetCacheItem(_prefix + cacheKey, getCacheItem);

        public T GetItem<T>(
            string cacheKey, 
            Func<T> getCacheItem,
            TimeSpan? timeout,
            bool isSliding = false,
            CacheItemPriority priority = CacheItemPriority.Normal,
            CacheItemRemovedCallback removedCallback = null,
            string[] dependentFiles = null
        ) => _runtimeCache.GetCacheItem(_prefix + cacheKey, getCacheItem, timeout, isSliding, priority, removedCallback, dependentFiles);

        public void InsertCacheItem<T>(
            string cacheKey, 
            Func<T> getCacheItem,
            TimeSpan? timeout = null,
            bool isSliding = false,
            CacheItemPriority priority = CacheItemPriority.Normal,
            CacheItemRemovedCallback removedCallback = null,
            string[] dependentFiles = null
        ) => _runtimeCache.InsertCacheItem(_prefix + cacheKey, getCacheItem, timeout, isSliding, priority, removedCallback, dependentFiles);

        public void RemoveItem(string key) 
            => _runtimeCache.ClearByKey(_prefix + key);
    }
}

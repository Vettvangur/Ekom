using Ekom.Models;
using System;
using System.Collections.Concurrent;

namespace Ekom.Cache
{
    /// <summary>
    /// Used to differentiate per store caches from base caches at runtime.
    /// The store cache looks at the list of caches when it is updated and runs through them.
    /// Updating all per store caches.
    /// </summary>
    interface IPerStoreCache : ICache
    {
        /// <summary>
        /// Handles initial population of cache data
        /// </summary>
        void FillCache(IStore store);
    }

    /// <summary>
    /// Used to register per store caches with unity
    /// </summary>
    /// <typeparam name="T"></typeparam>
    interface IPerStoreCache<T> : ICache, IPerStoreCache
    {
        /// <summary>
        /// 
        /// </summary>
        ConcurrentDictionary<string, ConcurrentDictionary<Guid, T>> Cache { get; }
        /// <summary>
        /// Class indexer
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        ConcurrentDictionary<Guid, T> this[string index] { get; }
    }
}

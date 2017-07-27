using Examine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Models;

namespace uWebshop.Cache
{
    /// <summary>
    /// Used to differentiate per store caches from base caches at runtime.
    /// The store cache looks at the list of caches when it is updated and runs through them.
    /// Updating all per store caches.
    /// </summary>
    public interface IPerStoreCache : ICache
    {
        void FillCache(Store store);
    }

    /// <summary>
    /// Used to register per store caches with unity
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IPerStoreCache<T> : ICache, IPerStoreCache
    {
        ConcurrentDictionary<string, ConcurrentDictionary<int, T>> Cache { get; }
    }
}

using System;
using System.Collections.Concurrent;

namespace Ekom.Cache
{
    /// <summary>
    /// For custom caches or global non store dependant caches
    /// </summary>
    /// <typeparam name="T">Type of data to cache</typeparam>
    public interface IBaseCache<T> : ICache
    {
        /// <summary>
        /// 
        /// </summary>
        ConcurrentDictionary<Guid, T> Cache { get; }

        /// <summary>
        /// Class indexer
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        T this[Guid index] { get; set; }
    }
}

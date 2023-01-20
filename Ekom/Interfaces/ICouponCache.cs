using Ekom.Models;
using System.Collections.Concurrent;

namespace Ekom.Interfaces
{
    /// <summary>
    /// The coupon cache
    /// A database based cache, similar to the <see cref="StockCache"/>
    /// </summary>
    public interface ICouponCache
    {
        /// <summary>
        /// Gets the cache.
        /// </summary>
        /// <value>
        /// The cache.
        /// </value>
        ConcurrentDictionary<string, CouponData> Cache { get; }

        /// <summary>
        /// This method only serves to conform with the other caches
        /// </summary>
        void AddReplace(CouponData coupon);

        /// <summary>
        /// Handles initial population of cache data
        /// </summary>
        void FillCache();
        /// <summary>
        /// This method only serves to conform with the other caches
        /// </summary>
        void Remove(CouponData coupon);
    }
}

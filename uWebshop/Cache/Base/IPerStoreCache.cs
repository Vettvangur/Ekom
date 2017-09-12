using System;
using System.Collections.Concurrent;
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
		/// <summary>
		/// Handles initial population of cache data
		/// </summary>
		void FillCache(Store store);
	}

	/// <summary>
	/// Used to register per store caches with unity
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public interface IPerStoreCache<T> : ICache, IPerStoreCache
	{
		/// <summary>
		/// 
		/// </summary>
		ConcurrentDictionary<string, ConcurrentDictionary<Guid, T>> Cache { get; }
	}
}

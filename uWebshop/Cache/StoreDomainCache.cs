using System.Diagnostics;
using System.Linq;
using Umbraco.Core.Models;
using uWebshop.Services;

namespace uWebshop.Cache
{
    public class StoreDomainCache : BaseCache<IDomain>
    {
        public static StoreDomainCache Instance { get; } = new StoreDomainCache();

        public override string nodeAlias { get; set; } = "notApplicable";

        /// <summary>
        /// Fill store domain cache with domains from domain service
        /// </summary>
        public void FillCache()
        {
            var domains = StoreService.GetAllStoreDomains();

            if (domains.Any())
            {
                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                Log.Info("Starting to fill store domain cache...");

                foreach (var d in domains)
                {
                    AddOrUpdateCache(d.Id, d);
                }

                Log.Info("Finished filling store domain cache with " + domains.Count() + " domain items. Time it took to fill: " + stopwatch.Elapsed);
            }
        }

        public void AddOrUpdateCache(int id, IDomain newCacheItem)
        {
            string cacheKey = id.ToString();

            _cache.AddOrUpdate(
                cacheKey,
                newCacheItem,
                (key, oldCacheItem) => newCacheItem);
        }
    }
}

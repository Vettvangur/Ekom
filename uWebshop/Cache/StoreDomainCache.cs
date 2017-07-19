using System;
using System.Diagnostics;
using System.Linq;
using Examine;
using Umbraco.Core.Models;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshop.Cache
{
    public class StoreDomainCache : BaseCache<IDomain, StoreDomainCache>
    {
        protected override string nodeAlias { get; } = "Does not apply";

        /// <summary>
        /// Fill store domain cache with domains from domain service
        /// </summary>
        public override void FillCache()
        {
            var domains = StoreService.GetAllStoreDomains();

            if (domains.Any())
            {
                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                _log.Info("Starting to fill store domain cache...");

                foreach (var d in domains)
                {
                    AddOrReplaceFromCache(d.Id, d);
                }

                _log.Info("Finished filling store domain cache with " + domains.Count() + " domain items. Time it took to fill: " + stopwatch.Elapsed);
            }
        }
    }
}

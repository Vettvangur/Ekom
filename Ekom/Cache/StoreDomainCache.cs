using System;
using Examine;
using System.Diagnostics;
using System.Linq;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Ekom.Services;
using Umbraco.Core.Services;
using Umbraco.Core.Composing;

namespace Ekom.Cache
{
    class StoreDomainCache : BaseCache<IDomain>
    {
        public override string NodeAlias { get; } = "";

        IDomainService _domainService;
        /// <summary>
        /// ctor
        /// </summary>
        public StoreDomainCache(
            Configuration config,
            ILogger logger,
            IFactory factory,
            IDomainService domainService
        ) : base(config, logger, factory, null)
        {
            _domainService = domainService;
        }

        /// <summary>
        /// Fill store domain cache with domains from domain service
        /// </summary>
        public override void FillCache()
        {
            var domains = _domainService.GetAll(false).ToList();

            var stopwatch = new Stopwatch();

            stopwatch.Start();

            _logger.Info<StoreDomainCache>("Starting to fill store domain cache...");

            if (domains.Any())
            {
                foreach (var d in domains)
                {
                    AddOrReplaceFromCache(d.Key, d);
                }
            }

            _logger.Info<StoreDomainCache>(
                $"Finished filling store domain cache with {domains.Count()} domain items. Time it took to fill: {stopwatch.Elapsed}"
            );
        }
    }
}

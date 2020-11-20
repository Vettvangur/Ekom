using Ekom.Interfaces;
using System.Diagnostics;
using System.Linq;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Services;

namespace Ekom.Cache
{
    class StoreDomainCache : BaseCache<IDomain>, IStoreDomainCache
    {
        public override string NodeAlias { get; } = "";

        readonly IDomainService _domainService;
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
                "Finished filling store domain cache with {Count} domain items. Time it took to fill: {Elapsed}",
                domains.Count,
                stopwatch.Elapsed
            );
        }

        /// <inheritdoc />
        public void AddReplace(IDomain domain)
        {
            Cache[domain.Key] = domain;
        }
    }
}

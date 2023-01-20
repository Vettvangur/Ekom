using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;


namespace Ekom.Cache
{
    class StoreDomainCache : BaseCache<UmbracoDomain>, IStoreDomainCache
    {
        public override string NodeAlias { get; } = "";

        protected IUmbracoService umbracoService => _serviceProvider.GetService<IUmbracoService>();

        /// <summary>
        /// ctor
        /// </summary>
        public StoreDomainCache(
            Configuration config,
            ILogger<BaseCache<UmbracoDomain>> logger,
            IServiceProvider serviceProvider
        ) : base(config, logger, null, serviceProvider)
        {
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// Fill store domain cache with domains from domain service
        /// </summary>
        public override void FillCache()
        {
            var domains = umbracoService.GetDomains().ToList();

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            _logger.LogInformation("Starting to fill store domain cache...");

            if (domains.Any())
            {
                foreach (var d in domains)
                {
                    AddOrReplaceFromCache(d.Key, d);
                }
            }

            _logger.LogInformation(
                "Finished filling store domain cache with {Count} domain items. Time it took to fill: {Elapsed}",
                domains.Count,
                stopwatch.Elapsed
            );
        }

        /// <inheritdoc />
        public void AddReplace(UmbracoDomain domain)
        {
            Cache[domain.Key] = domain;
        }
    }
}

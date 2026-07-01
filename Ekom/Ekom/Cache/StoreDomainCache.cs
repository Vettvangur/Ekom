using Ekom.Models;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Ekom.Cache;

class StoreDomainCache : BaseCache<UmbracoDomain>, IStoreDomainCache
{
    // This cache is not filled via NodesByTypes, so leave NodeAlias empty.
    public override string NodeAlias { get; } = "";

    public StoreDomainCache(
        Configuration config,
        ILogger<BaseCache<UmbracoDomain>> logger,
        IServiceProvider serviceProvider
    ) : base(config, logger, null, serviceProvider)
    {
    }
    public override void FillCache()
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var umbracoService = scope.ServiceProvider.GetRequiredService<IUmbracoService>();
            List<UmbracoDomain> domains = umbracoService.GetDomains().ToList();

            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation("Starting to fill store domain cache with {Count} domains...", domains.Count);

            foreach (var d in domains)
            {
                if (d == null) continue;

                AddOrReplaceFromCache(d.Key, d);
            }

            stopwatch.Stop();
            _logger.LogInformation(
                "Finished filling store domain cache with {Count} domain items. Time it took to fill: {Elapsed}",
                domains.Count,
                stopwatch.Elapsed
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StoreDomainCache: Error filling cache");
        }
    }

    /// <inheritdoc />
    public void AddReplace(UmbracoDomain domain)
    {
        if (domain == null) return;

        AddOrReplaceFromCache(domain.Key, domain);
    }
}

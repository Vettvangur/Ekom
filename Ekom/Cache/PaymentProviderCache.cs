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
    class PaymentProviderCache : PerStoreCache<IPaymentProvider>
    {
        public override string NodeAlias { get; } = "netPaymentProvider";

        public PaymentProviderCache(
            Configuration config,
            ILogger<IPerStoreCache<IPaymentProvider>> logger,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<IPaymentProvider> perStoreFactory,
            IServiceProvider serviceProvider
        ) : base(config, logger, storeCache, perStoreFactory, serviceProvider)
        {
        }

        public override void FillCache(IStore storeParam = null)
        {
            if (!string.IsNullOrEmpty(NodeAlias))
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                _logger.LogDebug("Starting to fill...");

                int count = 0;

                try
                {
                    var paymentProviderRoot = nodeService.NodesByTypes("netPaymentProviders").FirstOrDefault();

                    if (paymentProviderRoot == null)
                    {
                        throw new Exception("Ekom payment providers node not found.");
                    }

                    var results = nodeService.NodeChildren(paymentProviderRoot.Id.ToString()).ToList();

                    if (storeParam == null) // Startup initialization
                    {
                        foreach (var store in _storeCache.Cache.Select(x => x.Value))
                        {
                            count += FillStoreCache(store, results);
                        }
                    }
                    else // Triggered with dynamic addition/removal of store
                    {
                        count += FillStoreCache(storeParam, results);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Filling per store cache Failed for {NodeAlias}!", NodeAlias);
                }

                stopwatch.Stop();
                _logger.LogInformation(
                    "Finished filling per store cache with {Count} items for {NodeAlias}. Time it took to fill: {Elapsed}",
                    count,
                    NodeAlias,
                    stopwatch.Elapsed
                );
            }
            else
            {
                _logger.LogError(
                    "No examine search found with the name {ExamineIndex}, Can not fill cache.",
                    _config.ExamineIndex
                );
            }
        }
    }
}

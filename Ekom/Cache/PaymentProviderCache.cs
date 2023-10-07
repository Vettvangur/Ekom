using Ekom.Interfaces;
using Ekom.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Ekom.Cache
{
    class PaymentProviderCache : PerStoreCache<IPaymentProvider>
    {
        public override string NodeAlias { get; } = "ekmPaymentProvider";

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
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                _logger.LogDebug("Starting to fill...");

                int count = 0;

                try
                {
                    var paymentProviderRoot = nodeService.NodesByTypes("ekmPaymentProviders").FirstOrDefault();

                    if (paymentProviderRoot == null)
                    {
                        throw new Exception("Ekom payment providers node not found.");
                    }

                    var results = nodeService.NodeChildren(paymentProviderRoot.Id.ToString()).ToList();

                    if (storeParam == null) // Startup initialization
                    {
                        foreach (var store in _storeCache.Cache.Select(x => x.Value))
                        {
                            count += FillStoreCache(store, results, NodeAlias);
                        }
                    }
                    else // Triggered with dynamic addition/removal of store
                    {
                        count += FillStoreCache(storeParam, results, NodeAlias);
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
                    "No NodeAlias, Can not fill cache."
                );
            }
        }
    }
}

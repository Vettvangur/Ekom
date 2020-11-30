using Ekom.Interfaces;
using Examine;
using System;
using System.Diagnostics;
using System.Linq;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Examine;

namespace Ekom.Cache
{
    class PaymentProviderCache : PerStoreCache<IPaymentProvider>
    {
        public override string NodeAlias { get; } = "netPaymentProvider";

        public PaymentProviderCache(
            Configuration config,
            ILogger logger,
            IFactory factory,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<IPaymentProvider> perStoreFactory
        ) : base(config, logger, factory, storeCache, perStoreFactory)
        {
        }

        public override void FillCache(IStore storeParam = null)
        {
            if (!string.IsNullOrEmpty(NodeAlias)
                   && ExamineManager.TryGetIndex(_config.ExamineIndex, out IIndex index))
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                var searcher = index.GetSearcher();

                _logger.Debug<PerStoreCache<IPaymentProvider>>("Starting to fill...");

                int count = 0;

                try
                {
                    ISearchResults results = null;

                    results = searcher.CreateQuery("content")
                    .Field("__NodeTypeAlias", NodeAlias.MultipleCharacterWildcard()).Not().NodeTypeAlias("netPaymentProviders")
                    .Execute(int.MaxValue);


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
                    _logger.Error<PerStoreCache<IPaymentProvider>>(ex, "Filling per store cache Failed!");
                }

                stopwatch.Stop();
                _logger.Info<PerStoreCache<IPaymentProvider>>(
                    "Finished filling per store cache with {Count} items. Time it took to fill: {Elapsed}",
                    count,
                    stopwatch.Elapsed
                );
            }
            else
            {
                _logger.Error<PerStoreCache<IPaymentProvider>>(
                    "No examine search found with the name {ExamineIndex}, Can not fill cache.",
                    _config.ExamineIndex
                );
            }
        }
    }
}

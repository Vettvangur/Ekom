using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Repository;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Umbraco.Core.Logging;

namespace Ekom.Cache
{
    class StockPerStoreCache : PerStoreCache<StockData>
    {
        readonly IStockRepository _stockRepo;
        public StockPerStoreCache(
            ILogger logger,
            Configuration config,
            IStockRepository stockRepo,
            IBaseCache<IStore> storeCache
        ) : base(config, logger, null, storeCache, null)
        {
            _stockRepo = stockRepo;

            _logger = logger;
        }

        public override ConcurrentDictionary<string, ConcurrentDictionary<Guid, StockData>> Cache
        {
            get
            {
                if (_config.PerStoreStock)
                {
                    return base.Cache;
                }

                throw new StockException(
                    "PerStoreStock configuration set to disabled, please configure PerStoreStock before accessing the cache."
                );
            }
        }

        public override string NodeAlias { get; } = "";

        public override void FillCache()
        {
#if DEBUG
            var stopwatch = new Stopwatch();
            stopwatch.Start();
#endif
            _logger.Info<StockPerStoreCache>("Starting to fill...");

            int count = 0;

            var allStock = _stockRepo.GetAllStockAsync().Result;
            var filteredStock = allStock.Where(stock => stock.UniqueId.Length == 39);

            foreach (var store in _storeCache.Cache.Select(x => x.Value))
            {
                count += FillStoreCache(store, filteredStock);
            }

#if DEBUG
            stopwatch.Stop();
            _logger.Info<StockPerStoreCache>(
                "Finished filling cache with {Count} items. Time it took to fill: {Elapsed}",
                count,
                stopwatch.Elapsed
            );
#else
            _logger.Debug<StockPerStoreCache>(
                "Finished filling cache with {Count} items.",
                count
            );
#endif
        }

        private int FillStoreCache(IStore store, IEnumerable<StockData> stockData)
        {
            int count = 0;

            Cache[store.Alias] = new ConcurrentDictionary<Guid, StockData>();

            var curStoreCache = Cache[store.Alias];

            foreach (var stock in stockData.Where(x => x.UniqueId.StartsWith(store.Alias)))
            {
                var stockIdSplit = stock.UniqueId.Split('_');

                var key = Guid.Parse(stockIdSplit[1]);

                curStoreCache[key] = stock;

                count++;
            }

            return count;
        }
    }
}

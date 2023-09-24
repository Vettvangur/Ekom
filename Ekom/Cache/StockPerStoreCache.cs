using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Repositories;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Ekom.Cache
{
    class StockPerStoreCache : PerStoreCache<StockData>
    {
        readonly StockRepository _stockRepo;
        public StockPerStoreCache(
            Configuration config,
            ILogger<IPerStoreCache<StockData>> logger,
            IBaseCache<IStore> storeCache,
            StockRepository stockRepo,
            IServiceProvider serviceProvider
        ) : base(config, logger, storeCache, null, serviceProvider)
        {
            _stockRepo = stockRepo;
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
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            _logger.LogInformation("Starting to fill stock cache...");

            int count = 0;

            var allStock = _stockRepo.GetAllStockAsync().Result;
            var filteredStock = allStock.Where(stock => stock.UniqueId.Contains("_", StringComparison.InvariantCulture));

            foreach (var store in _storeCache.Cache.Select(x => x.Value))
            {
                count += FillStoreCache(store, filteredStock);
            }

            stopwatch.Stop();
            _logger.LogInformation(
                "Finished filling stock per store cache with {Count} items. Time it took to fill: {Elapsed}",
                count,
                stopwatch.Elapsed
            );
        }

        private int FillStoreCache(IStore store, IEnumerable<StockData> stockData)
        {
            int count = 0;

            Cache[store.Alias] = new ConcurrentDictionary<Guid, StockData>();

            var curStoreCache = Cache[store.Alias];

            foreach (var stock in stockData.Where(x => x.UniqueId.Split('_')[0].Equals(store.Alias, StringComparison.InvariantCulture)))
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

using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Repository;
using Ekom.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Ekom.Cache
{
    class StockPerStoreCache : PerStoreCache<StockData>
    {
        readonly StockRepository _stockRepo;
        public StockPerStoreCache(
            ILogFactory logFac,
            StockRepository stockRepo,
            IBaseCache<IStore> storeCache
        ) : base(null, storeCache, null)
        {
            _stockRepo = stockRepo;

            _log = logFac.GetLogger(typeof(StockPerStoreCache));
        }

        public override ConcurrentDictionary<string, ConcurrentDictionary<Guid, StockData>> Cache
        {
            get
            {
                if (_config.PerStoreStock)
                {
                    return base.Cache;
                }

                throw new StockException("PerStoreStock configuration set to disabled, please configure PerStoreStock before accessing the cache.");
            }
        }

        public override string NodeAlias { get; } = "";

        public override void FillCache()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            _log.Info("Starting to fill...");

            int count = 0;

            var allStock = _stockRepo.GetAllStock();
            var filteredStock = allStock.Where(stock => stock.UniqueId.Length == 39);

            foreach (var store in _storeCache.Cache.Select(x => x.Value))
            {
                count += FillStoreCache(store, filteredStock);
            }

            stopwatch.Stop();
            _log.Info("Finished filling cache with " + count + " items. Time it took to fill: " + stopwatch.Elapsed);
        }

        private int FillStoreCache(IStore store, IEnumerable<StockData> stockData)
        {
            int count = 0;

            Cache[store.Alias] = new ConcurrentDictionary<Guid, StockData>();

            var curStoreCache = Cache[store.Alias];

            foreach (var stock in stockData)
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

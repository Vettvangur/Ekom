using Examine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Ekom.Models;
using Ekom.Models.Data;
using Ekom.Repository;
using Ekom.Services;

namespace Ekom.Cache
{
    class StockPerStoreCache : PerStoreCache<StockData>
    {
        StockRepository _stockRepo;
        public StockPerStoreCache(
            ILogFactory logFac,
            StockRepository stockRepo,
            IBaseCache<Store> storeCache
        ) : base(null, null, storeCache)
        {
            _stockRepo = stockRepo;

            _log = logFac.GetLogger(typeof(StockPerStoreCache));
        }

        public override string NodeAlias { get; } = "";

        protected override StockData New(SearchResult r, Store store)
        {
            return null;
        }

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

        private int FillStoreCache(Store store, IEnumerable<StockData> stockData)
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

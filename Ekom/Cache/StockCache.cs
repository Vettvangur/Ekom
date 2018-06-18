using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Services;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;

namespace Ekom.Cache
{
    class StockCache : BaseCache<StockData>
    {
        IStockRepository _stockRepo;
        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="logFac"></param>
        /// <param name="stockRepo"></param>
        public StockCache(
            ILogFactory logFac,
            IStockRepository stockRepo
        ) : base(null, null)
        {
            _stockRepo = stockRepo;
            _log = logFac.GetLogger(typeof(StockCache));
        }

        public override ConcurrentDictionary<Guid, StockData> Cache
        {
            get
            {
                if (!_config.PerStoreStock)
                {
                    return base.Cache;
                }

                throw new StockException("PerStoreStock configuration enabled, please disable PerStoreStock before accessing this cache.");
            }
        }

        public override string NodeAlias { get; } = "";

        public override void FillCache()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            _log.Info("Starting to fill...");

            var allStock = _stockRepo.GetAllStock();
            foreach (var stock in allStock.Where(stock => stock.UniqueId.Length == 36))
            {
                var key = Guid.Parse(stock.UniqueId);

                Cache[key] = stock;
            }

            stopwatch.Stop();
            _log.Info("Finished filling cache with " + allStock.Count() + " items. Time it took to fill: " + stopwatch.Elapsed);
        }
    }
}

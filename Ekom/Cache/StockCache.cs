using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models.Data;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using Umbraco.Core.Logging;

namespace Ekom.Cache
{
    class StockCache : BaseCache<StockData>
    {
        readonly IStockRepository _stockRepo;
        /// <summary>
        /// ctor
        /// </summary>
        public StockCache(
            Configuration config,
            ILogger logger,
            IStockRepository stockRepo
        ) : base(config, logger, null, null)
        {
            _stockRepo = stockRepo;
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
#if DEBUG
            var stopwatch = new Stopwatch();
            stopwatch.Start();
#endif
            _logger.Info<StockCache>("Starting to fill...");

            var allStock = _stockRepo.GetAllStockAsync().Result;
            foreach (var stock in allStock.Where(stock => stock.UniqueId.Length == 36))
            {
                var key = Guid.Parse(stock.UniqueId);

                Cache[key] = stock;
            }

#if DEBUG
            stopwatch.Stop();
            _logger.Info<StockCache>(
                "Finished filling cache with {Count} items. Time it took to fill: {Elapsed}",
                allStock.Count,
                stopwatch.Elapsed);
#else
            _logger.Debug<StockCache>(
                "Finished filling cache with {Count} items.",
                allStock.Count);
#endif
        }
    }
}

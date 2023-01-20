using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;

namespace Ekom.Cache
{
    class StockCache : BaseCache<StockData>
    {
        readonly StockRepository _stockRepo;
        /// <summary>
        /// ctor
        /// </summary>
        public StockCache(
            Configuration config,
            ILogger<BaseCache<StockData>> logger,
            StockRepository stockRepo,
            IServiceProvider serviceProvider
        ) : base(config, logger, null, serviceProvider)
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
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            _logger.LogInformation("Starting to fill stock cache...");

            var allStock = _stockRepo.GetAllStockAsync().Result;
            foreach (var stock in allStock.Where(stock => stock.UniqueId.Length == 36))
            {
                var key = Guid.Parse(stock.UniqueId);

                Cache[key] = stock;
            }

            stopwatch.Stop();
            _logger.LogInformation(
                "Finished filling Stock cache with {Count} items. Time it took to fill: {Elapsed}",
                allStock.Count,
                stopwatch.Elapsed);
        }
    }
}

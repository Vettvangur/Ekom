using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Services;
using Examine;
using System;
using System.Diagnostics;
using System.Linq;
using Umbraco.Core.Models;

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
        ) : base(null, null, null)
        {
            _stockRepo = stockRepo;
            _log = logFac.GetLogger(typeof(StockCache));
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

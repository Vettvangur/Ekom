using System;
using System.Linq;
using uWebshop.Interfaces;
using uWebshop.Models.Data;
using uWebshop.Repository;
using uWebshop.Services;

namespace uWebshop.Cache
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
		)
		{
			_stockRepo = stockRepo;
			_log = logFac.GetLogger(typeof(StockCache));
		}

		public override string NodeAlias { get; } = "";

		public override void FillCache()
		{
			var allStock = _stockRepo.GetAllStock();
			foreach (var stock in allStock.Where(stock => stock.UniqueId.Length == 36))
			{
				var key = Guid.Parse(stock.UniqueId);

				Cache[key] = stock;
			}
		}
	}
}

using log4net;
using Microsoft.Practices.Unity;
using System;
using uWebshop.App_Start;
using uWebshop.Cache;
using uWebshop.Exceptions;
using uWebshop.Interfaces;
using uWebshop.Models.Data;
using uWebshop.Services;

namespace uWebshop.API
{
	/// <summary>
	/// The uWebshop API, get/update stock for item
	/// </summary>
	public class Stock
	{
		private static Stock _current;
		/// <summary>
		/// Stock Singleton
		/// </summary>
		public static Stock Current
		{
			get
			{
				return _current ?? (_current = UnityConfig.GetConfiguredContainer().Resolve<Stock>());
			}
		}

		ILog _log;

		IStoreService _storeSvc;
		IBaseCache<StockData> _stockCache;
		IPerStoreCache<StockData> _stockPerStoreCache;
		IStockRepository _stockRepo;
		Configuration _config;

		/// <summary>
		/// ctor
		/// </summary>
		public Stock(
			ILogFactory logFac,
			IBaseCache<StockData> stockCache,
			IPerStoreCache<StockData> stockPerStoreCache,
			Configuration config,
			IStockRepository stockRepo,
			IStoreService storeSvc
		)
		{
			_stockCache = stockCache;
			_stockPerStoreCache = stockPerStoreCache;
			_config = config;
			_storeSvc = storeSvc;
			_stockRepo = stockRepo;

			_log = logFac.GetLogger(typeof(Stock));
		}

		public int GetStock(Guid key)
		{
			return GetStockData(key).Stock;
		}

		public int GetStock(Guid key, Models.Store store)
		{
			return GetStockData(key, store).Stock;
		}

		public StockData GetStockData(Guid key)
		{
			if (_config.PerStoreStock)
			{
				var store = _storeSvc.GetStoreFromCache();

				return GetStockData(key, store);
			}
			else
			{
				return _stockCache.Cache[key];
			}
		}

		public StockData GetStockData(Guid key, Models.Store store)
		{
			return _stockPerStoreCache.Cache[store.Alias][key];
		}

		public void UpdateStock(Guid key, int value)
		{
			if (_config.PerStoreStock)
			{
				var store = _storeSvc.GetStoreFromCache();
				UpdateStock(key, store, value);
			}
			else
			{

			}
		}

		public void UpdateStock(Guid key, Models.Store store, int value)
		{
			var stockData = _stockPerStoreCache.Cache[store.Alias][key];

			if (stockData.Stock + value >= 0)
			{
				lock (stockData)
				{
					stockData.Stock += value;

					var uniqueId = $"{store.Alias}_{key}";

					var repoVal = _stockRepo.Update(uniqueId, value);

					if (repoVal != stockData.Stock)
					{
						// Memory always follows database data
						stockData.Stock = repoVal;

						throw new StockException($"Stock for item {uniqueId} is out of sync!.");
					}
				}
			}
		}
	}
}

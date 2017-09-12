using log4net;
using Microsoft.Practices.Unity;
using System;
using uWebshop.App_Start;
using uWebshop.Cache;
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
		Configuration _config;

		/// <summary>
		/// ctor
		/// </summary>
		public Stock(
			ILogFactory logFac,
			IBaseCache<StockData> stockCache,
			IPerStoreCache<StockData> stockPerStoreCache,
			Configuration config,
			IStoreService storeSvc
		)
		{
			_stockCache = stockCache;
			_stockPerStoreCache = stockPerStoreCache;
			_config = config;
			_storeSvc = storeSvc;

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

		public void UpdateStock(Guid key)
		{
			if (_config.PerStoreStock)
			{
				var store = _storeSvc.GetStoreFromCache();
				UpdateStock(key, store);
			}
			else
			{
			}
		}

		public void UpdateStock(Guid key, Models.Store store)
		{

		}
	}
}

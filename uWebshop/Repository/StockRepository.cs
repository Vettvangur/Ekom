using log4net;
using System;
using System.Collections.Generic;
using Umbraco.Core;
using uWebshop.Models.Data;
using uWebshop.Services;

namespace uWebshop.Repository
{
	/// <summary>
	/// Handles database transactions for <see cref="StockData"/>
	/// </summary>
	class StockRepository
	{
		ILog _log;
		DatabaseContext _dbCtx;
		/// <summary>
		/// ctor
		/// </summary>
		/// <param name="config"></param>
		/// <param name="dbCtx"></param>
		/// <param name="logFac"></param>
		public StockRepository(Configuration config, DatabaseContext dbCtx, ILogFactory logFac)
		{
			_dbCtx = dbCtx;
			_log = logFac.GetLogger(typeof(StockRepository));
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="uniqueId"></param>
		/// <returns></returns>
		public StockData GetStockByKey(Guid uniqueId)
		{
			using (var db = _dbCtx.Database)
			{
				return db.FirstOrDefault<StockData>("WHERE UniqueId = @0", uniqueId);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="uniqueId"></param>
		/// <param name="storeAlias"></param>
		/// <returns></returns>
		public StockData GetStockByKeyAndStore(Guid uniqueId, string storeAlias)
		{
			using (var db = _dbCtx.Database)
			{
				return db.FirstOrDefault<StockData>("WHERE UniqueId = @0", $"{storeAlias}_{uniqueId}");
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public IEnumerable<StockData> GetAllStock()
		{
			using (var db = _dbCtx.Database)
			{
				return db.Query<StockData>("");
			}
		}
	}
}

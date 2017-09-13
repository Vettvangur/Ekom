using log4net;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Web.Hosting;
using Umbraco.Core;
using uWebshop.Exceptions;
using uWebshop.Interfaces;
using uWebshop.Models.Data;
using uWebshop.Services;

namespace uWebshop.Repository
{
	/// <summary>
	/// Handles database transactions for <see cref="StockData"/>
	/// </summary>
	class StockRepository : IStockRepository
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
		/// <param name="uniqueId">
		/// Expects a value in the format
		/// $"{storeAlias}_{uniqueId}" for PerStore Stock
		/// Guid otherwise
		/// </param>
		/// <returns></returns>
		public StockData GetStockByUniqueId(string uniqueId)
		{
			using (var db = _dbCtx.Database)
			{
				var stockData = db.FirstOrDefault<StockData>("WHERE UniqueId = @0", uniqueId);

				return stockData ?? CreateNewStockRecord(uniqueId);
			}
		}

		public StockData CreateNewStockRecord(string uniqueId)
		{
			var dateNow = DateTime.Now;
			var stockData = new StockData
			{
				UniqueId = uniqueId,
				CreateDate = dateNow,
				UpdateDate = dateNow,
			};

			// Run synchronously to ensure that callers can expect a db record present after method runs
			using (var db = _dbCtx.Database)
			{
				db.Insert(stockData);
			}

			return stockData;
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

		/// <summary>
		/// Increment or decrement stock by the supplied value
		/// </summary>
		/// <param name="uniqueId"></param>
		/// <param name="value">This value can be negative or positive depending on whether the indended action is to increment or decrement stock</param>
		/// <returns></returns>
		public int Update(string uniqueId, int value)
		{
			var stockData = GetStockByUniqueId(uniqueId);

			if (stockData.Stock + value >= 0)
			{
				stockData.Stock += value;
				stockData.UpdateDate = DateTime.Now;

				// Called synchronously and hopefully contained by a locking construct
				using (var db = _dbCtx.Database)
				{
					db.Update(stockData);
					return stockData.Stock;
				}
			}
			else
			{
				throw new StockException($"Not enough stock available in database for {uniqueId}. This indicates that the database and cache are out of sync!");
			}
		}
	}
}

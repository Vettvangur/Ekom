using Ekom.Cache;
using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Services;
using Hangfire.States;
using log4net;
using System;

namespace Ekom.API
{
    /// <summary>
    /// The Ekom API, get/update stock for item
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
                return _current ?? (_current = Configuration.container.GetInstance<Stock>());
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

        /// <summary>
        /// Gets stock amount from cache. 
        /// If PerStoreStock is configured, gets store from cache.
        /// If no stock entry exists, creates a new one.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public int GetStock(Guid key)
        {
            return GetStockData(key).Stock;
        }

        /// <summary>
        /// Gets stock amount from store cache. 
        /// If no stock entry exists, creates a new one.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="storeAlias"></param>
        /// <returns></returns>
        public int GetStock(Guid key, string storeAlias)
        {
            return GetStockData(key, storeAlias).Stock;
        }

        /// <summary>
        /// Gets <see cref="StockData"/> from cache. 
        /// If PerStoreStock is configured, gets store from cache.
        /// If no stock entry exists, creates a new one.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public StockData GetStockData(Guid key)
        {
            if (_config.PerStoreStock)
            {
                var store = _storeSvc.GetStoreFromCache();

                return GetStockData(key, store.Alias);
            }
            else
            {
                EnsureStockEntryExists(key);

                return _stockCache.Cache[key];
            }
        }

        /// <summary>
        /// Gets <see cref="StockData"/> from store cache. 
        /// If no stock entry exists, creates a new one.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="storeAlias"></param>
        /// <returns></returns>
        public StockData GetStockData(Guid key, string storeAlias)
        {
            EnsurePerStoreEntryExists(key, storeAlias);

            return _stockPerStoreCache.Cache[storeAlias][key];
        }

        /// <summary>
        /// Updates stock count of item. 
        /// If PerStoreStock is configured, gets store from cache and updates relevant item.
        /// If no stock entry exists, creates a new one, the attempts to update.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public void UpdateStock(Guid key, int value)
        {
            if (_config.PerStoreStock)
            {
                var store = _storeSvc.GetStoreFromCache();
                UpdateStock(key, store.Alias, value);
            }
            else
            {
                EnsureStockEntryExists(key);

                var stockData = _stockCache.Cache[key];

                UpdateStockWithLock(stockData, value);
            }
        }

        /// <summary>
        /// Updates stock count of store item. 
        /// If no stock entry exists, creates a new one, the attempts to update.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="storeAlias"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public void UpdateStock(Guid key, string storeAlias, int value)
        {
            EnsurePerStoreEntryExists(key, storeAlias);

            var stockData = _stockPerStoreCache.Cache[storeAlias][key];

            UpdateStockWithLock(stockData, value);
        }

        /// <summary>
        /// Reserve stock for the given timespan.
        /// Rollback is scheduled using Hangfire
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value">Only accepts negative values to indicate amount of stock to decrement</param>
        /// <param name="timeSpan">How long to reserve</param>
        /// <returns>Hangfire Job Id</returns>
        public string ReserveStock(Guid key, int value, TimeSpan timeSpan)
        {
            if (value >= 0) throw new ArgumentOutOfRangeException();

            UpdateStock(key, value);

            var jobId = Hangfire.BackgroundJob.Schedule(() =>
                UpdateStockHangfire(key, -value),
                timeSpan
            );

            return jobId;
        }

        /// <summary>
        /// Reserve stock for the given timespan.
        /// Rollback is scheduled using Hangfire
        /// </summary>
        /// <param name="key"></param>
        /// <param name="storeAlias"></param>
        /// <param name="value">Only accepts negative values to indicate amount of stock to decrement</param>
        /// <param name="timeSpan">How long to reserve</param>
        /// <returns>Hangfire Job Id</returns>
        public string ReserveStock(Guid key, string storeAlias, int value, TimeSpan timeSpan)
        {
            if (value >= 0) throw new ArgumentOutOfRangeException();

            UpdateStock(key, storeAlias, value);

            var jobId = Hangfire.BackgroundJob.Schedule(() =>
                UpdateStockHangfire(key, storeAlias, -value),
                timeSpan
            );

            return jobId;
        }

        /// <summary>
        /// Cancel a previously scheduled stock reservation rollback.
        /// </summary>
        /// <param name="jobId"></param>
        public void CancelRollback(string jobId)
        {
            if (!Hangfire.BackgroundJob.Delete(jobId, ScheduledState.StateName)
            && !Hangfire.BackgroundJob.Delete(jobId, EnqueuedState.StateName))
            {
                throw new StockException(
                    "Unable to cancel rollback job, most likely the job has already finished"
                );
            }
        }

        /// <summary>
        /// Only requeues from scheduled state.
        /// Never requeues succeeded jobs.
        /// </summary>
        /// <param name="jobId">Hangfire Job Id</param>
        public void CompleteRollback(string jobId)
        {
            Hangfire.BackgroundJob.Requeue(
                jobId,
                ScheduledState.StateName
            );
        }

        private void EnsureStockEntryExists(Guid key)
        {
            if (!_stockCache.Cache.ContainsKey(key))
            {
                _stockCache.Cache[key]
                    = _stockRepo.CreateNewStockRecord(key.ToString());
            }
        }

        private void EnsurePerStoreEntryExists(Guid key, string storeAlias)
        {
            if (!_stockPerStoreCache.Cache[storeAlias].ContainsKey(key))
            {
                _stockPerStoreCache.Cache[storeAlias][key]
                    = _stockRepo.CreateNewStockRecord($"{storeAlias}_{key}");
            }
        }

        private void UpdateStockWithLock(StockData stockData, int value)
        {
            if (stockData.Stock + value >= 0)
            {
                lock (stockData)
                {
                    stockData.Stock += value;

                    var repoVal = _stockRepo.Update(stockData.UniqueId, value);

                    if (repoVal != stockData.Stock)
                    {
                        var prevCachedVal = stockData.Stock;

                        // Memory always follows database data
                        stockData.Stock = repoVal;

                        throw new StockException(
                            $"Stock for item {stockData.UniqueId} is out of sync!. Repo value {repoVal} != cache value {prevCachedVal}"
                        + "This indicates that the requested value + Db stock > 0 but Cache and Db are out of sync"
                        );
                    }
                }
            }
            else
            {
                throw new StockException($"Not enough stock available for {stockData.UniqueId}.");
            }
        }

        /// <summary>
        /// Allows hangfire to serialise the method call to database
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void UpdateStockHangfire(Guid key, int value)
        {
            Current.UpdateStock(key, value);
        }
        /// <summary>
        /// Allows hangfire to serialise the method call to database
        /// </summary>
        /// <param name="key"></param>
        /// <param name="storeAlias"></param>
        /// <param name="value"></param>
        public static void UpdateStockHangfire(Guid key, string storeAlias, int value)
        {
            Current.UpdateStock(key, storeAlias, value);
        }
    }
}

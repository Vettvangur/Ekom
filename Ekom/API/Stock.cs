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
    public partial class Stock
    {
        /// <summary>
        /// Stock Instance
        /// </summary>
        public static Stock Instance => Configuration.container.GetInstance<Stock>();

        ILog _log;
        Configuration _config;
        IStockRepository _stockRepo;
        IDiscountStockRepository _discountStockRepo;
        IStoreService _storeSvc;
        IBaseCache<StockData> _stockCache;
        IPerStoreCache<StockData> _stockPerStoreCache;

        /// <summary>
        /// ctor
        /// </summary>
        internal Stock(
            Configuration config,
            ILogFactory logFac,
            IBaseCache<StockData> stockCache,
            IStockRepository stockRepo,
            IDiscountStockRepository discountStockRepo,
            IStoreService storeService,
            IPerStoreCache<StockData> stockPerStoreCache
        )
        {
            _config = config;
            _stockCache = stockCache;
            _stockRepo = stockRepo;
            _discountStockRepo = discountStockRepo;
            _storeSvc = storeService;
            _stockPerStoreCache = stockPerStoreCache;

            _log = logFac.GetLogger<Stock>();
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
            return _stockCache.Cache.ContainsKey(key) ? GetStockData(key).Stock : 0;
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
            if (!string.IsNullOrEmpty(storeAlias))
            {
                if (_stockPerStoreCache.Cache.ContainsKey(storeAlias) && _stockPerStoreCache.Cache[storeAlias].ContainsKey(key))
                {
                    return GetStockData(key, storeAlias).Stock;
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                return GetStock(key);
            }
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
                //EnsureStockEntryExists(key);

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
            //EnsurePerStoreEntryExists(key, storeAlias);


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
        public void UpdateStock(Guid key, int value, bool increment = true)
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

                if (increment && stockData.Stock + value < 0)
                {
                    throw new StockException($"Not enough stock available for {stockData.UniqueId}.");
                }

                SetStockWithLock(stockData, (increment ? stockData.Stock + value : value));
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
        public void UpdateStock(Guid key, string storeAlias, int value, bool increment = true)
        {
            EnsurePerStoreEntryExists(key, storeAlias);

            var stockData = _stockPerStoreCache.Cache[storeAlias][key];

            if (increment && stockData.Stock + value < 0)
            {
                throw new StockException($"Not enough stock available for {stockData.UniqueId}.");
            }

            SetStockWithLock(stockData, (increment ? stockData.Stock + value : value));
        }

        /// <summary>
        /// Sets stock count of item. 
        /// If PerStoreStock is configured, gets store from cache and updates relevant item.
        /// If no stock entry exists, creates a new one, the attempts to update.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetStock(Guid key, int value)
        {
            if (_config.PerStoreStock)
            {
                var store = _storeSvc.GetStoreFromCache();
                return SetStock(key, store.Alias, value);
            }
            else
            {
                EnsureStockEntryExists(key);

                var stockData = _stockCache.Cache[key];

                return SetStockWithLock(stockData, value);
            }
        }

        /// <summary>
        /// Sets stock count of store item. 
        /// If no stock entry exists, creates a new one, the attempts to update.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="storeAlias"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetStock(Guid key, string storeAlias, int value)
        {
            EnsurePerStoreEntryExists(key, storeAlias);

            var stockData = _stockPerStoreCache.Cache[storeAlias][key];

            return SetStockWithLock(stockData, value);
        }

        /// <summary>
        /// Reserve stock for the given timespan.
        /// Rollback is scheduled using Hangfire
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value">Only accepts negative values to indicate amount of stock to decrement</param>
        /// <param name="timeSpan">How long to reserve</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <returns>Hangfire Job Id</returns>
        public string ReserveStock(Guid key, int value, TimeSpan timeSpan = default(TimeSpan))
        {
            if (value >= 0) throw new ArgumentOutOfRangeException();
            if (timeSpan == default(TimeSpan))
            {
                timeSpan = _config.ReservationTimeout;
            }

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
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <returns>Hangfire Job Id</returns>
        public string ReserveStock(Guid key, string storeAlias, int value, TimeSpan timeSpan = default(TimeSpan))
        {
            if (value >= 0) throw new ArgumentOutOfRangeException();
            if (timeSpan == default(TimeSpan))
            {
                timeSpan = _config.ReservationTimeout;
            }

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
        /// <exception cref="StockException"></exception>
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
            if (_stockPerStoreCache.Cache.ContainsKey(storeAlias))
            {
                _log.Info("Debug: EnsurePerStoreEntryExists: Store cache found");
            }
            else
            {
                _log.Info("Debug: EnsurePerStoreEntryExists: Store cache NOT found");
            }

            if (_stockPerStoreCache.Cache.ContainsKey(storeAlias) && !_stockPerStoreCache.Cache[storeAlias].ContainsKey(key))
            {
                _stockPerStoreCache.Cache[storeAlias][key]
                    = _stockRepo.CreateNewStockRecord($"{storeAlias}_{key}");
            }
        }

        /// <summary>
        /// Sets stock of provided <see cref="StockData"/> item and updates database.
        /// Ensures proper locking while change is performed.
        /// </summary>
        /// <param name="stockData"></param>
        /// <param name="value"></param>
        /// <exception cref="ArgumentException">
        /// Throws an exception when current value and provided value are equal
        /// </exception>
        /// <exception cref="ArgumentNullException"/>
        private bool SetStockWithLock(StockData stockData, int value)
        {
            if (stockData == null)
            {
                throw new ArgumentNullException(nameof(stockData));
            }
            if (stockData.Stock == value)
            {
                throw new ArgumentException($"Stock is already set to provided value.", nameof(value));
            }
            if (value < 0)
            {
                throw new ArgumentException($"Cannot set stock of {stockData.UniqueId} to negative number.", nameof(value));
            }

            lock (stockData)
            {
                var oldValue = stockData.Stock;
                stockData.Stock = value;

                _stockRepo.Set(stockData.UniqueId, value, oldValue);

                return true;
            }
        }

        /// <summary>
        /// Allows hangfire to serialise the method call to database
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void UpdateStockHangfire(Guid key, int value)
        {
            Instance.UpdateStock(key, value);
        }
        /// <summary>
        /// Allows hangfire to serialise the method call to database
        /// </summary>
        /// <param name="key"></param>
        /// <param name="storeAlias"></param>
        /// <param name="value"></param>
        public static void UpdateStockHangfire(Guid key, string storeAlias, int value)
        {
            Instance.UpdateStock(key, storeAlias, value);
        }
    }
}
